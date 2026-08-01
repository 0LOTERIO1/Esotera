namespace Esotera.Application.Options;

public class EmailOptions
{
    public const string SectionName = "Email";

    public bool Enabled { get; set; }
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public bool SmtpUseSsl { get; set; } = true;
    public string? SmtpUser { get; set; }
    public string? SmtpPassword { get; set; }
    public string FromAddress { get; set; } = "esoteralivraria1@gmail.com";
    public string FromName { get; set; } = "Esotera";
    /// <summary>Base URL do frontend (ex.: https://esotera.vercel.app) para montar links.</summary>
    public string? FrontendBaseUrl { get; set; }

    /// <summary>
    /// Opcional: e-mail do admin para aviso de nova inscrição na newsletter.
    /// Só envia se SMTP estiver configurado e este valor preenchido.
    /// </summary>
    public string? AdminNotifyEmail { get; set; }

    /// <summary>
    /// Timeout do SmtpClient e do envio (segundos). Evita request pendente no Render
    /// quando smtp.gmail.com:587 está bloqueado ou lento.
    /// </summary>
    public int SmtpTimeoutSeconds { get; set; } = 15;

    public bool IsSmtpConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(SmtpHost)
        && !string.IsNullOrWhiteSpace(SmtpUser)
        && !string.IsNullOrWhiteSpace(SmtpPassword)
        && !string.IsNullOrWhiteSpace(FromAddress);
}
