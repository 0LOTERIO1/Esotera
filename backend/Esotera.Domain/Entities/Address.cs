namespace Esotera.Domain.Entities;

public class Address
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Cep { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public string? Complement { get; set; }
    public string Neighborhood { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }

    /// <summary>
    /// Residencial (true) / Comercial (false). Null = legado sem captura explícita.
    /// Não inventar default — J3 futuro exige valor explícito; PAC/SEDEX aceitam null.
    /// </summary>
    public bool? IsResidentialAddress { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public User User { get; set; } = null!;
}
