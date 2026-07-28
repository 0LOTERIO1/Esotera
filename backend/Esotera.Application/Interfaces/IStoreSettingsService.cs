using Esotera.Application.DTOs.Settings;

namespace Esotera.Application.Interfaces;

public interface IStoreSettingsService
{
    Task<PublicStoreSettingsDto> GetPublicAsync();
    Task<AdminStoreSettingsDto> GetAdminAsync();
    Task<AdminStoreSettingsDto> UpdateAsync(UpdateStoreSettingsRequest request);
}
