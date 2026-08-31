using Esotera.Application.Interfaces;

namespace Esotera.Infrastructure.Services;

/// <summary>Cliente ME calculate fake para testes — sem HTTP real.</summary>
public sealed class FakeMelhorEnvioShipmentClient : IMelhorEnvioShipmentClient
{
    public int CallCount { get; private set; }
    public List<string> AccessTokensUsed { get; } = new();
    public MelhorEnvioCalculateRequest? LastRequest { get; private set; }

    public bool ReturnUnauthenticatedOnce { get; set; }
    public bool AlwaysUnauthenticated { get; set; }
    public bool TimedOut { get; set; }
    public bool NetworkError { get; set; }
    public bool FailOk { get; set; }

    /// <summary>Quando null, usa preços regionais espelhando a simulação antiga.</summary>
    public Func<MelhorEnvioCalculateRequest, IReadOnlyList<MelhorEnvioRawServiceQuote>>? CustomServices { get; set; }

    public Task<MelhorEnvioCalculateOutcome> CalculateAsync(
        MelhorEnvioCalculateRequest request,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        AccessTokensUsed.Add(accessToken);
        LastRequest = request;
        cancellationToken.ThrowIfCancellationRequested();

        if (TimedOut)
            return Task.FromResult(new MelhorEnvioCalculateOutcome { Ok = false, TimedOut = true });

        if (NetworkError)
            return Task.FromResult(new MelhorEnvioCalculateOutcome { Ok = false, NetworkError = true });

        if (AlwaysUnauthenticated || ReturnUnauthenticatedOnce)
        {
            ReturnUnauthenticatedOnce = false;
            return Task.FromResult(new MelhorEnvioCalculateOutcome { Ok = false, Unauthenticated = true });
        }

        if (FailOk)
            return Task.FromResult(new MelhorEnvioCalculateOutcome { Ok = false });

        var services = CustomServices?.Invoke(request) ?? DefaultRegionalServices(request.ToPostalCode);
        return Task.FromResult(new MelhorEnvioCalculateOutcome { Ok = true, Services = services });
    }

    // --- Inserção no carrinho (Fase C1). Nunca compra etiqueta. ---

    public int CartCallCount { get; private set; }
    public MelhorEnvioCartRequest? LastCartRequest { get; private set; }

    /// <summary>Quando null, devolve sucesso com id/protocolo fixos.</summary>
    public Func<MelhorEnvioCartRequest, MelhorEnvioCartOutcome>? CartOutcome { get; set; }

    public const string FakeCartShipmentId = "fake-shipment-0001";
    public const string FakeCartProtocol = "ORD-FAKE-0001";

    public Task<MelhorEnvioCartOutcome> CreateCartItemAsync(
        MelhorEnvioCartRequest request,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        CartCallCount++;
        AccessTokensUsed.Add(accessToken);
        LastCartRequest = request;
        cancellationToken.ThrowIfCancellationRequested();

        var outcome = CartOutcome?.Invoke(request) ?? new MelhorEnvioCartOutcome
        {
            Ok = true,
            ShipmentId = FakeCartShipmentId,
            Protocol = FakeCartProtocol
        };

        return Task.FromResult(outcome);
    }

    public void Reset()
    {
        CallCount = 0;
        AccessTokensUsed.Clear();
        LastRequest = null;
        ReturnUnauthenticatedOnce = false;
        AlwaysUnauthenticated = false;
        TimedOut = false;
        NetworkError = false;
        FailOk = false;
        CustomServices = null;
        CartCallCount = 0;
        LastCartRequest = null;
        CartOutcome = null;
    }

    /// <summary>Preços alinhados à antiga simulação por UF (SP = 18.90 / 28.90).</summary>
    public static IReadOnlyList<MelhorEnvioRawServiceQuote> DefaultRegionalServices(string toPostalCode)
    {
        var digits = new string(toPostalCode.Where(char.IsDigit).ToArray());
        // Heurística simples para testes: CEP SP capital / grande SP → preços SP
        var (eco, exp, ecoDays, expDays) = EstimateByCep(digits);

        return
        [
            new MelhorEnvioRawServiceQuote
            {
                CompanyId = 1,
                CompanyName = "Correios",
                ServiceId = 1,
                ServiceName = "PAC",
                Price = eco,
                DeliveryTime = ecoDays
            },
            new MelhorEnvioRawServiceQuote
            {
                CompanyId = 1,
                CompanyName = "Correios",
                ServiceId = 2,
                ServiceName = "SEDEX",
                Price = exp,
                DeliveryTime = expDays
            }
        ];
    }

    private static (decimal Eco, decimal Exp, int EcoDays, int ExpDays) EstimateByCep(string digits)
    {
        if (digits.Length != 8)
            return (39.90m, 59.90m, 10, 5);

        // Faixas aproximadas usadas nos testes (SP / Sul / outros)
        var prefix = int.Parse(digits[..2]);
        if (prefix is >= 1 and <= 19)
            return (18.90m, 28.90m, 5, 2); // SP
        if (prefix is >= 20 and <= 28 or >= 30 and <= 39 or >= 29 and <= 29)
            return (24.90m, 36.90m, 7, 3); // RJ/ES/MG approx
        if (prefix is >= 80 and <= 89 or >= 90 and <= 99)
            return (29.90m, 42.90m, 7, 3); // Sul
        return (39.90m, 59.90m, 10, 5);
    }
}
