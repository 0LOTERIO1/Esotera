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
/// Orquestra a inserção do frete no carrinho do Melhor Envio (Fase C1).
///
/// Só chama POST /me/cart. Não existe caminho de código aqui para checkout,
/// generate ou print — e o escopo shipping-checkout não é sequer solicitado.
/// </summary>
public sealed class MelhorEnvioShipmentProcessingService : IMelhorEnvioShipmentProcessingService
{
    private const string ReauthorizeMessage =
        "Reautorize o Melhor Envio com os novos escopos antes de criar envio.";

    private readonly EsoteraDbContext _db;
    private readonly IMelhorEnvioOAuthService _oauth;
    private readonly IMelhorEnvioShipmentClient _client;
    private readonly MelhorEnvioOptions _options;
    private readonly MelhorEnvioSenderOptions _sender;
    private readonly ILogger<MelhorEnvioShipmentProcessingService> _logger;

    public MelhorEnvioShipmentProcessingService(
        EsoteraDbContext db,
        IMelhorEnvioOAuthService oauth,
        IMelhorEnvioShipmentClient client,
        IOptions<MelhorEnvioOptions> options,
        IOptions<MelhorEnvioSenderOptions> sender,
        ILogger<MelhorEnvioShipmentProcessingService> logger)
    {
        _db = db;
        _oauth = oauth;
        _client = client;
        _options = options.Value;
        _sender = sender.Value;
        _logger = logger;
    }

    public async Task<MelhorEnvioCartCreationResult> CreateCartShipmentAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _db.MelhorEnvioShipments
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.OrderId == orderId, cancellationToken);

        if (snapshot is null)
        {
            return MelhorEnvioCartCreationResult.Blocked(
                MelhorEnvioShipmentErrorCodes.ShipmentMissing,
                "Nenhum registro Melhor Envio para este pedido. Use \"Preparar envio\" primeiro.");
        }

        // Idempotência forte: com id do Melhor Envio já salvo, nunca criar outro.
        if (!string.IsNullOrWhiteSpace(snapshot.MelhorEnvioShipmentId))
        {
            return new MelhorEnvioCartCreationResult(
                Ok: true,
                Status: snapshot.Status,
                ShipmentId: snapshot.MelhorEnvioShipmentId,
                Protocol: snapshot.MelhorEnvioProtocol,
                ErrorCode: null,
                ErrorMessage: null,
                AlreadyCreated: true);
        }

        if (snapshot.Status != MelhorEnvioShipmentStatus.ReadyToCreate)
        {
            return MelhorEnvioCartCreationResult.Blocked(
                MelhorEnvioShipmentErrorCodes.StatusNotReady,
                $"Envio no status \"{snapshot.Status}\". Só é possível criar quando está pronto para criar envio.");
        }

        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null)
        {
            return MelhorEnvioCartCreationResult.Blocked(
                MelhorEnvioShipmentErrorCodes.OrderMissing,
                "Pedido não encontrado.");
        }

        if (!ShippingMethod.IsMelhorEnvio(order.ShippingMethodId))
        {
            return MelhorEnvioCartCreationResult.Blocked(
                MelhorEnvioShipmentErrorCodes.NotMelhorEnvioShipping,
                "Pedido não usa entrega Melhor Envio.");
        }

        if (order.Status != OrderStatus.PaymentApproved)
        {
            return MelhorEnvioCartCreationResult.Blocked(
                MelhorEnvioShipmentErrorCodes.PaymentNotApproved,
                "Pagamento não aprovado.");
        }

        var invoiceKey = await _db.FiscalInvoices
            .AsNoTracking()
            .Where(f => f.OrderId == orderId && f.Status == FiscalInvoiceStatus.Authorized)
            .OrderByDescending(f => f.AuthorizedAtUtc)
            .Select(f => f.ChNFe)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(invoiceKey))
        {
            return MelhorEnvioCartCreationResult.Blocked(
                MelhorEnvioShipmentErrorCodes.InvoiceNotAuthorized,
                "NF-e autorizada não encontrada para este pedido.");
        }

        if (!_options.IsOAuthConfigured)
        {
            return MelhorEnvioCartCreationResult.Blocked(
                MelhorEnvioShipmentErrorCodes.NotConfigured,
                "Integração Melhor Envio não está configurada no servidor.");
        }

        var connection = await _db.MelhorEnvioConnections
            .AsNoTracking()
            .Select(c => new { c.Scopes, c.Environment })
            .FirstOrDefaultAsync(cancellationToken);

        if (connection is null)
        {
            return MelhorEnvioCartCreationResult.Blocked(
                MelhorEnvioShipmentErrorCodes.NotConfigured,
                "Melhor Envio não está conectado. Autorize a integração nas configurações.");
        }

        if (!string.Equals(
                connection.Environment?.Trim(),
                _options.NormalizedEnvironment,
                StringComparison.OrdinalIgnoreCase))
        {
            return MelhorEnvioCartCreationResult.Blocked(
                MelhorEnvioShipmentErrorCodes.EnvironmentMismatch,
                "A conexão salva pertence a outro ambiente. Reautorize o Melhor Envio.");
        }

        if (!MelhorEnvioOptions.HasAllScopes(connection.Scopes, MelhorEnvioOptions.CartCreationScopes))
        {
            return MelhorEnvioCartCreationResult.Blocked(
                MelhorEnvioShipmentErrorCodes.ScopeMissing,
                ReauthorizeMessage);
        }

        var settings = await _db.StoreSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (settings is null)
        {
            return MelhorEnvioCartCreationResult.Blocked(
                MelhorEnvioShipmentErrorCodes.NotConfigured,
                "Configurações da loja não encontradas.");
        }

        var payload = MelhorEnvioCartPayloadBuilder.Build(order, invoiceKey, settings, _sender);
        if (!payload.Ok)
        {
            // Dado obrigatório ausente: nada foi enviado. Registra o motivo e mantém
            // ready_to_create para o operador corrigir e repetir.
            await RecordFailureAsync(
                orderId,
                MelhorEnvioShipmentStatus.ReadyToCreate,
                payload.ErrorCode!,
                payload.ErrorMessage!,
                cancellationToken);

            return MelhorEnvioCartCreationResult.Blocked(payload.ErrorCode!, payload.ErrorMessage!);
        }

        // Claim atômico: só um processo sai de ready_to_create para cart_pending.
        var claimed = await _db.MelhorEnvioShipments
            .Where(s => s.OrderId == orderId
                && s.Status == MelhorEnvioShipmentStatus.ReadyToCreate
                && s.MelhorEnvioShipmentId == null)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(s => s.Status, MelhorEnvioShipmentStatus.CartPending)
                    .SetProperty(s => s.UpdatedAtUtc, DateTime.UtcNow),
                cancellationToken);

        if (claimed != 1)
        {
            return MelhorEnvioCartCreationResult.Blocked(
                MelhorEnvioShipmentErrorCodes.ClaimLost,
                "Outro processo já está criando este envio. Recarregue o pedido.");
        }

        MelhorEnvioCartOutcome outcome;
        try
        {
            outcome = await _oauth.ExecuteWithTokenRetryAsync(
                (token, ct) => _client.CreateCartItemAsync(payload.Request!, token, ct),
                r => r.Unauthenticated,
                cancellationToken);
        }
        catch (MelhorEnvioOAuthException ex)
        {
            _logger.LogWarning(
                "Melhor Envio cart: token indisponível para pedido {OrderId} (reason={Reason})",
                orderId,
                ex.ReasonCode);

            return await FailAndReturnAsync(
                orderId,
                MelhorEnvioShipmentStatus.ReadyToCreate,
                MelhorEnvioShipmentErrorCodes.TokenUnavailable,
                ReauthorizeMessage,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Deixa em cart_pending de propósito: resultado desconhecido.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Melhor Envio cart: falha inesperada no pedido {OrderId}", orderId);
            return await FailAndReturnAsync(
                orderId,
                MelhorEnvioShipmentStatus.Failed,
                MelhorEnvioShipmentErrorCodes.Unexpected,
                "Falha inesperada ao criar o envio. Verifique o painel do Melhor Envio antes de repetir.",
                cancellationToken);
        }

        if (outcome.Ok)
        {
            var shipment = await _db.MelhorEnvioShipments
                .FirstAsync(s => s.OrderId == orderId, cancellationToken);

            var now = DateTime.UtcNow;
            shipment.MelhorEnvioShipmentId = outcome.ShipmentId;
            shipment.MelhorEnvioProtocol = outcome.Protocol;
            shipment.Status = MelhorEnvioShipmentStatus.CartCreated;
            shipment.CartCreatedAtUtc = now;
            shipment.LastSyncAtUtc = now;
            shipment.LastSyncErrorCode = null;
            shipment.LastSyncErrorMessage = null;
            shipment.UpdatedAtUtc = now;

            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Melhor Envio cart: pedido {OrderId} inserido no carrinho (status=cart_created)",
                orderId);

            return new MelhorEnvioCartCreationResult(
                Ok: true,
                Status: shipment.Status,
                ShipmentId: shipment.MelhorEnvioShipmentId,
                Protocol: shipment.MelhorEnvioProtocol,
                ErrorCode: null,
                ErrorMessage: null);
        }

        // Política de status na falha:
        // - recusa ANTES de criar algo (401/403) → volta a ready_to_create: é seguro repetir
        //   depois de reautorizar, e o pedido continua aparecendo como pendente de ação.
        // - resto (validação 4xx, 5xx, timeout, resposta sem id) → failed: ou exige correção
        //   de dados, ou o resultado é desconhecido e repetir cegamente pode duplicar item
        //   no carrinho. failed obriga conferência humana antes de tentar de novo.
        var revertToReady = outcome.Unauthenticated || outcome.Forbidden;
        var nextStatus = revertToReady
            ? MelhorEnvioShipmentStatus.ReadyToCreate
            : MelhorEnvioShipmentStatus.Failed;

        var message = outcome.Forbidden
            ? ReauthorizeMessage
            : outcome.ErrorMessage ?? "Falha ao criar o envio no Melhor Envio.";

        return await FailAndReturnAsync(
            orderId,
            nextStatus,
            outcome.ErrorCode ?? MelhorEnvioShipmentErrorCodes.Unexpected,
            message,
            cancellationToken);
    }

    private async Task<MelhorEnvioCartCreationResult> FailAndReturnAsync(
        Guid orderId,
        string status,
        string errorCode,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        await RecordFailureAsync(orderId, status, errorCode, errorMessage, cancellationToken);
        return new MelhorEnvioCartCreationResult(
            Ok: false,
            Status: status,
            ShipmentId: null,
            Protocol: null,
            ErrorCode: errorCode,
            ErrorMessage: errorMessage);
    }

    private async Task RecordFailureAsync(
        Guid orderId,
        string status,
        string errorCode,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        var code = MelhorEnvioShipmentErrorCodes.Sanitize(errorCode);
        var message = MelhorEnvioShipmentErrorCodes.SanitizeMessage(errorMessage);
        var now = DateTime.UtcNow;

        await _db.MelhorEnvioShipments
            .Where(s => s.OrderId == orderId)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(s => s.Status, status)
                    .SetProperty(s => s.LastSyncAtUtc, now)
                    .SetProperty(s => s.LastSyncErrorCode, code)
                    .SetProperty(s => s.LastSyncErrorMessage, message)
                    .SetProperty(s => s.UpdatedAtUtc, now),
                cancellationToken);
    }
}
