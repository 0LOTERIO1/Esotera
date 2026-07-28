using Esotera.Application.DTOs.Newsletter;

namespace Esotera.Application.Interfaces;

public interface INewsletterService
{
    Task<NewsletterMessageResponse> SubscribeAsync(SubscribeNewsletterRequest request);
    Task<NewsletterMessageResponse> UnsubscribeAsync(string token);
    Task<NewsletterAdminListResponse> AdminListAsync(string? search, bool? isActive, int skip = 0, int take = 100);
    Task<string> AdminExportCsvAsync(string? search, bool? isActive);
}
