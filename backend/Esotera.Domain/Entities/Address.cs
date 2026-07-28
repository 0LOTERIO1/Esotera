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
    public DateTime CreatedAtUtc { get; set; }

    public User User { get; set; } = null!;
}
