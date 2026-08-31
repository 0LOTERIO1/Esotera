namespace Esotera.Application.Interfaces;

/// <summary>
/// Payload de POST /api/v2/me/cart. Apenas inserção no carrinho — NÃO compra,
/// NÃO gera e NÃO imprime etiqueta.
/// </summary>
public sealed record MelhorEnvioCartRequest(
    int Service,
    MelhorEnvioCartParty From,
    MelhorEnvioCartParty To,
    IReadOnlyList<MelhorEnvioCartProduct> Products,
    IReadOnlyList<MelhorEnvioCartVolume> Volumes,
    MelhorEnvioCartOptions Options
);

public sealed record MelhorEnvioCartParty(
    string Name,
    string? Email,
    string? Phone,
    /// <summary>CPF (pessoa física). Vazio quando se usa CompanyDocument.</summary>
    string? Document,
    /// <summary>CNPJ (pessoa jurídica).</summary>
    string? CompanyDocument,
    /// <summary>Inscrição estadual. "ISENTO" para pessoa física.</summary>
    string? StateRegister,
    string? EconomicActivityCode,
    string Address,
    string? Complement,
    string Number,
    string District,
    string City,
    string PostalCode,
    string StateAbbr,
    string? CountryId = null
);

public sealed record MelhorEnvioCartProduct(
    string Name,
    int Quantity,
    decimal UnitaryValue
);

public sealed record MelhorEnvioCartVolume(
    decimal HeightCm,
    decimal WidthCm,
    decimal LengthCm,
    /// <summary>Peso em kg.</summary>
    decimal WeightKg
);

public sealed record MelhorEnvioCartOptions(
    decimal InsuranceValue,
    string? Platform,
    string? Reminder,
    /// <summary>Chave da NF-e (44 dígitos). Obrigatória em envio comercial.</summary>
    string? InvoiceKey,
    string? OrderTag,
    bool Receipt = false,
    bool OwnHand = false,
    bool Reverse = false,
    /// <summary>Sempre false: nossos envios são comerciais, com NF-e.</summary>
    bool NonCommercial = false
);

/// <summary>Resultado da inserção no carrinho. Nunca expor ao cliente da loja.</summary>
public sealed class MelhorEnvioCartOutcome
{
    public bool Ok { get; init; }

    /// <summary>ID do envio no Melhor Envio (uuid).</summary>
    public string? ShipmentId { get; init; }

    /// <summary>Protocolo (ex.: ORD-20220397305).</summary>
    public string? Protocol { get; init; }

    /// <summary>401 — token inválido/expirado.</summary>
    public bool Unauthenticated { get; init; }

    /// <summary>403 — token válido sem o escopo cart-write.</summary>
    public bool Forbidden { get; init; }

    /// <summary>4xx de validação: dados recusados pelo Melhor Envio.</summary>
    public bool ValidationRejected { get; init; }

    public bool TimedOut { get; init; }
    public bool NetworkError { get; init; }

    /// <summary>Código curto sanitizado.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>Mensagem operacional sanitizada (sem payload/token).</summary>
    public string? ErrorMessage { get; init; }
}
