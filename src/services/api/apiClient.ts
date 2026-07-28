import { sessionService } from "./sessionService";

export class ApiError extends Error {
  constructor(
    public status: number,
    public title: string,
    public detail?: string,
    public errors?: Record<string, string[]>,
  ) {
    super(detail || title);
    this.name = "ApiError";
  }

  /** Mensagem amigável para exibir na UI (sem detalhes técnicos) */
  get userMessage(): string {
    if (this.status === 0) {
      return "Não foi possível conectar à API. Verifique se o backend está em execução.";
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
      return "Sessão expirada ou credenciais inválidas. Faça login novamente.";
    }
    if (this.status === 403) {
      return "Você não tem permissão para esta ação.";
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

async function parseError(response: Response): Promise<ApiError> {
  try {
    const data = (await response.json()) as {
      title?: string;
      detail?: string;
      errors?: Record<string, string[]>;
    };
    return new ApiError(
      response.status,
      data.title || "Erro",
      data.detail,
      data.errors,
    );
  } catch {
    return new ApiError(
      response.status,
      "Erro ao comunicar com o servidor",
      undefined,
    );
  }
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

  let response: Response;
  try {
    response = await fetch(url, { ...init, headers });
  } catch {
    throw new ApiError(
      0,
      "API indisponível",
      "Não foi possível conectar à API. Verifique se o backend está em execução.",
    );
  }

  if (response.status === 401 && auth) {
    sessionService.notifyUnauthorized();
    throw new ApiError(
      401,
      "Não autorizado",
      "Sessão expirada. Faça login novamente.",
    );
  }

  if (!response.ok) {
    throw await parseError(response);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return response.json() as Promise<T>;
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
