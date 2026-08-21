using Esotera.Application.DTOs.J3;
using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Esotera.Application.Shipping;
using Esotera.Domain.Enums;
using Esotera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Esotera.Infrastructure.Services;

/// <summary>
/// Recovery Admin: gates → decrypt/parse → UMA chamada importOrderByAccessKey.
/// Zero createTmsOrders. Zero retry. Zero update de J3Fulfillment para Created.
/// </summary>
public sealed class J3ImportOrderByAccessKeyAdminService : IJ3ImportOrderByAccessKeyAdminService
{
    private readonly EsoteraDbContext _context;
    private readonly IJ3ImportOrderByAccessKeyClient _importClient;
    private readonly IIntegrationsEncryptionService _encryption;
    private readonly IFiscalInvoiceXmlParser _parser;
    private readonly J3ShippingOptions _j3;
    private readonly ILogger<J3ImportOrderByAccessKeyAdminService> _logger;

    public J3ImportOrderByAccessKeyAdminService(
        EsoteraDbContext context,
        IJ3ImportOrderByAccessKeyClient importClient,
        IIntegrationsEncryptionService encryption,
        IFiscalInvoiceXmlParser parser,
        IOptions<J3ShippingOptions> j3Options,
        ILogger<J3ImportOrderByAccessKeyAdminService> logger)
    {
        _context = context;
        _importClient = importClient;
        _encryption = encryption;
        _parser = parser;
        _j3 = j3Options.Value;
        _logger = logger;
    }

    public async Task<J3ImportByAccessKeyAdminOutcome> ImportAsync(
        Guid orderId,
        J3ImportByAccessKeyConfirmRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var order = await _context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
            return J3ImportByAccessKeyAdminOutcome.NotFound();

        var confirm = request.ConfirmOrderNumber?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(confirm)
            || !string.Equals(confirm, order.OrderNumber, StringComparison.Ordinal))
        {
            return J3ImportByAccessKeyAdminOutcome.BadRequest(
                "ConfirmOrderNumberMismatch",
                "Confirmação do número do pedido inválida.");
        }

        if (!_j3.ImportByAccessKeyEnabled)
        {
            return ConflictSnap(
                order,
                null,
                J3FulfillmentErrorCodes.ImportByAccessKeyDisabled,
                "Importação J3 por chave de acesso está desabilitada.");
        }

        if (_j3.FulfillmentEnabled)
        {
            return ConflictSnap(
                order,
                null,
                J3FulfillmentErrorCodes.FulfillmentMustBeDisabled,
                "Desabilite J3_FULFILLMENT_ENABLED antes do recovery importOrderByAccessKey.");
        }

        if (!string.Equals(order.ShippingMethodId, ShippingMethod.J3, StringComparison.Ordinal))
        {
            return ConflictSnap(
                order,
                null,
                J3FulfillmentEligibilityCodes.WrongShippingMethod,
                "Pedido não utiliza frete J3.");
        }

        if (!string.Equals(order.Status, OrderStatus.PaymentApproved, StringComparison.Ordinal))
        {
            return ConflictSnap(
                order,
                null,
                J3FulfillmentEligibilityCodes.PaymentNotApproved,
                "Pagamento ainda não aprovado.");
        }

        var fiscal = await _context.FiscalInvoices
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.OrderId == orderId, cancellationToken);
        if (fiscal is null)
        {
            return ConflictSnap(
                order,
                null,
                J3FulfillmentEligibilityCodes.MissingFiscalInvoice,
                "NF-e autorizada necessária.");
        }

        if (!string.Equals(fiscal.Status, FiscalInvoiceStatus.Authorized, StringComparison.Ordinal))
        {
            return ConflictSnap(
                order,
                null,
                J3FulfillmentEligibilityCodes.FiscalInvoiceNotAuthorized,
                "NF-e autorizada necessária.");
        }

        if (!J3FulfillmentEligibility.IsValidChNFe(fiscal.ChNFe))
        {
            return ConflictSnap(
                order,
                null,
                J3FulfillmentEligibilityCodes.InvalidNfeKey,
                "Chave da NF-e inválida.");
        }

        var fulfillment = await _context.J3Fulfillments
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.OrderId == orderId, cancellationToken);
        if (fulfillment is null
            || !string.Equals(fulfillment.Status, J3FulfillmentStatus.UnknownOutcome, StringComparison.Ordinal)
            || !string.Equals(
                fulfillment.LastErrorCode,
                J3FulfillmentErrorCodes.GraphqlAmbiguous,
                StringComparison.Ordinal))
        {
            return ConflictSnap(
                order,
                fulfillment,
                J3FulfillmentEligibilityCodes.UnknownOutcomeRequiresReview,
                "Recovery exige J3Fulfillment unknown_outcome com LastErrorCode GRAPHQL_AMBIGUOUS.");
        }

        if (string.IsNullOrWhiteSpace(_j3.SellerId))
        {
            return ConflictSnap(
                order,
                fulfillment,
                J3FulfillmentErrorCodes.MissingSellerId,
                "J3_SELLER_ID não configurado.");
        }

        if (string.IsNullOrWhiteSpace(_j3.SellerInformationId))
        {
            return ConflictSnap(
                order,
                fulfillment,
                J3FulfillmentErrorCodes.MissingSellerInformationId,
                "J3_SELLER_INFORMATION_ID não configurado.");
        }

        if (!_encryption.IsConfigured)
        {
            return ConflictSnap(
                order,
                fulfillment,
                J3FulfillmentErrorCodes.Configuration,
                "Criptografia de integrações não configurada.");
        }

        Application.DTOs.Fiscal.FiscalInvoiceParseResult parsed;
        try
        {
            parsed = J3ImportOrderByAccessKeyXmlSource.ParseFromCipher(
                fiscal.XmlCipher,
                _encryption,
                _parser);
        }
        catch (Exception)
        {
            _logger.LogWarning(
                "J3 import recovery order {OrderId}: XML decrypt/parse failed (details omitted).",
                orderId);
            return ConflictSnap(
                order,
                fulfillment,
                "INVALID_FISCAL_XML",
                "Não foi possível ler a NF-e autorizada.");
        }

        if (!parsed.HasAuthorizationEvidence
            || !string.Equals(parsed.ChNFe, fiscal.ChNFe, StringComparison.Ordinal))
        {
            return ConflictSnap(
                order,
                fulfillment,
                J3FulfillmentEligibilityCodes.FiscalInvoiceNotAuthorized,
                "NF-e autorizada necessária.");
        }

        if (string.IsNullOrWhiteSpace(parsed.IssuerAddress?.PhoneDigits)
            && string.IsNullOrWhiteSpace(J3ImportOrderByAccessKeyMapper.DigitsOrNull(_j3.EmitterPhone)))
        {
            return ConflictSnap(
                order,
                fulfillment,
                "MISSING_EMIT_PHONE",
                "Telefone do emitente ausente (XML e J3_EMITTER_PHONE).");
        }

        var mapped = J3ImportOrderByAccessKeyMapper.TryBuild(order, parsed, _j3);
        if (!mapped.IsValid || mapped.Command is null)
        {
            return ConflictSnap(
                order,
                fulfillment,
                mapped.ErrorCode ?? J3FulfillmentErrorCodes.Configuration,
                "Payload NfeDataInput inválido.");
        }

        _logger.LogInformation(
            "J3 operation {Operation} recovery starting order {OrderId} fulfillment {FulfillmentId}",
            J3ImportOrderByAccessKeyMutation.OperationName,
            orderId,
            fulfillment.Id);

        var attempt = await _importClient.ImportAsync(order, parsed, cancellationToken);

        // Re-read fulfillment to prove unchanged (we never write it).
        var fulfillmentAfter = await _context.J3Fulfillments
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.OrderId == orderId, cancellationToken);

        var body = BuildBody(order, fulfillmentAfter, attempt, httpSent: true);

        _logger.LogInformation(
            "J3 operation {Operation} recovery finished order {OrderId} outcome {Outcome} error {ErrorCode} fulfillmentUnchanged {Unchanged}",
            J3ImportOrderByAccessKeyMutation.OperationName,
            orderId,
            attempt.Outcome,
            attempt.ErrorCode,
            true);

        return attempt.Outcome switch
        {
            J3CreateOrderOutcome.Success => J3ImportByAccessKeyAdminOutcome.Ok(body),
            J3CreateOrderOutcome.DefiniteFailure => J3ImportByAccessKeyAdminOutcome.Unprocessable(
                attempt.ErrorCode ?? J3FulfillmentErrorCodes.Unknown,
                "importOrderByAccessKey rejeitado (sem retry).",
                body),
            _ => J3ImportByAccessKeyAdminOutcome.Conflict(
                attempt.ErrorCode ?? J3FulfillmentErrorCodes.GraphqlAmbiguous,
                "Resultado incerto de importOrderByAccessKey (sem retry). Fulfillment não alterado.",
                body)
        };
    }

    private J3ImportByAccessKeyAdminOutcome ConflictSnap(
        Domain.Entities.Order order,
        Domain.Entities.J3Fulfillment? fulfillment,
        string reasonCode,
        string message)
    {
        _logger.LogInformation(
            "J3 operation {Operation} recovery blocked order {OrderId} reason {ReasonCode} (no HTTP)",
            J3ImportOrderByAccessKeyMutation.OperationName,
            order.Id,
            reasonCode);

        return J3ImportByAccessKeyAdminOutcome.Conflict(
            reasonCode,
            message,
            BuildBody(order, fulfillment, attempt: null, httpSent: false));
    }

    private static J3ImportByAccessKeyAdminResultDto BuildBody(
        Domain.Entities.Order order,
        Domain.Entities.J3Fulfillment? fulfillment,
        J3CreateOrderAttemptResult? attempt,
        bool httpSent) =>
        new(
            order.Id,
            order.OrderNumber,
            fulfillment?.Id,
            fulfillment?.Status ?? "none",
            fulfillment?.LastErrorCode,
            FulfillmentUnchanged: true,
            Outcome: attempt?.Outcome.ToString() ?? "Blocked",
            ErrorCode: attempt?.ErrorCode,
            HttpSent: httpSent,
            OperationName: J3ImportOrderByAccessKeyMutation.OperationName);
}
