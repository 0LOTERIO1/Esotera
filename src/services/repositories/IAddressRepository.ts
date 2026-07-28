import type { AddressInput, SavedAddress } from "@/types";

export interface IAddressRepository {
  list(): Promise<SavedAddress[]>;
  create(input: AddressInput): Promise<SavedAddress>;
  update(id: string, input: AddressInput): Promise<SavedAddress>;
  remove(id: string): Promise<void>;
  setPrimary(id: string): Promise<void>;
}
