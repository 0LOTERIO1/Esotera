namespace Esotera.Domain.Enums;

public static class PaymentMethod
{
    public const string Pix = "pix";
    public const string Card = "card";
    public const string Boleto = "boleto";

    public static readonly string[] All = [Pix, Card, Boleto];

    public static bool IsValid(string method) => All.Contains(method);
}
