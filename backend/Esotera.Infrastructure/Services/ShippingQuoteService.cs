using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Esotera.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Esotera.Infrastructure.Services;

/// <summary>
/// Orquestra cotação: tenta transportadoras reais se configuradas; caso contrário
/// (ou em falha) usa <see cref="SimulatedShippingService"/> — sem inventar preços
/// a partir de erros de API.
/// </summary>
public sealed class ShippingQuoteService : IShippingQuoteService, ISimulatedShippingService
{
    private readonly SimulatedShippingService _simulated;
    private readonly MelhorEnvioOptions _melhorEnvio;
    private readonly J3ShippingOptions _j3;
    private readonly ILogger<ShippingQuoteService> _logger;

    public ShippingQuoteService(
        SimulatedShippingService simulated,
        IOptions<MelhorEnvioOptions> melhorEnvio,
        IOptions<J3ShippingOptions> j3,
        ILogger<ShippingQuoteService> logger)
    {
        _simulated = simulated;
        _melhorEnvio = melhorEnvio.Value;
        _j3 = j3.Value;
        _logger = logger;
    }

    public (decimal Price, int EstimatedDays) Quote(
        string shippingMethodId,
        string cep,
        string state,
        decimal productsTotalAfterDiscount,
        StoreSettings settings)
    {
        // Integração real ainda bloqueada: faltam credenciais/regras oficiais do cliente.
        // Quando MelhorEnvio/J3 estiverem IsConfigured, chamar providers aqui.
        // Em falha de transportadora: NÃO inventar valor — relançar ValidationException
        // ou cair no simulado apenas se a política do negócio permitir (hoje: sempre simulado).
        if (_melhorEnvio.IsConfigured || _j3.IsConfigured)
        {
            _logger.LogWarning(
                "Credenciais de transportadora detectadas, mas a integração real ainda não foi ativada. Usando cotação simulada.");
        }

        return _simulated.Quote(
            shippingMethodId,
            cep,
            state,
            productsTotalAfterDiscount,
            settings);
    }
}
