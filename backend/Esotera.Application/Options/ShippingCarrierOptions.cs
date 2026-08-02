namespace Esotera.Application.Options;

/// <summary>
/// Melhor Envio — OAuth Sandbox + preparação de cotação.
/// Variáveis: MELHOR_ENVIO_*, INTEGRATIONS_ENCRYPTION_KEY (cifra de tokens).
/// </summary>
public class MelhorEnvioOptions
{
    public const string SectionName = "MelhorEnvio";

    public const string SandboxAuthorizeUrl = "https://sandbox.melhorenvio.com.br/oauth/authorize";
    public const string SandboxTokenUrl = "https://sandbox.melhorenvio.com.br/oauth/token";
    public const string RequiredScope = "shipping-calculate";
    public const int AccessTokenLifetimeDays = 30;
    public const int RefreshTokenLifetimeDays = 45;
    public const int OAuthStateLifetimeMinutes = 10;
    /// <summary>Margem para refresh lazy antes do access token expirar.</summary>
    public const int RefreshSkewHours = 72;

    public bool Enabled { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    /// <summary>Sandbox vs produção quando a API for ligada.</summary>
    public string Environment { get; set; } = "sandbox";
    /// <summary>Callback OAuth registrado no app Melhor Envio (URL da API).</summary>
    public string? RedirectUri { get; set; }
    /// <summary>User-Agent obrigatório em todas as requests à API Melhor Envio.</summary>
    public string? UserAgent { get; set; }
    /// <summary>Base do frontend para redirect pós-callback (ex.: https://esotera.vercel.app).</summary>
    public string? FrontendBaseUrl { get; set; }

    public bool IsSandbox =>
        string.Equals(Environment?.Trim(), "sandbox", StringComparison.OrdinalIgnoreCase);

    /// <summary>Credenciais mínimas para cotação futura (legado).</summary>
    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret);

    /// <summary>Pronto para fluxo OAuth Sandbox (authorize / token / refresh).</summary>
    public bool IsOAuthConfigured =>
        Enabled
        && IsSandbox
        && !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret)
        && !string.IsNullOrWhiteSpace(RedirectUri)
        && !string.IsNullOrWhiteSpace(UserAgent)
        && !string.IsNullOrWhiteSpace(FrontendBaseUrl);
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
