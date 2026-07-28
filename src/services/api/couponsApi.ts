import { apiClient } from "./apiClient";

const AUTH = { auth: true } as const;

export type ValidateCouponResponse = {
  isValid: boolean;
  code?: string | null;
  discountAmount?: number;
  errorMessage?: string | null;
};

export type AdminCouponDto = {
  id: string;
  code: string;
  discountAmount: number;
  minPurchase: number;
  appliesToShipping: boolean;
  oneUsePerCustomer: boolean;
  maxTotalUses: number | null;
  usageCount: number;
  isActive: boolean;
  isArchived: boolean;
  archivedAtUtc?: string | null;
  validFromUtc?: string | null;
  validUntilUtc?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
};

export type CreateCouponPayload = {
  code: string;
  discountAmount: number;
  minPurchase: number;
  oneUsePerCustomer?: boolean;
  maxTotalUses?: number | null;
  isActive?: boolean;
  validFromUtc?: string | null;
  validUntilUtc?: string | null;
};

export type UpdateCouponPayload = {
  code?: string | null;
  discountAmount?: number | null;
  minPurchase?: number | null;
  oneUsePerCustomer?: boolean | null;
  maxTotalUses?: number | null;
  clearMaxTotalUses?: boolean | null;
  isActive?: boolean | null;
  validFromUtc?: string | null;
  validUntilUtc?: string | null;
  clearValidFrom?: boolean | null;
  clearValidUntil?: boolean | null;
};

export const couponsApi = {
  validate(code: string, subtotal: number): Promise<ValidateCouponResponse> {
    return apiClient.post<ValidateCouponResponse>(
      "/api/coupons/validate",
      { code, subtotal },
      AUTH,
    );
  },

  listAdmin(params?: {
    archived?: "active" | "archived" | "all" | "only";
    isActive?: boolean;
  }): Promise<AdminCouponDto[]> {
    const q = new URLSearchParams();
    if (params?.archived === "all") q.set("archived", "all");
    else if (params?.archived === "archived" || params?.archived === "only")
      q.set("archived", "only");
    if (params?.isActive !== undefined)
      q.set("isActive", String(params.isActive));
    const qs = q.toString();
    return apiClient.get<AdminCouponDto[]>(
      `/api/admin/coupons${qs ? `?${qs}` : ""}`,
      AUTH,
    );
  },

  getAdmin(id: string): Promise<AdminCouponDto> {
    return apiClient.get<AdminCouponDto>(`/api/admin/coupons/${id}`, AUTH);
  },

  create(payload: CreateCouponPayload): Promise<AdminCouponDto> {
    return apiClient.post<AdminCouponDto>("/api/admin/coupons", payload, AUTH);
  },

  update(id: string, payload: UpdateCouponPayload): Promise<AdminCouponDto> {
    return apiClient.put<AdminCouponDto>(
      `/api/admin/coupons/${id}`,
      payload,
      AUTH,
    );
  },

  activate(id: string): Promise<AdminCouponDto> {
    return apiClient.patch<AdminCouponDto>(
      `/api/admin/coupons/${id}/activate`,
      undefined,
      AUTH,
    );
  },

  deactivate(id: string): Promise<AdminCouponDto> {
    return apiClient.patch<AdminCouponDto>(
      `/api/admin/coupons/${id}/deactivate`,
      undefined,
      AUTH,
    );
  },

  archive(id: string): Promise<AdminCouponDto> {
    return apiClient.patch<AdminCouponDto>(
      `/api/admin/coupons/${id}/archive`,
      undefined,
      AUTH,
    );
  },

  restore(id: string): Promise<AdminCouponDto> {
    return apiClient.patch<AdminCouponDto>(
      `/api/admin/coupons/${id}/restore`,
      undefined,
      AUTH,
    );
  },
};
