import { apiClient } from "./apiClient";

const AUTH = { auth: true } as const;

export type PublicStoreSettingsDto = {
  storeName: string;
  freeShippingMin: number;
  freeShippingStates: string[];
  j3Price: number;
  j3CutoffHour: number;
  shippingSubsidyEnabled: boolean;
  shippingSubsidyAmount: number;
};

export type AdminStoreSettingsDto = PublicStoreSettingsDto & {
  shippingOriginCep: string;
  packageLengthCm: number;
  packageWidthCm: number;
  packageHeightCm: number;
  packageWeightGrams: number;
  melhorEnvioQuoteEnabled: boolean;
  updatedAtUtc: string;
};

export type UpdateStoreSettingsPayload = {
  storeName: string;
  freeShippingMin: number;
  freeShippingStates: string[];
  j3Price: number;
  j3CutoffHour: number;
  shippingSubsidyEnabled: boolean;
  shippingSubsidyAmount: number;
  shippingOriginCep: string;
  packageLengthCm: number;
  packageWidthCm: number;
  packageHeightCm: number;
  packageWeightGrams: number;
  melhorEnvioQuoteEnabled: boolean;
};

export const settingsApi = {
  getPublic(): Promise<PublicStoreSettingsDto> {
    return apiClient.get<PublicStoreSettingsDto>("/api/settings/public");
  },

  getAdmin(): Promise<AdminStoreSettingsDto> {
    return apiClient.get<AdminStoreSettingsDto>("/api/admin/settings", AUTH);
  },

  update(payload: UpdateStoreSettingsPayload): Promise<AdminStoreSettingsDto> {
    return apiClient.put<AdminStoreSettingsDto>(
      "/api/admin/settings",
      payload,
      AUTH,
    );
  },
};
