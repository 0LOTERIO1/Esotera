using System.Text.Json.Serialization;

namespace Esotera.Application.DTOs.J3;

/// <summary>Comando local: Order + NF-e parseada → ImportOrderByAccessKeyInput.</summary>
public sealed class J3ImportOrderByAccessKeyCommand
{
    public required Guid LocalOrderId { get; init; }
    public required J3ImportOrderByAccessKeyInputDto Input { get; init; }
}

/// <summary>ImportOrderByAccessKeyInput (schema J3).</summary>
public sealed class J3ImportOrderByAccessKeyInputDto
{
    public required J3NfeDataInputDto Order { get; init; }
    public string? SellerId { get; init; }
    public required string SellerInformationId { get; init; }
}

/// <summary>NfeDataInput.</summary>
public sealed class J3NfeDataInputDto
{
    public required string ChNFe { get; init; }
    public required J3NfeAddressInputDto DestEnder { get; init; }
    public required string DestXNome { get; init; }
    public required J3NfeAddressInputDto EmitEnder { get; init; }
    public required string EmitXFant { get; init; }
    public required string EmitXNome { get; init; }
    public required IReadOnlyList<J3NfeDetInputDto> Items { get; init; }
}

/// <summary>NfeAddressInput. Schema usa CEP (maiúsculo); fone/nro/xLgr/xCpl.</summary>
public sealed class J3NfeAddressInputDto
{
    [JsonPropertyName("CEP")]
    public required string Cep { get; init; }
    public required string Fone { get; init; }
    public required string Nro { get; init; }
    public required string XLgr { get; init; }
    public string? XCpl { get; init; }
}

/// <summary>NfeDetInput. qCom Int!; vUnCom Float!; xProd String!.</summary>
public sealed class J3NfeDetInputDto
{
    public required int QCom { get; init; }
    public required double VUnCom { get; init; }
    public required string XProd { get; init; }
}
