using Esotera.Application.DTOs.Addresses;
using Esotera.Application.Exceptions;
using Esotera.Application.Interfaces;
using Esotera.Domain.Entities;
using Esotera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Esotera.Infrastructure.Services;

public class AddressService : IAddressService
{
    private readonly EsoteraDbContext _context;

    public AddressService(EsoteraDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AddressDto>> ListForUserAsync(Guid userId)
    {
        return await _context.Addresses
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsPrimary)
            .ThenByDescending(a => a.CreatedAtUtc)
            .Select(a => MapToDto(a))
            .ToListAsync();
    }

    public async Task<AddressDto?> GetByIdAsync(Guid userId, Guid addressId)
    {
        var address = await _context.Addresses
            .FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == userId);

        return address == null ? null : MapToDto(address);
    }

    public async Task<AddressDto> CreateAsync(Guid userId, CreateAddressRequest request)
    {
        var normalizedCep = request.Cep.Replace("-", "").Trim();

        if (request.IsPrimary)
        {
            var existingPrimary = await _context.Addresses
                .Where(a => a.UserId == userId && a.IsPrimary)
                .ToListAsync();
            
            foreach (var addr in existingPrimary)
                addr.IsPrimary = false;
        }

        var address = new Address
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Cep = normalizedCep,
            Street = request.Street.Trim(),
            Number = request.Number.Trim(),
            Complement = request.Complement?.Trim(),
            Neighborhood = request.Neighborhood.Trim(),
            City = request.City.Trim(),
            State = request.State.ToUpper().Trim(),
            IsPrimary = request.IsPrimary,
            IsResidentialAddress = request.IsResidentialAddress,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Addresses.Add(address);
        await _context.SaveChangesAsync();

        return MapToDto(address);
    }

    public async Task<AddressDto> UpdateAsync(Guid userId, Guid addressId, UpdateAddressRequest request)
    {
        var address = await _context.Addresses
            .FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == userId)
            ?? throw new NotFoundException("Endereço", addressId);

        if (request.Cep != null) address.Cep = request.Cep.Replace("-", "").Trim();
        if (request.Street != null) address.Street = request.Street.Trim();
        if (request.Number != null) address.Number = request.Number.Trim();
        if (request.Complement != null) address.Complement = request.Complement.Trim();
        if (request.Neighborhood != null) address.Neighborhood = request.Neighborhood.Trim();
        if (request.City != null) address.City = request.City.Trim();
        if (request.State != null) address.State = request.State.ToUpper().Trim();

        // Só atualiza quando o cliente envia o campo (não inventar default em legado).
        if (request.IsResidentialAddress.HasValue)
            address.IsResidentialAddress = request.IsResidentialAddress;

        if (request.IsPrimary == true && !address.IsPrimary)
        {
            var existingPrimary = await _context.Addresses
                .Where(a => a.UserId == userId && a.IsPrimary && a.Id != addressId)
                .ToListAsync();

            foreach (var addr in existingPrimary)
                addr.IsPrimary = false;

            address.IsPrimary = true;
        }
        else if (request.IsPrimary == false)
        {
            address.IsPrimary = false;
        }

        await _context.SaveChangesAsync();
        return MapToDto(address);
    }

    public async Task DeleteAsync(Guid userId, Guid addressId)
    {
        var address = await _context.Addresses
            .FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == userId)
            ?? throw new NotFoundException("Endereço", addressId);

        _context.Addresses.Remove(address);
        await _context.SaveChangesAsync();
    }

    public async Task SetPrimaryAsync(Guid userId, Guid addressId)
    {
        var address = await _context.Addresses
            .FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == userId)
            ?? throw new NotFoundException("Endereço", addressId);

        var existingPrimary = await _context.Addresses
            .Where(a => a.UserId == userId && a.IsPrimary && a.Id != addressId)
            .ToListAsync();

        foreach (var addr in existingPrimary)
            addr.IsPrimary = false;

        address.IsPrimary = true;
        await _context.SaveChangesAsync();
    }

    private static AddressDto MapToDto(Address address) => new(
        address.Id,
        address.Cep,
        address.Street,
        address.Number,
        address.Complement,
        address.Neighborhood,
        address.City,
        address.State,
        address.IsPrimary,
        address.IsResidentialAddress
    );
}
