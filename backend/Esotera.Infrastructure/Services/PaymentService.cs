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
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        EsoteraDbContext context,
        IMercadoPagoClient mp,
        IOptions<MercadoPagoOptions> options,
        ILogger<PaymentService> logger)
    {
        _context = context;
        _mp = mp;
        _options = options.Value;
        _logger = logger;
    }

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

        // Replay idempotente do mesmo pagamento
        if (!string.IsNullOrWhiteSpace(order.PaymentIdempotencyKey)
            && string.Equals(order.PaymentIdempotencyKey, key, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(order.MercadoPagoPaymentId))
        {
            var existing = await _mp.GetPaymentAsync(order.MercadoPagoPaymentId, cancellationToken);
            return MapResponse(order, existing);
        }

        if (!string.IsNullOrWhiteSpace(order.PaymentIdempotencyKey)
            && !string.Equals(order.PaymentIdempotencyKey, key, StringComparison.Ordinal))
        {
            throw new ConflictException(
                "Já existe uma tentativa de pagamento para este pedido com outra chave de idempotência.");
        }

        var methodId = (request.PaymentMethodId ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(methodId))
            throw new ValidationException("paymentMethodId", "Informe o método de pagamento.");

        var isPix = methodId is "pix";
        var isCard = !isPix;

        if (isCard && string.IsNullOrWhiteSpace(request.Token))
            throw new ValidationException("token", "Token do cartão é obrigatório.");

        if (isCard)
        {
            var installments = request.Installments ?? order.PaymentInstallments ?? 1;
            if (installments is < 1 or > 2)
                throw new ValidationException("installments", "Parcelas permitidas: 1 ou 2 sem juros.");
        }

        // Alinha método do pedido com o Brick (pix vs card)
        if (isPix && order.PaymentMethod != PaymentMethod.Pix)
            order.PaymentMethod = PaymentMethod.Pix;
        if (isCard && order.PaymentMethod != PaymentMethod.Card)
            order.PaymentMethod = PaymentMethod.Card;

        var installmentsForMp = isPix ? 1 : (request.Installments ?? order.PaymentInstallments ?? 1);
        order.PaymentInstallments = isCard ? installmentsForMp : null;

        var snapshot = await _mp.CreatePaymentAsync(
            new MercadoPagoCreatePaymentCommand(
                TransactionAmount: order.Total,
                Description: $"Pedido Esotera {order.OrderNumber}",
                ExternalReference: order.Id.ToString("D"),
                PayerEmail: string.IsNullOrWhiteSpace(request.PayerEmail)
                    ? order.CustomerEmail
                    : request.PayerEmail.Trim(),
                PayerCpf: order.CustomerCpf,
                PaymentMethodId: methodId,
                Token: isCard ? request.Token : null,
                Installments: installmentsForMp,
                IssuerId: request.IssuerId,
                NotificationUrl: _options.ResolveNotificationUrl()),
            key,
            cancellationToken);

        ValidatePaymentMatchesOrder(order, snapshot);

        order.PaymentIdempotencyKey = key;
        order.MercadoPagoPaymentId = snapshot.Id;
        order.MercadoPagoPaymentStatus = snapshot.Status;
        order.PaymentStatus = MapPaymentStatus(snapshot.Status);
        order.UpdatedAtUtc = DateTime.UtcNow;

        ApplyStatusFromMercadoPago(order, snapshot.Status, "Pagamento criado no Mercado Pago");

        await _context.SaveChangesAsync(cancellationToken);
        return MapResponse(order, snapshot);
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

        // Consulta segura na API — não confiar só no payload
        var payment = await _mp.GetPaymentAsync(dataId, cancellationToken);

        if (!Guid.TryParse(payment.ExternalReference, out var orderId))
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

        ValidatePaymentMatchesOrder(order, payment);

        // Idempotência: mesmo payment id + mesmo status → no-op
        if (string.Equals(order.MercadoPagoPaymentId, payment.Id, StringComparison.Ordinal)
            && string.Equals(order.MercadoPagoPaymentStatus, payment.Status, StringComparison.OrdinalIgnoreCase)
            && order.PaymentStatus == MapPaymentStatus(payment.Status)
            && MatchesOrderStatus(order.Status, payment.Status))
        {
            return;
        }

        order.MercadoPagoPaymentId = payment.Id;
        order.MercadoPagoPaymentStatus = payment.Status;
        order.PaymentStatus = MapPaymentStatus(payment.Status);
        order.UpdatedAtUtc = DateTime.UtcNow;

        ApplyStatusFromMercadoPago(order, payment.Status, $"Webhook MP: {payment.Status}");

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Pedido {OrderId} atualizado via webhook MP (status={Status}).",
            order.Id,
            payment.Status);
    }

    private void ValidatePaymentMatchesOrder(Domain.Entities.Order order, MercadoPagoPaymentSnapshot payment)
    {
        if (!string.Equals(payment.CurrencyId, Brl, StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("Moeda do pagamento divergente.");

        if (Math.Abs(payment.TransactionAmount - order.Total) > AmountTolerance)
            throw new ConflictException("Valor do pagamento divergente do pedido.");

        if (!string.IsNullOrWhiteSpace(payment.ExternalReference)
            && !string.Equals(payment.ExternalReference, order.Id.ToString("D"), StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("Referência externa do pagamento divergente.");
    }

    private void ApplyStatusFromMercadoPago(
        Domain.Entities.Order order,
        string mpStatus,
        string note)
    {
        var normalized = mpStatus.Trim().ToLowerInvariant();
        string? target = normalized switch
        {
            "approved" => OrderStatus.PaymentApproved,
            "rejected" or "cancelled" => OrderStatus.Cancelled,
            "refunded" or "charged_back" => OrderStatus.Cancelled,
            "pending" or "in_process" or "in_mediation" => OrderStatus.AwaitingPayment,
            _ => null
        };

        if (target == null || order.Status == target)
            return;

        // Não reabrir pedido cancelado para approved sem revisão manual admin
        if (order.Status == OrderStatus.Cancelled && target == OrderStatus.PaymentApproved)
        {
            _logger.LogWarning(
                "Pedido {OrderId} está cancelado; approved do MP ignorado para transição automática.",
                order.Id);
            return;
        }

        var from = order.Status;
        order.Status = target;
        order.StatusHistory.Add(new Domain.Entities.OrderStatusHistory
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            FromStatus = from,
            ToStatus = target,
            Note = note,
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    private static bool MatchesOrderStatus(string orderStatus, string mpStatus) =>
        mpStatus.Trim().ToLowerInvariant() switch
        {
            "approved" => orderStatus == OrderStatus.PaymentApproved,
            "rejected" or "cancelled" or "refunded" or "charged_back" =>
                orderStatus == OrderStatus.Cancelled,
            "pending" or "in_process" or "in_mediation" =>
                orderStatus == OrderStatus.AwaitingPayment,
            _ => true
        };

    private static string MapPaymentStatus(string mpStatus) =>
        mpStatus.Trim().ToLowerInvariant() switch
        {
            "approved" => "approved",
            "rejected" => "rejected",
            "cancelled" => "cancelled",
            "refunded" => "refunded",
            "charged_back" => "charged_back",
            _ => "pending"
        };

    private static CreatePaymentResponse MapResponse(
        Domain.Entities.Order order,
        MercadoPagoPaymentSnapshot payment) =>
        new(
            order.Id,
            order.OrderNumber,
            order.Total,
            Brl,
            payment.Status,
            payment.Id,
            payment.TicketUrl,
            payment.QrCode,
            payment.QrCodeBase64,
            payment.Status.Equals("approved", StringComparison.OrdinalIgnoreCase)
                ? "Pagamento aprovado."
                : payment.PaymentMethodId == "pix"
                    ? "Pix gerado. Escaneie o QR Code ou use o código copia e cola."
                    : "Pagamento em processamento.");
}
