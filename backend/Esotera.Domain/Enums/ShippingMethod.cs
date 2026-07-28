namespace Esotera.Domain.Enums;

public static class ShippingMethod
{
    public const string J3 = "j3";
    public const string MelhorEconomico = "melhor_economico";
    public const string MelhorExpresso = "melhor_expresso";

    public static readonly string[] All = [J3, MelhorEconomico, MelhorExpresso];

    public static bool IsValid(string method) => All.Contains(method);

    public static string GetDisplayName(string method) => method switch
    {
        J3 => "J3 Entregas",
        MelhorEconomico => "Melhor Envio - Econômico",
        MelhorExpresso => "Melhor Envio - Expresso",
        _ => method
    };

    public static string GetProvider(string method) => method switch
    {
        J3 => "J3",
        MelhorEconomico => "Melhor Envio",
        MelhorExpresso => "Melhor Envio",
        _ => "Desconhecido"
    };

    public static int GetEstimatedDays(string method, string region) => method switch
    {
        J3 => 1,
        MelhorEconomico => region switch
        {
            "Sudeste" => 5,
            "Sul" => 7,
            _ => 10
        },
        MelhorExpresso => region switch
        {
            "Sudeste" => 2,
            "Sul" => 3,
            _ => 5
        },
        _ => 10
    };
}
