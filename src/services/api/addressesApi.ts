import { apiClient, ApiError } from "./apiClient";
import { normalizeAddressPayload } from "@/utils/address";
import type { AddressInput, SavedAddress } from "@/types";

type ApiAddress = {
  id: string;
  cep: string;
  street: string;
  number: string;
  complement?: string | null;
  neighborhood: string;
  city: string;
  state: string;
  isPrimary: boolean;
  isResidentialAddress?: boolean | null;
};

function mapAddress(apiAddress: ApiAddress): SavedAddress {
  return {
    id: apiAddress.id,
    cep: apiAddress.cep,
    street: apiAddress.street,
    number: apiAddress.number,
    complement: apiAddress.complement ?? undefined,
    neighborhood: apiAddress.neighborhood,
    city: apiAddress.city,
    state: apiAddress.state,
    isPrimary: apiAddress.isPrimary,
    isResidentialAddress:
      apiAddress.isResidentialAddress === true ||
      apiAddress.isResidentialAddress === false
        ? apiAddress.isResidentialAddress
        : null,
  };
}

function toRequestBody(input: AddressInput) {
  const normalized = normalizeAddressPayload(input);
  return {
    cep: normalized.cep,
    street: normalized.street,
    number: normalized.number,
    complement: normalized.complement ?? null,
    neighborhood: normalized.neighborhood,
    city: normalized.city,
    state: normalized.state,
    isPrimary: Boolean(normalized.isPrimary),
    isResidentialAddress:
      normalized.isResidentialAddress === true ||
      normalized.isResidentialAddress === false
        ? normalized.isResidentialAddress
        : null,
  };
}

const AUTH = { auth: true as const };

export const addressesApi = {
  /** Todas as rotas de endereço exigem JWT */
  async list(): Promise<SavedAddress[]> {
    const response = await apiClient.get<ApiAddress[]>(
      "/api/users/me/addresses",
      AUTH,
    );
    return response.map(mapAddress);
  },

  async getById(id: string): Promise<SavedAddress | null> {
    try {
      const response = await apiClient.get<ApiAddress>(
        `/api/users/me/addresses/${id}`,
        AUTH,
      );
      return mapAddress(response);
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) return null;
      throw error;
    }
  },

  async create(input: AddressInput): Promise<SavedAddress> {
    const response = await apiClient.post<ApiAddress>(
      "/api/users/me/addresses",
      toRequestBody(input),
      AUTH,
    );
    return mapAddress(response);
  },

  async update(id: string, input: AddressInput): Promise<SavedAddress> {
    const response = await apiClient.put<ApiAddress>(
      `/api/users/me/addresses/${id}`,
      toRequestBody(input),
      AUTH,
    );
    return mapAddress(response);
  },

  async delete(id: string): Promise<void> {
    await apiClient.delete(`/api/users/me/addresses/${id}`, AUTH);
  },

  /** Backend retorna 204 — sem corpo */
  async setPrimary(id: string): Promise<void> {
    await apiClient.post(
      `/api/users/me/addresses/${id}/set-primary`,
      {},
      AUTH,
    );
  },
};

export function toAddressUserMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 400 && error.errors) {
      const first = Object.values(error.errors).flat().find(Boolean);
      if (first && typeof first === "string" && first.length < 200) {
        return first;
      }
    }
    return error.userMessage;
  }
  if (error instanceof Error) return error.message;
  return "Não foi possível processar o endereço.";
}
