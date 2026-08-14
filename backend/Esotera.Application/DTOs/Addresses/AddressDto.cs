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
    bool IsPrimary,
    bool? IsResidentialAddress
);

public record CreateAddressRequest(
    string Cep,
    string Street,
    string Number,
    string? Complement,
    string Neighborhood,
    string City,
    string State,
    bool IsPrimary = false,
    bool? IsResidentialAddress = null
);

public record UpdateAddressRequest(
    string? Cep,
    string? Street,
    string? Number,
    string? Complement,
    string? Neighborhood,
    string? City,
    string? State,
    bool? IsPrimary,
    bool? IsResidentialAddress = null
);
