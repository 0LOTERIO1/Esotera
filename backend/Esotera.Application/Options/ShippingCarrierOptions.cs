namespace Esotera.Application.Options;

/// <summary>
/// Melhor Envio — OAuth (sandbox ou produção) + cotação.
/// Variáveis: MELHOR_ENVIO_*, INTEGRATIONS_ENCRYPTION_KEY (cifra de tokens).
/// Auth somente OAuth: tokens ficam cifrados no banco, nunca em env.
/// </summary>
public class MelhorEnvioOptions
{
    public const string SectionName = "MelhorEnvio";

    public const string SandboxBaseUrl = "https://sandbox.melhorenvio.com.br";
    public const string ProductionBaseUrl = "https://melhorenvio.com.br";

    public const string SandboxAuthorizeUrl = $"{SandboxBaseUrl}/oauth/authorize";
    public const string SandboxTokenUrl = $"{SandboxBaseUrl}/oauth/token";
    public const string SandboxCalculateUrl = $"{SandboxBaseUrl}/api/v2/me/shipment/calculate";
    public const string RequiredScope = "shipping-calculate";
    public const int AccessTokenLifetimeDays = 30;
    public const int RefreshTokenLifetimeDays = 45;
    public const int OAuthStateLifetimeMinutes = 10;
    /// <summary>Margem para refresh lazy antes do access token expirar.</summary>
    public const int RefreshSkewHours = 72;

    public bool Enabled { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    /// <summary>Sandbox vs produção. Env: MELHOR_ENVIO_ENVIRONMENT.</summary>
    public string Environment { get; set; } = "sandbox";

    /// <summary>
    /// Base da API. Env: MELHOR_ENVIO_BASE_URL. Vazio = derivada do <see cref="Environment"/>.
    /// </summary>
    public string? BaseUrl { get; set; }
    /// <summary>Callback OAuth registrado no app Melhor Envio (URL da API).</summary>
    public string? RedirectUri { get; set; }
    /// <summary>User-Agent obrigatório em todas as requests à API Melhor Envio.</summary>
    public string? UserAgent { get; set; }
    /// <summary>Base do frontend para redirect pós-callback (ex.: https://esotera.vercel.app).</summary>
    public string? FrontendBaseUrl { get; set; }

    public bool IsSandbox =>
        !string.Equals(Environment?.Trim(), "production", StringComparison.OrdinalIgnoreCase);

    /// <summary>Rótulo canônico do ambiente — carimbado na conexão salva.</summary>
    public string NormalizedEnvironment => IsSandbox ? "sandbox" : "production";

    /// <summary>Base efetiva: BaseUrl explícita ou o default do ambiente.</summary>
    public string ResolvedBaseUrl =>
        string.IsNullOrWhiteSpace(BaseUrl)
            ? (IsSandbox ? SandboxBaseUrl : ProductionBaseUrl)
            : BaseUrl.Trim().TrimEnd('/');

    public bool HasValidBaseUrl =>
        Uri.TryCreate(ResolvedBaseUrl, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public string AuthorizeUrl => $"{ResolvedBaseUrl}/oauth/authorize";
    public string TokenUrl => $"{ResolvedBaseUrl}/oauth/token";
    public string CalculateUrl => $"{ResolvedBaseUrl}/api/v2/me/shipment/calculate";

    /// <summary>Credenciais mínimas para cotação futura (legado).</summary>
    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret);

    /// <summary>
    /// Pronto para o fluxo OAuth (authorize / token / refresh) em qualquer ambiente.
    /// Produção exige a mesma configuração de sandbox — o ambiente não é mais um gate.
    /// </summary>
    public bool IsOAuthConfigured =>
        Enabled
        && HasValidBaseUrl
        && !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret)
        && !string.IsNullOrWhiteSpace(RedirectUri)
        && !string.IsNullOrWhiteSpace(UserAgent)
        && !string.IsNullOrWhiteSpace(FrontendBaseUrl);
}

/// <summary>
/// J3 Flex — options + gate + cliente GraphQL read-only (Passo 3: coverage no quote/CreateOrder)
/// + flag de fulfillment local (Passo 4.1 — sem mutations).
/// Env flat oficiais ONLY: J3_ENABLED, J3_FULFILLMENT_ENABLED, J3_GRAPHQL_URL, J3_TOKEN,
/// J3_COMPANY_GROUP_CODE, J3_SELLER_ID, J3_SELLER_INFORMATION_ID, J3_ORIGIN_ZIP,
/// J3_STANDARD_PRICE_CENTS, J3_TIMEOUT_SECONDS, J3_PROCESSING_STALE_MINUTES, J3_ECOMMERCE, J3_ORDER_PICKUP_TYPE,
/// J3_PACKAGE_IS_FRAGILE, J3_PACKAGE_IS_VALUABLE.
/// Também aceita section bind J3:* / J3__* (ex.: J3__Enabled) via GetSection + override flat.
/// J3_ENABLED: disponibilidade/quote J3 para NOVOS pedidos (CreateOrder/cotação).
/// J3_FULFILLMENT_ENABLED: processador futuro pode executar mutations J3 (claim/createTmsOrder).
/// Pending local: payment_approved + j3, independente das flags — não perder obrigação se fulfillment estiver off.
/// Enabled=false (default): cotação/pedido não expõem nem aceitam J3; config incompleta OK no startup.
/// FulfillmentEnabled=false (default): nenhuma mutation; config incompleta OK no startup.
/// Futuro (mutations): exigem Enabled AND FulfillmentEnabled.
/// Enabled=true + HasValidRealQuoteConfig → coverage real via IJ3Client (sem simulação de CEP/cutoff).
/// Preço da opção = StandardPriceCents/100 — nunca StoreSettings.J3Price, nunca 1299 implícito.
/// Sem ValidateOnStart para J3.
/// </summary>
public class J3ShippingOptions
{
    public const string SectionName = "J3";

    /// <summary>Default false — desativa J3 para clientes reais.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Default false — habilita mutation importOrderByAccessKey (recovery admin controlado).
    /// Independente de J3_FULFILLMENT_ENABLED (createTmsOrders).
    /// Env: J3_IMPORT_BY_ACCESS_KEY_ENABLED.
    /// </summary>
    public bool ImportByAccessKeyEnabled { get; set; }

    /// <summary>
    /// Default false — desativa PROCESSAMENTO/MUTATION J3 (claim, createTmsOrder futuro).
    /// Não impede registrar J3Fulfillment Pending após pagamento.
    /// Env: J3_FULFILLMENT_ENABLED. Processor/mutations de fulfillment usam esta flag
    /// (não exigem J3_ENABLED — pedidos J3 já pagos).
    /// </summary>
    public bool FulfillmentEnabled { get; set; }

    /// <summary>Endpoint GraphQL J3 Flex. Env: J3_GRAPHQL_URL.</summary>
    public string? GraphQlUrl { get; set; }

    /// <summary>Bearer/token J3 estático (legado). Nunca logar. Env: J3_TOKEN.
    /// Coverage continua usando este token. Mutations Seller preferem login quando configurado.</summary>
    public string? Token { get; set; }

    /// <summary>Email do login Seller (portal). Env: J3_LOGIN_EMAIL. Nunca logar valor.</summary>
    public string? LoginEmail { get; set; }

    /// <summary>Senha do login Seller. Env: J3_LOGIN_PASSWORD. Somente env secret. Nunca logar.</summary>
    public string? LoginPassword { get; set; }

    /// <summary>
    /// URL do login REST. Default portal oficial.
    /// Env: J3_LOGIN_URL.
    /// </summary>
    public string LoginUrl { get; set; } = "https://app.j3tms.com.br/api/auth/login";

    /// <summary>Minutos antes do exp JWT para renovar. Env: J3_AUTH_RENEW_SKEW_MINUTES. Default 5.</summary>
    public int AuthRenewSkewMinutes { get; set; } = 5;

    /// <summary>Código do grupo de empresa. Default comercial não-secreto.</summary>
    public string CompanyGroupCode { get; set; } = "J3";

    /// <summary>Seller ID (string). Vazio por default — não hardcode em código. Uso futuro.</summary>
    public string SellerId { get; set; } = string.Empty;

    /// <summary>Seller information ID (string). Vazio por default — uso futuro read/write.</summary>
    public string SellerInformationId { get; set; } = string.Empty;

    /// <summary>CEP de origem opcional. Env: J3_ORIGIN_ZIP.</summary>
    public string? OriginZip { get; set; }

    /// <summary>
    /// Telefone corporativo do emitente fiscal (fallback para importOrderByAccessKey / emitEnder.fone).
    /// Ordem: XML enderEmit/fone → este valor (só dígitos) → fail-closed.
    /// Env: J3_EMITTER_PHONE. Nunca usar CustomerPhone / seller / Users.
    /// </summary>
    public string? EmitterPhone { get; set; }

    /// <summary>
    /// Preço padrão J3 em centavos (fonte única do preço da opção J3).
    /// Default 0 = ausente/não configurado (detectável). Valor comercial é configurado externamente
    /// (env / appsettings) — sem default comercial no código. Exigir &gt; 0 quando Enabled=true.
    /// </summary>
    public int StandardPriceCents { get; set; }

    /// <summary>Timeout HTTP futuro (segundos). Env: J3_TIMEOUT_SECONDS.</summary>
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// Janela diagnóstica (minutos) para Processing possivelmente preso. Default 15.
    /// Env: J3_PROCESSING_STALE_MINUTES. Não reprocessa nem chama J3.
    /// </summary>
    public int ProcessingStaleMinutes { get; set; } = 15;

    /// <summary>
    /// Estado do formulário portal (Standalone). NÃO é enviado em createTmsOrders Avulso (Passo 4.2B):
    /// o bundle oficial não inclui ecommerce no input. Env: J3_ECOMMERCE.
    /// </summary>
    public string Ecommerce { get; set; } = "Standalone";

    /// <summary>
    /// Tipo de coleta (ex.: Standard). Env: J3_ORDER_PICKUP_TYPE. Default Standard.
    /// </summary>
    public string OrderPickupType { get; set; } = "Standard";

    /// <summary>
    /// Schema J3 exige Boolean em package.isFragile.
    /// Default explícito Esotera: false até existir regra comercial. Env: J3_PACKAGE_IS_FRAGILE.
    /// Não é regra oficial J3.
    /// </summary>
    public bool PackageIsFragile { get; set; }

    /// <summary>
    /// Schema J3 exige Boolean em package.isValuable.
    /// Default explícito Esotera: false até existir regra comercial. Env: J3_PACKAGE_IS_VALUABLE.
    /// Não é regra oficial J3.
    /// </summary>
    public bool PackageIsValuable { get; set; }

    /// <summary>Login Seller configurado (email + password).</summary>
    public bool HasSellerLoginCredentials =>
        !string.IsNullOrWhiteSpace(LoginEmail)
        && !string.IsNullOrWhiteSpace(LoginPassword);

    /// <summary>URL de login absoluta válida.</summary>
    public bool HasValidLoginUrl =>
        !string.IsNullOrWhiteSpace(LoginUrl)
        && Uri.TryCreate(LoginUrl.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>
    /// Bearer disponível para mutations Seller: login credentials OU token estático legado.
    /// </summary>
    public bool HasSellerBearerSource =>
        HasSellerLoginCredentials || !string.IsNullOrWhiteSpace(Token);

    /// <summary>Preço em centavos configurado e válido (&gt; 0). Ausência/0/negativo = inválido.</summary>
    public bool HasValidStandardPriceCents => StandardPriceCents > 0;

    /// <summary>Preço em reais a partir de <see cref="StandardPriceCents"/> (só usar se HasValidStandardPriceCents).</summary>
    public decimal StandardPriceReais => StandardPriceCents / 100m;

    /// <summary>
    /// URL GraphQL absoluta válida (sem chamar a rede).
    /// </summary>
    public bool HasValidGraphQlUrl =>
        !string.IsNullOrWhiteSpace(GraphQlUrl)
        && Uri.TryCreate(GraphQlUrl.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>
    /// Config mínima para oferecer J3 real no quote/CreateOrder (além de Enabled).
    /// Seller IDs NÃO são exigidos nesta fase.
    /// </summary>
    public bool HasValidRealQuoteConfig =>
        HasValidGraphQlUrl
        && !string.IsNullOrWhiteSpace(Token)
        && !string.IsNullOrWhiteSpace(CompanyGroupCode)
        && HasValidStandardPriceCents;

    /// <summary>
    /// Pronto para invocar o cliente HTTP: Enabled + URL + Token.
    /// Cotação real usa <see cref="HasValidRealQuoteConfig"/> (também preço e company group).
    /// </summary>
    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(GraphQlUrl)
        && !string.IsNullOrWhiteSpace(Token);

    /// <summary>
    /// Combinado quote+fulfillment. O processor usa só <see cref="FulfillmentEnabled"/>.
    /// </summary>
    public bool CanFulfill => Enabled && FulfillmentEnabled;
}
