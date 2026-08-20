namespace Esotera.Application.Common;

/// <summary>UF → nome completo (exigência do template UpSeller).</summary>
public static class BrazilianStateNames
{
    private static readonly Dictionary<string, string> ByUf = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AC"] = "Acre",
        ["AL"] = "Alagoas",
        ["AP"] = "Amapá",
        ["AM"] = "Amazonas",
        ["BA"] = "Bahia",
        ["CE"] = "Ceará",
        ["DF"] = "Distrito Federal",
        ["ES"] = "Espírito Santo",
        ["GO"] = "Goiás",
        ["MA"] = "Maranhão",
        ["MT"] = "Mato Grosso",
        ["MS"] = "Mato Grosso do Sul",
        ["MG"] = "Minas Gerais",
        ["PA"] = "Pará",
        ["PB"] = "Paraíba",
        ["PR"] = "Paraná",
        ["PE"] = "Pernambuco",
        ["PI"] = "Piauí",
        ["RJ"] = "Rio de Janeiro",
        ["RN"] = "Rio Grande do Norte",
        ["RS"] = "Rio Grande do Sul",
        ["RO"] = "Rondônia",
        ["RR"] = "Roraima",
        ["SC"] = "Santa Catarina",
        ["SP"] = "São Paulo",
        ["SE"] = "Sergipe",
        ["TO"] = "Tocantins"
    };

    /// <summary>
    /// Converte UF (ex. SP) para nome completo. Se já for nome conhecido, devolve-o.
    /// null/vazio → null. UF desconhecida → ValidationException no caller.
    /// </summary>
    public static string? TryGetFullName(string? stateOrUf)
    {
        if (string.IsNullOrWhiteSpace(stateOrUf))
            return null;

        var key = stateOrUf.Trim();
        if (ByUf.TryGetValue(key, out var name))
            return name;

        foreach (var pair in ByUf)
        {
            if (string.Equals(pair.Value, key, StringComparison.OrdinalIgnoreCase))
                return pair.Value;
        }

        return null;
    }
}
