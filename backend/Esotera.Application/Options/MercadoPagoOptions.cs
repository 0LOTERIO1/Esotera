namespace Esotera.Application.Options;

/// <summary>Ambiente tipado do Mercado Pago — nunca inferir pelo texto do Access Token.</summary>
public enum MercadoPagoEnvironmentKind
{
    Test = 0,
    Production = 1
}

public class MercadoPagoOptions
{
    public const string SectionName = "MercadoPago";

    public const string SandboxPayerEmail = "test_user_br@testuser.com";
    public const string SandboxPayerFirstName = "APRO";
    public const string SandboxExternalReferencePrefix = "teste_esotera_pix_50_";
    public const string CommercialSandboxBlockedMessage =
        "O Mercado Pago está em ambiente de teste. Use o teste Pix controlado de R$ 50,00 ou ative as credenciais de produção para processar o valor real deste pedido.";

    /// <summary>Access Token — SOMENTE backend. Nunca NEXT_PUBLIC_.</summary>
    public string? AccessToken { get; set; }

    /// <summary>Fonte da config do token (nome da chave) — nunca o valor.</summary>
    public string? AccessTokenSource { get; set; }

    public string? WebhookSecret { get; set; }

    /// <summary>Valor bruto legado (test/production) — preferir <see cref="EnvironmentKind"/>.</summary>
    public string Environment { get; set; } = "Test";

    public MercadoPagoEnvironmentKind EnvironmentKind { get; set; } = MercadoPagoEnvironmentKind.Test;

    /// <summary>URL pública do webhook.</summary>
    public string? NotificationUrl { get; set; }

    public string? PublicApiBaseUrl { get; set; }

    /// <summary>Habilita endpoint isolado de Pix R$ 50 em Test. Ignorado em Production.</summary>
    public bool SandboxPixEnabled { get; set; } = true;

    /// <summary>Valor oficial do teste Pix em sandbox (não altera pedidos comerciais).</summary>
    public decimal SandboxPixAmount { get; set; } = 50.00m;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(AccessToken);

    public bool IsTestEnvironment => EnvironmentKind == MercadoPagoEnvironmentKind.Test;

    public bool IsProductionEnvironment => EnvironmentKind == MercadoPagoEnvironmentKind.Production;

    public bool CanUseSandboxPixTest =>
        IsTestEnvironment && SandboxPixEnabled && IsConfigured;

    public static MercadoPagoEnvironmentKind ParseEnvironmentKind(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return MercadoPagoEnvironmentKind.Test;

        var v = raw.Trim();
        if (v.Equals("Production", StringComparison.OrdinalIgnoreCase)
            || v.Equals("Prod", StringComparison.OrdinalIgnoreCase)
            || v.Equals("Live", StringComparison.OrdinalIgnoreCase))
            return MercadoPagoEnvironmentKind.Production;

        return MercadoPagoEnvironmentKind.Test;
    }

    public bool IsSandboxTestExternalReference(string? externalReference) =>
        !string.IsNullOrWhiteSpace(externalReference)
        && externalReference.StartsWith(SandboxExternalReferencePrefix, StringComparison.Ordinal);

    public string ResolveNotificationUrl()
    {
        if (!string.IsNullOrWhiteSpace(NotificationUrl))
            return NotificationUrl.Trim();
        var baseUrl = (PublicApiBaseUrl ?? "").Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            return string.Empty;
        return $"{baseUrl}/api/webhooks/mercadopago";
    }
}
