using Esotera.Application.DTOs.Addresses;

namespace Esotera.Application.Interfaces;

public interface IAddressService
{
    Task<IReadOnlyList<AddressDto>> ListForUserAsync(Guid userId);
    Task<AddressDto?> GetByIdAsync(Guid userId, Guid addressId);
    Task<AddressDto> CreateAsync(Guid userId, CreateAddressRequest request);
    Task<AddressDto> UpdateAsync(Guid userId, Guid addressId, UpdateAddressRequest request);
    Task DeleteAsync(Guid userId, Guid addressId);
    Task SetPrimaryAsync(Guid userId, Guid addressId);
}
