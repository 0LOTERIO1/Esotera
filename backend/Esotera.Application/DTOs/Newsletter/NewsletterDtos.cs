namespace Esotera.Application.DTOs.Newsletter;

public record SubscribeNewsletterRequest(
    string Email,
    bool Consent
);

public record NewsletterMessageResponse(
    string Message,
    bool EmailSent = false
);

public record NewsletterSubscriptionDto(
    Guid Id,
    string Email,
    bool IsActive,
    DateTime ConsentAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? UnsubscribedAtUtc
);

public record NewsletterAdminListResponse(
    NewsletterSubscriptionDto[] Items,
    int Total
);
