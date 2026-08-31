using Esotera.Application.DTOs.Integrations;

namespace Esotera.Application.Interfaces;

/// <summary>
/// Diagnóstico Admin-only da integração Melhor Envio.
/// Read-only: nunca cria carrinho, compra ou etiqueta.
/// </summary>
public interface IMelhorEnvioDiagnosticsService
{
    /// <summary>
    /// Estado da configuração e da conexão. Com <paramref name="probe"/> = true,
    /// executa uma cotação de teste (somente leitura) para validar o token.
    /// </summary>
    Task<MelhorEnvioDiagnosticsDto> GetAsync(
        bool probe,
        CancellationToken cancellationToken = default);
}
