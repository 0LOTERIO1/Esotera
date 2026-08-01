using Esotera.Application.DTOs.Newsletter;

namespace Esotera.Application.Interfaces;

public interface INewsletterService
{
    Task<NewsletterMessageResponse> SubscribeAsync(
        SubscribeNewsletterRequest request,
        CancellationToken cancellationToken = default);

    Task<NewsletterMessageResponse> UnsubscribeAsync(
        string token,
        CancellationToken cancellationToken = default);

    Task<NewsletterAdminListResponse> AdminListAsync(
        string? search,
        bool? isActive,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default);

    Task<string> AdminExportCsvAsync(
        string? search,
        bool? isActive,
        CancellationToken cancellationToken = default);
}
