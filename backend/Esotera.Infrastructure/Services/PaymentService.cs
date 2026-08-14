using Esotera.Application.DTOs.Payments;
using Esotera.Application.Exceptions;
using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Esotera.Domain.Enums;
using Esotera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Esotera.Infrastructure.Services;

public class PaymentService : IPaymentService
{
    private const string Brl = "BRL";
    private const decimal AmountTolerance = 0.01m;

    private readonly EsoteraDbContext _context;
    private readonly IMercadoPagoClient _mp;
    private readonly MercadoPagoOptions _options;
    private readonly IJ3FulfillmentService _j3Fulfillment;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        EsoteraDbContext context,
        IMercadoPagoClient mp,
        IOptions<MercadoPagoOptions> options,
        IJ3FulfillmentService j3Fulfillment,
        ILogger<PaymentService> logger)
    {
        _context = context;
        _mp = mp;
        _options = options.Value;
        _j3Fulfillment = j3Fulfillment;
        _logger = logger;
    }

    public PaymentEnvironmentConfigDto GetPublicConfig() =>
        new(
            _options.EnvironmentKind.ToString(),
            _options.CanUseSandboxPixTest,
            _options.SandboxPixAmount,
            CommercialCheckoutAllowedInCurrentEnvironment());

    public async Task<CreatePaymentResponse> CreateForOrderAsync(
        Guid userId,
        Guid orderId,
        CreatePaymentRequest request,
        string paymentIdempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
            throw new ValidationException(
                "payment",
                "Pagamento ainda não está configurado. Defina MERCADO_PAGO_ACCESS_TOKEN no servidor.");

        var key = paymentIdempotencyKey.Trim();
        if (key.Length is < 8 or > 64)
            throw new ValidationException("idempotencyKey", "Idempotency-Key de pagamento inválida.");

        var order = await _context.Orders
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Pedido", orderId);

        if (order.Status is OrderStatus.PaymentApproved or OrderStatus.Cancelled)
            throw new ConflictException("Este pedido não aceita novo pagamento.");

        var methodId = (request.PaymentMethodId ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(methodId))
            throw new ValidationException("paymentMethodId", "Informe o método de pagamento.");

        if (methodId is not "pix")
        {
            throw new ValidationException(
                "paymentMethodId",
                "Nesta fase somente Pix está disponível. Cartão e boleto em breve.");
        }

        // Em Test, só permite checkout comercial se o total coincidir com o valor oficial de teste.
        // Nunca altera silenciosamente o total do pedido.
        if (_options.IsTestEnvironment
            && Math.Abs(order.Total - _options.SandboxPixAmount) > AmountTolerance)
        {
            throw new ValidationException("payment", MercadoPagoOptions.CommercialSandboxBlockedMessage);
        }

        if (!string.IsNullOrWhiteSpace(order.PaymentIdempotencyKey)
            && string.Equals(order.PaymentIdempotencyKey, key, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(order.MercadoPagoOrderId))
        {
            var existing = await _mp.GetOrderAsync(order.MercadoPagoOrderId, cancellationToken);
            return MapResponse(order, existing);
        }

        if (!string.IsNullOrWhiteSpace(order.PaymentIdempotencyKey)
            && !string.Equals(order.PaymentIdempotencyKey, key, StringComparison.Ordinal))
        {
            throw new ConflictException(
                "Já existe uma tentativa de pagamento para este pedido com outra chave de idempotência.");
        }

        if (order.PaymentMethod != PaymentMethod.Pix)
            order.PaymentMethod = PaymentMethod.Pix;
        order.PaymentInstallments = null;

        var (payerEmail, payerFirstName, payerCpf) = ResolvePayerForEnvironment(order, request);

        var snapshot = await _mp.CreatePaymentAsync(
            new MercadoPagoCreatePaymentCommand(
                TransactionAmount: order.Total,
                Description: _options.IsTestEnvironment
                    ? null
                    : $"Pedido Esotera {order.OrderNumber}",
                ExternalReference: order.Id.ToString("D"),
                PayerEmail: payerEmail,
                PayerFirstName: payerFirstName,
                PayerCpf: payerCpf,
                PaymentMethodId: "pix",
                Token: null,
                Installments: 1,
                IssuerId: null,
                NotificationUrl: _options.ResolveNotificationUrl(),
                IsSandboxOfficialTest: false),
            key,
            cancellationToken);

        ValidatePaymentMatchesOrder(order, snapshot);

        order.PaymentIdempotencyKey = key;
        order.MercadoPagoOrderId = snapshot.OrderId;
        order.MercadoPagoPaymentId = snapshot.TransactionPaymentId;
        order.MercadoPagoPaymentStatus = snapshot.Status;
        order.PaymentStatus = MapPaymentStatus(snapshot.Status, snapshot.StatusDetail);
        order.UpdatedAtUtc = DateTime.UtcNow;

        ApplyStatusFromMercadoPago(
            order,
            snapshot.Status,
            snapshot.StatusDetail,
            "Order Pix criada no Mercado Pago");

        await PersistOrderAndJ3PendingAtomicallyAsync(order, cancellationToken);
        return MapResponse(order, snapshot);
    }

    public async Task<SandboxPixTestResponse> CreateSandboxPixTestAsync(
        Guid userId,
        string paymentIdempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (_options.IsProductionEnvironment || !_options.CanUseSandboxPixTest)
        {
            throw new ForbiddenException(
                "O teste Pix controlado só está disponível em ambiente Mercado Pago Test.");
        }

        if (!_options.IsConfigured)
            throw new ValidationException(
                "payment",
                "Pagamento ainda não está configurado. Defina MERCADO_PAGO_ACCESS_TOKEN no servidor.");

        var key = paymentIdempotencyKey.Trim();
        if (key.Length is < 8 or > 64)
            throw new ValidationException("idempotencyKey", "Idempotency-Key de pagamento inválida.");

        var externalReference =
            $"{MercadoPagoOptions.SandboxExternalReferencePrefix}{Guid.NewGuid():N}";
        if (externalReference.Length > 64)
            externalReference = externalReference[..64];

        var snapshot = await _mp.CreatePaymentAsync(
            new MercadoPagoCreatePaymentCommand(
                TransactionAmount: _options.SandboxPixAmount,
                Description: null,
                ExternalReference: externalReference,
                PayerEmail: MercadoPagoOptions.SandboxPayerEmail,
                PayerFirstName: MercadoPagoOptions.SandboxPayerFirstName,
                PayerCpf: null,
                PaymentMethodId: "pix",
                Token: null,
                Installments: 1,
                IssuerId: null,
                NotificationUrl: _options.ResolveNotificationUrl(),
                IsSandboxOfficialTest: true),
            key,
            cancellationToken);

        _logger.LogInformation(
            "Sandbox Pix teste gerado (sem pedido comercial). UserId={UserId} OrderId={OrderId} Amount={Amount}",
            userId,
            snapshot.OrderId,
            snapshot.TransactionAmount);

        return new SandboxPixTestResponse(
            snapshot.OrderId,
            snapshot.TransactionPaymentId,
            snapshot.TransactionAmount,
            Brl,
            snapshot.Status,
            snapshot.StatusDetail,
            snapshot.ExternalReference,
            snapshot.TicketUrl,
            snapshot.QrCode,
            snapshot.QrCodeBase64,
            snapshot.DateOfExpiration,
            "Ambiente de teste — nenhuma cobrança real será realizada. Pix de teste de R$ 50,00 gerado.",
            IsSandboxTest: true);
    }

    public async Task ProcessWebhookAsync(
        string? rawBody,
        string? xSignature,
        string? xRequestId,
        string? dataIdFromQuery,
        CancellationToken cancellationToken = default)
    {
        var dataId = dataIdFromQuery
            ?? MercadoPagoWebhookSignature.ExtractDataIdFromBody(rawBody);

        if (!MercadoPagoWebhookSignature.IsValid(
                xSignature,
                xRequestId,
                dataId,
                _options.WebhookSecret,
                _logger))
        {
            throw new ForbiddenException("Assinatura do webhook inválida.");
        }

        if (string.IsNullOrWhiteSpace(dataId))
        {
            _logger.LogInformation("Webhook MP ignorado: sem data.id.");
            return;
        }

        if (!_options.IsConfigured)
        {
            _logger.LogWarning("Webhook MP recebido sem Access Token configurado.");
            return;
        }

        MercadoPagoPaymentSnapshot mpOrder;
        try
        {
            mpOrder = await _mp.GetOrderAsync(dataId, cancellationToken);
        }
        catch (NotFoundException)
        {
            _logger.LogInformation(
                "Webhook MP: order {DataId} inexistente (notificação simulada ou ID inválido) — ignorada.",
                dataId);
            return;
        }

        if (_options.IsSandboxTestExternalReference(mpOrder.ExternalReference))
        {
            _logger.LogInformation(
                "Webhook MP: order de teste sandbox {OrderId} ignorada (não atualiza pedido comercial).",
                mpOrder.OrderId);
            return;
        }

        if (!Guid.TryParse(mpOrder.ExternalReference, out var orderId))
        {
            _logger.LogWarning("Webhook MP: external_reference inválido.");
            return;
        }

        var order = await _context.Orders
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
        {
            _logger.LogWarning("Webhook MP: pedido não encontrado para external_reference.");
            return;
        }

        // Não associa order de valor incompatível (ex.: teste R$50 em pedido comercial diferente).
        if (mpOrder.TransactionAmount > 0
            && Math.Abs(mpOrder.TransactionAmount - order.Total) > AmountTolerance)
        {
            _logger.LogWarning(
                "Webhook MP: valor da order incompatível com pedido {OrderId} — ignorado.",
                order.Id);
            return;
        }

        ValidatePaymentMatchesOrder(order, mpOrder);

        var mappedPaymentStatus = MapPaymentStatus(mpOrder.Status, mpOrder.StatusDetail);

        if (string.Equals(order.MercadoPagoOrderId, mpOrder.OrderId, StringComparison.Ordinal)
            && string.Equals(order.MercadoPagoPaymentStatus, mpOrder.Status, StringComparison.OrdinalIgnoreCase)
            && order.PaymentStatus == mappedPaymentStatus
            && MatchesOrderStatus(order.Status, mpOrder.Status, mpOrder.StatusDetail))
        {
            _logger.LogInformation(
                "Webhook MP repetido ignorado (idempotente) para pedido {OrderId}.",
                order.Id);
            // Mesmo request não duplica EnsurePending. Webhook repetido repara Pending ausente
            // (janela SaveChanges approved → falha EnsurePending) sem HTTP 5xx / retry storm.
            await EnsureJ3FulfillmentPendingIfApprovedAsync(order, cancellationToken);
            return;
        }

        order.MercadoPagoOrderId = mpOrder.OrderId;
        if (!string.IsNullOrWhiteSpace(mpOrder.TransactionPaymentId))
            order.MercadoPagoPaymentId = mpOrder.TransactionPaymentId;
        order.MercadoPagoPaymentStatus = mpOrder.Status;
        order.PaymentStatus = mappedPaymentStatus;
        order.UpdatedAtUtc = DateTime.UtcNow;

        ApplyStatusFromMercadoPago(
            order,
            mpOrder.Status,
            mpOrder.StatusDetail,
            $"Webhook MP order: {mpOrder.Status}/{mpOrder.StatusDetail}");

        // GetOrderAsync (HTTP MP) já terminou. Transaction só banco local.
        await PersistOrderAndJ3PendingAtomicallyAsync(order, cancellationToken);
        _logger.LogInformation(
            "Pedido {OrderId} atualizado via webhook MP (OrderId={MpOrderId} Status={Status} StatusDetail={StatusDetail}).",
            order.Id,
            mpOrder.OrderId,
            mpOrder.Status,
            mpOrder.StatusDetail);
    }

    /// <summary>
    /// J3 + payment_approved (relacional): Order + histórico + Pending na mesma transaction.
    /// PAC/SEDEX e InMemory: SaveChanges + EnsurePending sem transaction extra.
    /// Zero HTTP dentro da transaction.
    /// </summary>
    private async Task PersistOrderAndJ3PendingAtomicallyAsync(
        Domain.Entities.Order order,
        CancellationToken cancellationToken)
    {
        var j3Approved = order.Status == OrderStatus.PaymentApproved
            && string.Equals(order.ShippingMethodId, ShippingMethod.J3, StringComparison.OrdinalIgnoreCase);

        if (!j3Approved || !_context.Database.IsRelational())
        {
            await _context.SaveChangesAsync(cancellationToken);
            await EnsureJ3FulfillmentPendingIfApprovedAsync(order, cancellationToken);
            return;
        }

        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await EnsureJ3FulfillmentPendingIfApprovedAsync(order, cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Invariante: payment_approved AND ShippingMethodId == j3 → exatamente um J3Fulfillment.
    /// J3_FULFILLMENT_ENABLED não participa. Zero HTTP J3 / zero processor.
    /// </summary>
    private Task EnsureJ3FulfillmentPendingIfApprovedAsync(
        Domain.Entities.Order order,
        CancellationToken cancellationToken) =>
        order.Status == OrderStatus.PaymentApproved
            ? _j3Fulfillment.EnsurePendingAsync(order.Id, cancellationToken)
            : Task.CompletedTask;

    private bool CommercialCheckoutAllowedInCurrentEnvironment()
    {
        if (_options.IsProductionEnvironment)
            return true;
        // Em Test, checkout comercial só faz sentido se o valor oficial de teste for usado.
        return _options.CanUseSandboxPixTest;
    }

    private (string Email, string? FirstName, string? Cpf) ResolvePayerForEnvironment(
        Domain.Entities.Order order,
        CreatePaymentRequest request)
    {
        if (_options.IsTestEnvironment)
        {
            return (
                MercadoPagoOptions.SandboxPayerEmail,
                MercadoPagoOptions.SandboxPayerFirstName,
                null);
        }

        var email = string.IsNullOrWhiteSpace(request.PayerEmail)
            ? order.CustomerEmail
            : request.PayerEmail.Trim();
        return (email, null, order.CustomerCpf);
    }

    private void ValidatePaymentMatchesOrder(Domain.Entities.Order order, MercadoPagoPaymentSnapshot payment)
    {
        if (!string.Equals(payment.CurrencyId, Brl, StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("Moeda do pagamento divergente.");

        if (payment.TransactionAmount > 0
            && Math.Abs(payment.TransactionAmount - order.Total) > AmountTolerance)
            throw new ConflictException("Valor do pagamento divergente do pedido.");

        if (!string.IsNullOrWhiteSpace(payment.ExternalReference)
            && !string.Equals(payment.ExternalReference, order.Id.ToString("D"), StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("Referência externa do pagamento divergente.");
    }

    private void ApplyStatusFromMercadoPago(
        Domain.Entities.Order order,
        string mpStatus,
        string? mpStatusDetail,
        string note)
    {
        var target = ResolveEsoteraStatus(mpStatus, mpStatusDetail);
        if (target == null || order.Status == target)
            return;

        if (order.Status == OrderStatus.Cancelled && target == OrderStatus.PaymentApproved)
        {
            _logger.LogWarning(
                "Pedido {OrderId} está cancelado; approved/processed do MP ignorado para transição automática.",
                order.Id);
            return;
        }

        var from = order.Status;
        order.Status = target;
        // DbSet.Add: Guid PK em entidade nova na coleção de Order já tracked vira Modified
        // (UPDATE 0 rows) no SQLite/Postgres; InMemory ignora. Webhook respondia 200 sem persistir.
        _context.OrderStatusHistories.Add(new Domain.Entities.OrderStatusHistory
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            FromStatus = from,
            ToStatus = target,
            Note = note,
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    private static string? ResolveEsoteraStatus(string mpStatus, string? mpStatusDetail)
    {
        var status = (mpStatus ?? "").Trim().ToLowerInvariant();
        var detail = (mpStatusDetail ?? "").Trim().ToLowerInvariant();

        if (status is "processed" or "approved"
            || detail is "accredited")
            return OrderStatus.PaymentApproved;

        if (status is "cancelled" or "canceled" or "expired" or "failed" or "refunded" or "charged_back"
            || detail is "rejected" or "cancelled" or "canceled")
            return OrderStatus.Cancelled;

        if (status is "action_required" or "created" or "pending" or "in_process" or "in_mediation"
            || detail is "waiting_transfer" or "pending_waiting_transfer")
            return OrderStatus.AwaitingPayment;

        return null;
    }

    private static bool MatchesOrderStatus(string orderStatus, string mpStatus, string? mpStatusDetail)
    {
        var target = ResolveEsoteraStatus(mpStatus, mpStatusDetail);
        return target == null || orderStatus == target;
    }

    private static string MapPaymentStatus(string mpStatus, string? mpStatusDetail)
    {
        var resolved = ResolveEsoteraStatus(mpStatus, mpStatusDetail);
        return resolved switch
        {
            OrderStatus.PaymentApproved => "approved",
            OrderStatus.Cancelled => "cancelled",
            _ => "pending"
        };
    }

    private static CreatePaymentResponse MapResponse(
        Domain.Entities.Order order,
        MercadoPagoPaymentSnapshot payment)
    {
        var uiStatus = MapPaymentStatus(payment.Status, payment.StatusDetail);
        var awaiting = uiStatus == "pending";
        var message = uiStatus == "approved"
            ? "Pagamento aprovado."
            : awaiting
                ? "Aguardando pagamento. Pix gerado — escaneie o QR Code ou use o código copia e cola."
                : "Pagamento em processamento.";

        return new(
            order.Id,
            order.OrderNumber,
            order.Total,
            Brl,
            uiStatus,
            payment.OrderId,
            payment.TransactionPaymentId,
            payment.TicketUrl,
            payment.QrCode,
            payment.QrCodeBase64,
            payment.DateOfExpiration,
            message);
    }
}
