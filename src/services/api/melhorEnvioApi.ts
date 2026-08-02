import { apiClient } from "./apiClient";

const AUTH = { auth: true } as const;

export type MelhorEnvioAuthorizeResponse = {
  authorizationUrl: string;
};

export type MelhorEnvioStatusDto = {
  connected: boolean;
  configured: boolean;
  environment: string | null;
  scopes: string | null;
  accessTokenExpiresAtUtc: string | null;
  refreshTokenExpiresAtUtc: string | null;
  connectedAtUtc: string | null;
  accessTokenValid: boolean;
  needsReauthorization: boolean;
};

export const melhorEnvioApi = {
  getStatus(): Promise<MelhorEnvioStatusDto> {
    return apiClient.get<MelhorEnvioStatusDto>(
      "/api/admin/integrations/melhor-envio/status",
      AUTH,
    );
  },

  /** Chama autenticado (Bearer) e devolve a URL — o browser navega em seguida. */
  startAuthorize(): Promise<MelhorEnvioAuthorizeResponse> {
    return apiClient.get<MelhorEnvioAuthorizeResponse>(
      "/api/integrations/melhor-envio/authorize",
      AUTH,
    );
  },
};

export function melhorEnvioErrorMessage(reason: string | null): string {
  switch (reason) {
    case "state_invalid":
      return "Sessão de autorização inválida. Tente conectar novamente.";
    case "state_expired":
      return "A autorização expirou. Tente conectar novamente.";
    case "already_used":
      return "Este link de autorização já foi usado.";
    case "denied":
      return "Autorização recusada no Melhor Envio.";
    case "missing_code":
      return "Código de autorização ausente.";
    case "exchange_failed":
      return "Não foi possível concluir a conexão com o Melhor Envio.";
    case "config_missing":
      return "Integração Melhor Envio não configurada no servidor.";
    case "encryption_failed":
      return "Falha ao proteger os tokens. Verifique a chave de criptografia.";
    case "persist_failed":
      return "Não foi possível salvar a conexão. Tente novamente.";
    default:
      return "Não foi possível conectar ao Melhor Envio.";
  }
}
