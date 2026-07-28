namespace Esotera.Application.DTOs.Addresses;

public record AddressDto(
    Guid Id,
    string Cep,
    string Street,
    string Number,
    string? Complement,
    string Neighborhood,
    string City,
    string State,
    bool IsPrimary
);

public record CreateAddressRequest(
    string Cep,
    string Street,
    string Number,
    string? Complement,
    string Neighborhood,
    string City,
    string State,
    bool IsPrimary = false
);

public record UpdateAddressRequest(
    string? Cep,
    string? Street,
    string? Number,
    string? Complement,
    string? Neighborhood,
    string? City,
    string? State,
    bool? IsPrimary
);
