namespace Esotera.Application.Options;

/// <summary>
/// Preparação Melhor Envio — NÃO integrado até o cliente confirmar credenciais e regras.
/// Variáveis: MELHOR_ENVIO_CLIENT_ID, MELHOR_ENVIO_CLIENT_SECRET, MELHOR_ENVIO_ENABLED.
/// </summary>
public class MelhorEnvioOptions
{
    public const string SectionName = "MelhorEnvio";

    public bool Enabled { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    /// <summary>Sandbox vs produção quando a API for ligada.</summary>
    public string Environment { get; set; } = "sandbox";

    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret);
}

/// <summary>
/// Preparação J3 — NÃO integrado até cobertura oficial de CEPs + token/API.
/// Variáveis: J3_API_URL, J3_API_TOKEN, J3_ENABLED.
/// </summary>
public class J3ShippingOptions
{
    public const string SectionName = "J3";

    public bool Enabled { get; set; }
    public string? ApiUrl { get; set; }
    public string? ApiToken { get; set; }

    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(ApiUrl)
        && !string.IsNullOrWhiteSpace(ApiToken);
}
