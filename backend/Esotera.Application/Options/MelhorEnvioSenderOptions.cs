namespace Esotera.Application.Options;

/// <summary>
/// Remetente (objeto `from`) usado na inserção de frete no carrinho do Melhor Envio.
/// Env flat: MELHOR_ENVIO_FROM_*. O CEP de origem continua vindo de StoreSettings
/// (fonte única já usada na cotação) — aqui só entram os dados que não existiam.
/// Nada aqui é segredo, mas nada aqui deve ser logado sem necessidade.
/// </summary>
public class MelhorEnvioSenderOptions
{
    public const string SectionName = "MelhorEnvioSender";

    /// <summary>Razão social / nome do remetente. Env: MELHOR_ENVIO_FROM_NAME.</summary>
    public string? Name { get; set; }

    /// <summary>E-mail do remetente. Env: MELHOR_ENVIO_FROM_EMAIL.</summary>
    public string? Email { get; set; }

    /// <summary>Telefone só dígitos. Env: MELHOR_ENVIO_FROM_PHONE.</summary>
    public string? Phone { get; set; }

    /// <summary>CNPJ do remetente, só dígitos. Env: MELHOR_ENVIO_FROM_COMPANY_DOCUMENT.</summary>
    public string? CompanyDocument { get; set; }

    /// <summary>
    /// Inscrição estadual. Obrigatória para envio comercial (com NF-e).
    /// Env: MELHOR_ENVIO_FROM_STATE_REGISTER.
    /// </summary>
    public string? StateRegister { get; set; }

    /// <summary>CNAE principal, só dígitos. Env: MELHOR_ENVIO_FROM_ECONOMIC_ACTIVITY_CODE.</summary>
    public string? EconomicActivityCode { get; set; }

    /// <summary>Logradouro. Env: MELHOR_ENVIO_FROM_ADDRESS.</summary>
    public string? Address { get; set; }

    /// <summary>Número. Env: MELHOR_ENVIO_FROM_NUMBER.</summary>
    public string? Number { get; set; }

    /// <summary>Complemento (opcional). Env: MELHOR_ENVIO_FROM_COMPLEMENT.</summary>
    public string? Complement { get; set; }

    /// <summary>Bairro. Env: MELHOR_ENVIO_FROM_DISTRICT.</summary>
    public string? District { get; set; }

    /// <summary>Cidade. Env: MELHOR_ENVIO_FROM_CITY.</summary>
    public string? City { get; set; }

    /// <summary>UF com 2 letras. Env: MELHOR_ENVIO_FROM_STATE_ABBR.</summary>
    public string? StateAbbr { get; set; }

    /// <summary>
    /// Identificação da loja na etiqueta/painel. Env: MELHOR_ENVIO_FROM_PLATFORM.
    /// Vazio = o Melhor Envio usa o nome do app.
    /// </summary>
    public string? Platform { get; set; }

    /// <summary>
    /// Todos os campos obrigatórios do `from` para envio COMERCIAL preenchidos.
    /// StateRegister entra aqui de propósito: sem inscrição estadual a API recusa
    /// o envio comercial com NF-e.
    /// </summary>
    public bool IsCompleteForCommercialShipping =>
        !string.IsNullOrWhiteSpace(Name)
        && !string.IsNullOrWhiteSpace(Email)
        && !string.IsNullOrWhiteSpace(Phone)
        && !string.IsNullOrWhiteSpace(CompanyDocument)
        && !string.IsNullOrWhiteSpace(StateRegister)
        && !string.IsNullOrWhiteSpace(EconomicActivityCode)
        && !string.IsNullOrWhiteSpace(Address)
        && !string.IsNullOrWhiteSpace(Number)
        && !string.IsNullOrWhiteSpace(District)
        && !string.IsNullOrWhiteSpace(City)
        && !string.IsNullOrWhiteSpace(StateAbbr);

    /// <summary>Nomes dos campos ausentes — para mensagem de diagnóstico no Admin.</summary>
    public IReadOnlyList<string> MissingCommercialFields()
    {
        var missing = new List<string>();
        void Check(string label, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                missing.Add(label);
        }

        Check("nome", Name);
        Check("e-mail", Email);
        Check("telefone", Phone);
        Check("CNPJ", CompanyDocument);
        Check("inscrição estadual", StateRegister);
        Check("CNAE", EconomicActivityCode);
        Check("logradouro", Address);
        Check("número", Number);
        Check("bairro", District);
        Check("cidade", City);
        Check("UF", StateAbbr);
        return missing;
    }
}
