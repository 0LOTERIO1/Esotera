using Esotera.Application.DTOs.Payments;
using Esotera.Application.Exceptions;
using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Esotera.Application.Validators;
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

        if (order.Status == OrderStatus.PaymentApproved)
            throw new ConflictException("Este pedido já está pago e não aceita novo pagamento.");

        if (order.Status == OrderStatus.Cancelled)
            throw new ConflictException("Este pedido não aceita novo pagamento.");

        var methodType = CreatePaymentRequestValidator.ResolveType(request)
            ?? throw new ValidationException(
                "paymentMethodType",
                "Tipo de pagamento inválido. Use bank_transfer, credit_card, debit_card ou ticket.");

        var methodId = (request.PaymentMethodId ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(methodId))
            throw new ValidationException("paymentMethodId", "Informe o método de pagamento.");

        // Em Test, só permite checkout comercial se o total coincidir com o valor oficial de teste.
        if (_options.IsTestEnvironment
            && Math.Abs(order.Total - _options.SandboxPixAmount) > AmountTolerance)
        {
            throw new ValidationException("payment", MercadoPagoOptions.CommercialSandboxBlockedMessage);
        }

        if (!string.IsNullOrWhiteSpace(order.PaymentIdempotencyKey)
            && string.Equals(order.PaymentIdempotencyKey, key, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(order.MercadoPagoOrderId))
        {
            // Mesma tentativa (duplo clique / retry de rede): reconsulta a order existente.
            var existing = await _mp.GetOrderAsync(order.MercadoPagoOrderId, cancellationToken);
            return MapResponse(order, existing, methodType);
        }

        if (!string.IsNullOrWhiteSpace(order.PaymentIdempotencyKey)
            && !string.Equals(order.PaymentIdempotencyKey, key, StringComparison.Ordinal))
        {
            if (!CanStartNewPaymentAttempt(order))
            {
                throw new ConflictException(
                    "Já existe uma tentativa de pagamento em aberto para este pedido. Aguarde a confirmação ou use a mesma chave de idempotência.");
            }

            // Tentativa anterior terminou em rejeição definitiva — libera nova cobrança.
            order.PaymentIdempotencyKey = null;
            order.MercadoPagoOrderId = null;
            order.MercadoPagoPaymentId = null;
            order.MercadoPagoPaymentStatus = null;
        }

        ApplyLocalPaymentMethod(order, methodType, request.Installments);

        var payer = ResolvePayerForEnvironment(order, request, methodType);
        if (methodType == "ticket" && string.IsNullOrWhiteSpace(payer.Cpf))
        {
            throw new ValidationException(
                "payerIdentification",
                "CPF do pagador é obrigatório para boleto. Atualize o cadastro do pedido.");
        }

        var snapshot = await _mp.CreatePaymentAsync(
            new MercadoPagoCreatePaymentCommand(
                TransactionAmount: order.Total,
                Description: _options.IsTestEnvironment
                    ? null
                    : $"Pedido Esotera {order.OrderNumber}",
                ExternalReference: order.Id.ToString("D"),
                PayerEmail: payer.Email,
                PayerFirstName: payer.FirstName,
                PayerLastName: payer.LastName,
                PayerCpf: payer.Cpf,
                PaymentMethodId: methodId,
                PaymentMethodType: methodType,
                Token: methodType is "credit_card" or "debit_card" ? request.Token?.Trim() : null,
                Installments: methodType == "credit_card" ? (request.Installments ?? 1) : null,
                IssuerId: string.IsNullOrWhiteSpace(request.IssuerId) ? null : request.IssuerId.Trim(),
                NotificationUrl: _options.ResolveNotificationUrl(),
                IsSandboxOfficialTest: false,
                PayerZipCode: order.ShipCep,
                PayerStreetName: order.ShipStreet,
                PayerStreetNumber: order.ShipNumber,
                PayerNeighborhood: order.ShipNeighborhood,
                PayerCity: order.ShipCity,
                PayerState: order.ShipState,
                PayerComplement: order.ShipComplement),
            key,
            cancellationToken);

        ValidatePaymentMatchesOrder(order, snapshot);

        var mappedPaymentStatus = MapPaymentStatus(snapshot.Status, snapshot.StatusDetail);

        order.PaymentIdempotencyKey = key;
        order.MercadoPagoOrderId = snapshot.OrderId;
        order.MercadoPagoPaymentId = snapshot.TransactionPaymentId;
        order.MercadoPagoPaymentStatus = snapshot.Status;
        order.PaymentStatus = mappedPaymentStatus;
        order.UpdatedAtUtc = DateTime.UtcNow;

        // Rejeição definitiva: libera nova tentativa (limpa key) sem cancelar o pedido.
        if (IsDefinitivePaymentFailure(snapshot.Status, snapshot.StatusDetail))
        {
            order.PaymentIdempotencyKey = null;
            order.PaymentStatus = "rejected";
        }

        ApplyStatusFromMercadoPago(
            order,
            snapshot.Status,
            snapshot.StatusDetail,
            $"Order {methodType} criada no Mercado Pago");

        await PersistOrderAndJ3PendingAtomicallyAsync(order, cancellationToken);
        return MapResponse(order, snapshot, methodType);
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
                PayerLastName: null,
                PayerCpf: null,
                PaymentMethodId: "pix",
                PaymentMethodType: "bank_transfer",
                Token: null,
                Installments: null,
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

        if (mpOrder.TransactionAmount > 0
            && Math.Abs(mpOrder.TransactionAmount - order.Total) > AmountTolerance)
        {
            _logger.LogWarning(
                "Webhook MP: valor da order incompatível com pedido {OrderId} — ignorado.",
                order.Id);
            return;
        }

        // Tentativa atual: webhook de outra order MP = stale (não sobrescreve B com A).
        // MercadoPagoOrderId null → recovery (POST remoto ok, persistência incompleta).
        if (!string.IsNullOrWhiteSpace(order.MercadoPagoOrderId)
            && !string.Equals(
                order.MercadoPagoOrderId.Trim(),
                (mpOrder.OrderId ?? "").Trim(),
                StringComparison.Ordinal))
        {
            _logger.LogInformation(
                "stale Mercado Pago order notification ignored. OrderId={OrderId} HasCurrentMpOrderId={HasCurrent} HasReceivedMpOrderId={HasReceived}",
                order.Id,
                true,
                !string.IsNullOrWhiteSpace(mpOrder.OrderId));
            return;
        }

        ValidatePaymentMatchesOrder(order, mpOrder);

        // payment_approved monotônico: ignora pending/rejected/etc. da tentativa atual.
        // Só reversões financeiras reais (refunded/charged_back) seguem.
        if (order.Status == OrderStatus.PaymentApproved
            && !IsFinancialReversal(mpOrder.Status, mpOrder.StatusDetail))
        {
            _logger.LogInformation(
                "Webhook MP ignorado (payment_approved monotônico) para pedido {OrderId}.",
                order.Id);
            await EnsureJ3FulfillmentPendingIfApprovedAsync(order, cancellationToken);
            return;
        }

        var mappedPaymentStatus = MapPaymentStatus(mpOrder.Status, mpOrder.StatusDetail);

        if (string.Equals(order.MercadoPagoOrderId, mpOrder.OrderId, StringComparison.Ordinal)
            && string.Equals(order.MercadoPagoPaymentStatus, mpOrder.Status, StringComparison.OrdinalIgnoreCase)
            && order.PaymentStatus == mappedPaymentStatus
            && MatchesOrderStatus(order.Status, mpOrder.Status, mpOrder.StatusDetail))
        {
            _logger.LogInformation(
                "Webhook MP repetido ignorado (idempotente) para pedido {OrderId}.",
                order.Id);
            await EnsureJ3FulfillmentPendingIfApprovedAsync(order, cancellationToken);
            return;
        }

        order.MercadoPagoOrderId = mpOrder.OrderId;
        if (!string.IsNullOrWhiteSpace(mpOrder.TransactionPaymentId))
            order.MercadoPagoPaymentId = mpOrder.TransactionPaymentId;
        order.MercadoPagoPaymentStatus = mpOrder.Status;
        order.PaymentStatus = mappedPaymentStatus;
        order.UpdatedAtUtc = DateTime.UtcNow;

        // Nunca limpar key / marcar rejected se já aprovado (evita nova cobrança).
        if (IsDefinitivePaymentFailure(mpOrder.Status, mpOrder.StatusDetail)
            && order.Status != OrderStatus.PaymentApproved)
        {
            order.PaymentIdempotencyKey = null;
            order.PaymentStatus = "rejected";
        }

        ApplyStatusFromMercadoPago(
            order,
            mpOrder.Status,
            mpOrder.StatusDetail,
            $"Webhook MP order: {mpOrder.Status}/{mpOrder.StatusDetail}");

        await PersistOrderAndJ3PendingAtomicallyAsync(order, cancellationToken);
        _logger.LogInformation(
            "Pedido {OrderId} atualizado via webhook MP (OrderId={MpOrderId} Status={Status} StatusDetail={StatusDetail}).",
            order.Id,
            mpOrder.OrderId,
            mpOrder.Status,
            mpOrder.StatusDetail);
    }

    /// <summary>
    /// Nova tentativa só após falha definitiva (rejected/failed/expired/cancelled do MP)
    /// e sem pagamento aprovado. Pending/action_required/in_process = fail-closed.
    /// </summary>
    public static bool CanStartNewPaymentAttempt(Domain.Entities.Order order)
    {
        if (order.Status == OrderStatus.PaymentApproved)
            return false;

        if (string.Equals(order.PaymentStatus, "rejected", StringComparison.OrdinalIgnoreCase))
            return true;

        return IsDefinitivePaymentFailure(order.MercadoPagoPaymentStatus, null);
    }

    internal static bool IsDefinitivePaymentFailure(string? mpStatus, string? mpStatusDetail)
    {
        var status = (mpStatus ?? "").Trim().ToLowerInvariant();
        var detail = (mpStatusDetail ?? "").Trim().ToLowerInvariant();

        if (status is "rejected" or "failed" or "cancelled" or "canceled" or "expired")
            return true;

        if (detail is "rejected" or "cc_rejected_other_reason" or "cc_rejected_insufficient_amount"
            or "cc_rejected_bad_filled_security_code" or "cc_rejected_bad_filled_date"
            or "cc_rejected_bad_filled_other" or "cc_rejected_high_risk"
            or "cc_rejected_blacklist" or "cc_rejected_call_for_authorize")
            return true;

        return false;
    }

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
        return _options.CanUseSandboxPixTest;
    }

    private static void ApplyLocalPaymentMethod(
        Domain.Entities.Order order,
        string methodType,
        int? installments)
    {
        order.PaymentMethod = methodType switch
        {
            "bank_transfer" => PaymentMethod.Pix,
            "credit_card" or "debit_card" => PaymentMethod.Card,
            "ticket" => PaymentMethod.Boleto,
            _ => order.PaymentMethod,
        };

        order.PaymentInstallments = methodType == "credit_card" ? (installments ?? 1) : null;
    }

    private (string Email, string? FirstName, string? LastName, string? Cpf) ResolvePayerForEnvironment(
        Domain.Entities.Order order,
        CreatePaymentRequest request,
        string methodType)
    {
        if (_options.IsTestEnvironment)
        {
            // Boleto em Test ainda precisa de CPF válido no body Orders.
            var testCpf = methodType == "ticket"
                ? (TryNormalizeCpf(order.CustomerCpf) ?? "19119119100")
                : null;
            return (
                MercadoPagoOptions.SandboxPayerEmail,
                MercadoPagoOptions.SandboxPayerFirstName,
                "User",
                testCpf);
        }

        var email = string.IsNullOrWhiteSpace(request.PayerEmail)
            ? order.CustomerEmail
            : request.PayerEmail.Trim();

        // Preferir dados confiáveis do pedido; Brick só complementa se Order não tiver CPF.
        var cpf = TryNormalizeCpf(order.CustomerCpf)
            ?? TryNormalizeCpf(request.PayerIdentificationNumber);

        SplitCustomerName(order.CustomerName, out var first, out var last);

        return (email, first, last, cpf);
    }

    private static void SplitCustomerName(string? fullName, out string? first, out string? last)
    {
        first = null;
        last = null;
        if (string.IsNullOrWhiteSpace(fullName))
            return;

        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return;
        first = parts[0];
        if (parts.Length > 1)
            last = string.Join(' ', parts.Skip(1));
    }

    private static string? TryNormalizeCpf(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
            return null;
        var digits = new string(cpf.Where(char.IsDigit).ToArray());
        return digits.Length == 11 ? digits : null;
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

        // payment_approved só regride via reversão financeira (refunded/charged_back → cancelled).
        if (order.Status == OrderStatus.PaymentApproved
            && target != OrderStatus.Cancelled)
        {
            _logger.LogInformation(
                "Pedido {OrderId}: regressão de payment_approved ignorada (status remoto {MpStatus}/{MpDetail}).",
                order.Id,
                mpStatus,
                mpStatusDetail);
            return;
        }

        if (order.Status == OrderStatus.PaymentApproved
            && target == OrderStatus.Cancelled
            && !IsFinancialReversal(mpStatus, mpStatusDetail))
        {
            _logger.LogWarning(
                "Pedido {OrderId}: cancelamento sem reversão financeira reconhecida ignorado.",
                order.Id);
            return;
        }

        var from = order.Status;
        order.Status = target;
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

    /// <summary>Reversões financeiras reais já mapeadas — únicas que podem sair de payment_approved.</summary>
    internal static bool IsFinancialReversal(string? mpStatus, string? mpStatusDetail)
    {
        var status = (mpStatus ?? "").Trim().ToLowerInvariant();
        return status is "refunded" or "charged_back";
    }

    /// <summary>
    /// Rejeição/expiração de meio NÃO cancela o pedido (permite nova tentativa).
    /// Apenas refunded/charged_back após fluxo de pagamento → cancelled.
    /// </summary>
    private static string? ResolveEsoteraStatus(string mpStatus, string? mpStatusDetail)
    {
        var status = (mpStatus ?? "").Trim().ToLowerInvariant();
        var detail = (mpStatusDetail ?? "").Trim().ToLowerInvariant();

        if (status is "processed" or "approved"
            || detail is "accredited")
            return OrderStatus.PaymentApproved;

        if (status is "refunded" or "charged_back")
            return OrderStatus.Cancelled;

        // rejected / failed / cancelled / expired / pending → permanece ou volta a awaiting_payment
        if (status is "rejected" or "failed" or "cancelled" or "canceled" or "expired"
            || detail.StartsWith("cc_rejected", StringComparison.Ordinal)
            || detail is "rejected")
            return OrderStatus.AwaitingPayment;

        if (status is "action_required" or "created" or "pending" or "in_process" or "in_mediation"
            || detail is "waiting_transfer" or "pending_waiting_transfer" or "pending_waiting_payment")
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
        if (IsDefinitivePaymentFailure(mpStatus, mpStatusDetail))
            return "rejected";

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
        MercadoPagoPaymentSnapshot payment,
        string? methodTypeHint = null)
    {
        var uiStatus = MapPaymentStatus(payment.Status, payment.StatusDetail);
        var methodHint = (methodTypeHint ?? order.PaymentMethod ?? "").ToLowerInvariant();
        var message = uiStatus switch
        {
            "approved" => "Pagamento aprovado.",
            "rejected" => "Pagamento não aprovado. Você pode tentar outro meio ou cartão.",
            "pending" when methodHint is "ticket" or "boleto" =>
                "Boleto gerado. O pedido só será confirmado após a compensação.",
            "pending" when methodHint is "pix" or "bank_transfer" =>
                "Aguardando pagamento. Pix gerado — escaneie o QR Code ou use o código copia e cola.",
            "pending" => "Pagamento em processamento. Aguarde a confirmação.",
            _ => "Pagamento em processamento.",
        };

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
            message,
            payment.DigitableLine,
            payment.BarcodeContent);
    }
}
