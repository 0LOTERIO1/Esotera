import { apiClient } from "./apiClient";

export type NewsletterMessage = { message: string };

export type NewsletterSubscription = {
  id: string;
  email: string;
  isActive: boolean;
  consentAtUtc: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  unsubscribedAtUtc?: string | null;
};

export const newsletterApi = {
  subscribe(email: string, consent: boolean) {
    return apiClient.post<NewsletterMessage>(
      "/api/newsletter/subscribe",
      { email, consent },
      { auth: false },
    );
  },

  unsubscribe(token: string) {
    return apiClient.post<NewsletterMessage>(
      "/api/newsletter/unsubscribe",
      { token },
      { auth: false },
    );
  },

  adminList(params: {
    search?: string;
    isActive?: boolean | null;
    skip?: number;
    take?: number;
  }) {
    const q = new URLSearchParams();
    if (params.search) q.set("search", params.search);
    if (params.isActive === true) q.set("isActive", "true");
    if (params.isActive === false) q.set("isActive", "false");
    if (params.skip != null) q.set("skip", String(params.skip));
    if (params.take != null) q.set("take", String(params.take));
    const qs = q.toString();
    return apiClient.get<{ items: NewsletterSubscription[]; total: number }>(
      `/api/admin/newsletter${qs ? `?${qs}` : ""}`,
      { auth: true },
    );
  },

  async adminExportCsv(params: {
    search?: string;
    isActive?: boolean | null;
  }): Promise<Blob> {
    const q = new URLSearchParams();
    if (params.search) q.set("search", params.search);
    if (params.isActive === true) q.set("isActive", "true");
    if (params.isActive === false) q.set("isActive", "false");
    const qs = q.toString();
    const base =
      process.env.NEXT_PUBLIC_API_URL?.replace(/\/$/, "") ||
      "http://localhost:5080";
    const { sessionService } = await import("./sessionService");
    const { ApiError } = await import("./apiClient");
    const token = sessionService.getToken();
    if (!token) {
      throw new ApiError(
        401,
        "Não autorizado",
        "É necessário estar autenticado na API para esta ação.",
      );
    }
    const res = await fetch(
      `${base}/api/admin/newsletter/export${qs ? `?${qs}` : ""}`,
      {
        headers: { Authorization: `Bearer ${token}` },
      },
    );
    if (res.status === 401) {
      sessionService.notifyUnauthorized();
      throw new ApiError(
        401,
        "Não autorizado",
        "Sessão expirada. Faça login novamente.",
      );
    }
    if (res.status === 403) {
      throw new ApiError(
        403,
        "Proibido",
        "Você não tem permissão para esta ação.",
      );
    }
    if (!res.ok) throw new Error("Falha ao exportar CSV.");
    return res.blob();
  },
};
