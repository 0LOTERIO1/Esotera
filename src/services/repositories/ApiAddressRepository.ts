import { addressesApi, toAddressUserMessage } from "@/services/api/addressesApi";
import { ApiError } from "@/services/api/apiClient";
import type { AddressInput, SavedAddress } from "@/types";
import type { IAddressRepository } from "./IAddressRepository";

/**
 * Endereços do usuário autenticado via API .NET (JWT obrigatório).
 * Sem fallback para mock — falhas sobem para a UI.
 */
export class ApiAddressRepository implements IAddressRepository {
  private rethrow(error: unknown): never {
    if (error instanceof ApiError && (error.status === 401 || error.status === 403)) {
      throw error;
    }
    throw new Error(toAddressUserMessage(error));
  }

  async list(): Promise<SavedAddress[]> {
    try {
      return await addressesApi.list();
    } catch (error) {
      this.rethrow(error);
    }
  }

  async create(input: AddressInput): Promise<SavedAddress> {
    try {
      return await addressesApi.create(input);
    } catch (error) {
      this.rethrow(error);
    }
  }

  async update(id: string, input: AddressInput): Promise<SavedAddress> {
    try {
      return await addressesApi.update(id, input);
    } catch (error) {
      this.rethrow(error);
    }
  }

  async remove(id: string): Promise<void> {
    try {
      await addressesApi.delete(id);
    } catch (error) {
      this.rethrow(error);
    }
  }

  async setPrimary(id: string): Promise<void> {
    try {
      await addressesApi.setPrimary(id);
    } catch (error) {
      this.rethrow(error);
    }
  }
}
