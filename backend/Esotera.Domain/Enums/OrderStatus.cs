namespace Esotera.Domain.Enums;

public static class OrderStatus
{
    public const string AwaitingPayment = "awaiting_payment";
    public const string PaymentApproved = "payment_approved";
    public const string Preparing = "preparing";
    public const string Shipped = "shipped";
    public const string Delivered = "delivered";
    public const string Cancelled = "cancelled";

    public static readonly string[] All =
    [
        AwaitingPayment,
        PaymentApproved,
        Preparing,
        Shipped,
        Delivered,
        Cancelled
    ];

    public static bool IsValid(string status) => All.Contains(status);
}
