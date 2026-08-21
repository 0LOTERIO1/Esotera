import { sessionService } from "./sessionService";

export class ApiError extends Error {
  constructor(
    public status: number,
    public title: string,
    public detail?: string,
    public errors?: Record<string, string[]>,
    public reasonCode?: string,
  ) {
    super(detail || title);
    this.name = "ApiError";
  }

  /** Mensagem amigável para exibir na UI (sem detalhes técnicos) */
  get userMessage(): string {
    if (this.status === 0) {
      return (
        this.detail ||
        "Não foi possível conectar à API. Verifique sua conexão e tente novamente."
      );
    }
    if (this.status === 400) {
      if (this.errors) {
        const first = Object.values(this.errors).flat().find(Boolean);
        if (typeof first === "string" && first.length > 0 && first.length < 200) {
          return first;
        }
      }
      return this.detail || "Dados inválidos. Revise as informações e tente novamente.";
    }
    if (this.status === 401) {
      // Preferir detalhe da API (ex.: login) em vez de mensagem genérica de sessão.
      return this.detail || "Credenciais inválidas. Verifique e-mail e senha.";
    }
    if (this.status === 403) {
      return this.detail || "Você não tem permissão para esta ação.";
    }
    if (this.status === 404) {
      return "Recurso não encontrado.";
    }
    if (this.status === 409) {
      return (
        this.detail ||
        "Os dados desta tentativa foram alterados. Revise o pedido e tente novamente."
      );
    }
    if (this.status === 429) {
      return "Muitas tentativas. Aguarde cerca de 1 minuto e tente novamente.";
    }
    if (this.status === 502 || this.status === 503 || this.status === 504) {
      return "A API está iniciando ou temporariamente indisponível. Tente novamente em alguns segundos.";
    }
    if (this.status >= 500) {
      return "Erro no servidor. Tente novamente em instantes.";
    }
    return this.detail || this.title || "Ocorreu um erro inesperado.";
  }
}

export type RequestOptions = RequestInit & {
  /** Se true, envia Authorization Bearer. Padrão: false (rotas públicas). */
  auth?: boolean;
};

function getBaseUrl(): string {
  const url = process.env.NEXT_PUBLIC_API_URL?.trim();
  return url && url.length > 0 ? url.replace(/\/$/, "") : "http://localhost:5080";
}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

async function parseError(response: Response): Promise<ApiError> {
  const status = response.status;
  const fallbackTitle =
    status === 429
      ? "Muitas tentativas"
      : status === 502 || status === 503 || status === 504
        ? "API indisponível"
        : "Erro ao comunicar com o servidor";

  const text = await response.text().catch(() => "");
  if (!text.trim()) {
    return new ApiError(status, fallbackTitle, undefined);
  }

  try {
    const data = JSON.parse(text) as {
      title?: string;
      detail?: string;
      errors?: Record<string, string[]>;
      reasonCode?: string;
      eligibilityReason?: string;
      extensions?: {
        reasonCode?: string;
        eligibilityReason?: string;
      };
    };
    const reasonCode =
      data.reasonCode ||
      data.eligibilityReason ||
      data.extensions?.reasonCode ||
      data.extensions?.eligibilityReason;
    return new ApiError(
      status,
      data.title || fallbackTitle,
      data.detail,
      data.errors,
      typeof reasonCode === "string" ? reasonCode : undefined,
    );
  } catch {
    // HTML/texto do gateway (cold start Render, proxy, etc.)
    return new ApiError(status, fallbackTitle, undefined);
  }
}

function isRetryableStatus(status: number): boolean {
  return status === 502 || status === 503 || status === 504;
}

async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { auth = false, headers: initHeaders, ...init } = options;
  const url = `${getBaseUrl()}${path}`;

  const headers: Record<string, string> = {
    ...(initHeaders as Record<string, string>),
  };

  if (auth) {
    const token = sessionService.getToken();
    if (token) {
      headers.Authorization = `Bearer ${token}`;
    }
  }

  if (init.body && typeof init.body === "string" && !headers["Content-Type"]) {
    headers["Content-Type"] = "application/json";
  }

  const sentBearer = Boolean(headers.Authorization);
  const maxAttempts = 2;
  let lastNetworkError: unknown;

  for (let attempt = 1; attempt <= maxAttempts; attempt += 1) {
    let response: Response;
    try {
      response = await fetch(url, { ...init, headers });
    } catch (error) {
      lastNetworkError = error;
      if (
        (error instanceof DOMException && error.name === "AbortError") ||
        (error instanceof Error && error.name === "AbortError")
      ) {
        throw new ApiError(
          0,
          "Tempo esgotado",
          "A requisição demorou demais. Se acabou de se inscrever, a inscrição pode já ter sido salva.",
        );
      }
      // Cold start / rede: uma nova tentativa após breve espera.
      if (attempt < maxAttempts) {
        await sleep(1500);
        continue;
      }
      throw new ApiError(
        0,
        "API indisponível",
        "Não foi possível conectar à API. Se o serviço acabou de despertar, aguarde alguns segundos e tente de novo.",
      );
    }

    // Só invalida sessão se um JWT foi enviado e rejeitado (token inválido/expirado).
    if (response.status === 401 && auth && sentBearer) {
      sessionService.notifyUnauthorized();
      throw new ApiError(
        401,
        "Não autorizado",
        "Sessão expirada. Faça login novamente.",
      );
    }

    if (response.status === 401 && auth && !sentBearer) {
      throw new ApiError(
        401,
        "Não autorizado",
        "É necessário estar autenticado na API para esta ação.",
      );
    }

    if (!response.ok && isRetryableStatus(response.status) && attempt < maxAttempts) {
      await sleep(1500);
      continue;
    }

    if (!response.ok) {
      throw await parseError(response);
    }

    if (response.status === 204) {
      return undefined as T;
    }

    const text = await response.text();
    if (!text.trim()) {
      return undefined as T;
    }
    try {
      return JSON.parse(text) as T;
    } catch {
      throw new ApiError(
        response.status,
        "Resposta inválida",
        "A API retornou uma resposta inesperada. Tente novamente.",
      );
    }
  }

  void lastNetworkError;
  throw new ApiError(
    0,
    "API indisponível",
    "Não foi possível conectar à API. Tente novamente em instantes.",
  );
}

export const apiClient = {
  getBaseUrl,

  get<T>(path: string, options?: RequestOptions): Promise<T> {
    return request<T>(path, { ...options, method: "GET" });
  },

  post<T>(path: string, body?: unknown, options?: RequestOptions): Promise<T> {
    return request<T>(path, {
      ...options,
      method: "POST",
      body: body !== undefined ? JSON.stringify(body) : undefined,
    });
  },

  put<T>(path: string, body?: unknown, options?: RequestOptions): Promise<T> {
    return request<T>(path, {
      ...options,
      method: "PUT",
      body: body !== undefined ? JSON.stringify(body) : undefined,
    });
  },

  patch<T>(path: string, body?: unknown, options?: RequestOptions): Promise<T> {
    return request<T>(path, {
      ...options,
      method: "PATCH",
      body: body !== undefined ? JSON.stringify(body) : undefined,
    });
  },

  delete<T>(path: string, options?: RequestOptions): Promise<T> {
    return request<T>(path, { ...options, method: "DELETE" });
  },

  /** Multipart upload — não define Content-Type (boundary do browser). */
  postFormData<T>(
    path: string,
    formData: FormData,
    options?: RequestOptions,
  ): Promise<T> {
    return request<T>(path, {
      ...options,
      method: "POST",
      body: formData,
    });
  },
};

/** @deprecated use sessionService — mantido para imports legados */
export function getToken() {
  return sessionService.getToken();
}
export function setToken(token: string | null) {
  sessionService.setToken(token);
}
export function clearToken() {
  sessionService.clear();
}
