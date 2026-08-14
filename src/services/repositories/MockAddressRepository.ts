import { STORAGE_KEYS, safeParseJSON } from "@/utils/storage";
import { normalizeAddressPayload } from "@/utils/address";
import { useAuthStore } from "@/stores/authStore";
import type { AddressInput, SavedAddress } from "@/types";
import type { IAddressRepository } from "./IAddressRepository";

type AddressBook = Record<string, SavedAddress[]>;

function isFictionalDemoAddress(address: SavedAddress): boolean {
  const street = address.street.trim().toLowerCase();
  const neighborhood = address.neighborhood.trim().toLowerCase();
  return (
    street === "rua exemplo" ||
    (neighborhood === "ermelino matarazzo" && street.includes("exemplo")) ||
    (address.complement?.trim().toLowerCase() === "apto 12" &&
      street === "rua exemplo")
  );
}

/**
 * Endereços no localStorage (modo mock).
 * Sincroniza o endereço principal com authStore.user.address (checkout).
 */
export class MockAddressRepository implements IAddressRepository {
  private readBook(): AddressBook {
    if (typeof window === "undefined") return {};
    return safeParseJSON<AddressBook>(
      localStorage.getItem(STORAGE_KEYS.addresses),
      {},
    );
  }

  private writeBook(book: AddressBook) {
    if (typeof window === "undefined") return;
    localStorage.setItem(STORAGE_KEYS.addresses, JSON.stringify(book));
  }

  private syncPrimaryToProfile(addresses: SavedAddress[]) {
    const primary = addresses.find((a) => a.isPrimary) ?? addresses[0];
    if (!primary) return;
    useAuthStore.getState().updateProfile({
      address: {
        cep: primary.cep,
        street: primary.street,
        number: primary.number,
        complement: primary.complement,
        neighborhood: primary.neighborhood,
        city: primary.city,
        state: primary.state,
      },
    });
  }

  private ensureSeed(userId: string): SavedAddress[] {
    const book = this.readBook();
    const existing = book[userId];

    if (existing?.length) {
      const cleaned = existing.filter((a) => !isFictionalDemoAddress(a));
      if (cleaned.length !== existing.length) {
        book[userId] = cleaned;
        this.writeBook(book);
        if (cleaned.length) this.syncPrimaryToProfile(cleaned);
      }
      return cleaned;
    }

    const user = useAuthStore.getState().user;
    if (!user || user.id !== userId || !user.address?.street?.trim()) {
      return [];
    }

    if (
      isFictionalDemoAddress({
        ...user.address,
        id: "mock-primary",
        isPrimary: true,
      })
    ) {
      return [];
    }

    const seeded: SavedAddress[] = [
      {
        ...user.address,
        id: "mock-primary",
        isPrimary: true,
      },
    ];
    book[userId] = seeded;
    this.writeBook(book);
    return seeded;
  }

  private requireUserId(): string {
    const userId = useAuthStore.getState().user?.id;
    if (!userId) throw new Error("Faça login para gerenciar endereços.");
    return userId;
  }

  async list(): Promise<SavedAddress[]> {
    const userId = this.requireUserId();
    const list = this.ensureSeed(userId);
    return [...list].sort(
      (a, b) => Number(b.isPrimary) - Number(a.isPrimary),
    );
  }

  async create(input: AddressInput): Promise<SavedAddress> {
    const userId = this.requireUserId();
    const book = this.readBook();
    const list = this.ensureSeed(userId);
    const normalized = normalizeAddressPayload(input);

    const makePrimary = Boolean(normalized.isPrimary) || list.length === 0;
    const nextList = makePrimary
      ? list.map((a) => ({ ...a, isPrimary: false }))
      : [...list];

    const created: SavedAddress = {
      id: `mock-${crypto.randomUUID()}`,
      cep: normalized.cep,
      street: normalized.street,
      number: normalized.number,
      complement: normalized.complement,
      neighborhood: normalized.neighborhood,
      city: normalized.city,
      state: normalized.state,
      isPrimary: makePrimary,
      isResidentialAddress:
        normalized.isResidentialAddress === true ||
        normalized.isResidentialAddress === false
          ? normalized.isResidentialAddress
          : null,
    };

    nextList.push(created);
    book[userId] = nextList;
    this.writeBook(book);
    this.syncPrimaryToProfile(nextList);
    return created;
  }

  async update(id: string, input: AddressInput): Promise<SavedAddress> {
    const userId = this.requireUserId();
    const book = this.readBook();
    const list = this.ensureSeed(userId);
    const index = list.findIndex((a) => a.id === id);
    if (index < 0) throw new Error("Endereço não encontrado.");

    const normalized = normalizeAddressPayload(input);
    let nextList = list.map((a) =>
      a.id === id
        ? {
            ...a,
            cep: normalized.cep,
            street: normalized.street,
            number: normalized.number,
            complement: normalized.complement,
            neighborhood: normalized.neighborhood,
            city: normalized.city,
            state: normalized.state,
            isPrimary: a.isPrimary,
            isResidentialAddress:
              normalized.isResidentialAddress === true ||
              normalized.isResidentialAddress === false
                ? normalized.isResidentialAddress
                : a.isResidentialAddress ?? null,
          }
        : a,
    );

    if (normalized.isPrimary) {
      nextList = nextList.map((a) => ({
        ...a,
        isPrimary: a.id === id,
      }));
    }

    book[userId] = nextList;
    this.writeBook(book);
    this.syncPrimaryToProfile(nextList);
    return nextList.find((a) => a.id === id)!;
  }

  async remove(id: string): Promise<void> {
    const userId = this.requireUserId();
    const book = this.readBook();
    const list = this.ensureSeed(userId);
    let nextList = list.filter((a) => a.id !== id);
    if (nextList.length && !nextList.some((a) => a.isPrimary)) {
      nextList = nextList.map((a, i) => ({ ...a, isPrimary: i === 0 }));
    }
    book[userId] = nextList;
    this.writeBook(book);
    if (nextList.length) this.syncPrimaryToProfile(nextList);
  }

  async setPrimary(id: string): Promise<void> {
    const userId = this.requireUserId();
    const book = this.readBook();
    const list = this.ensureSeed(userId);
    if (!list.some((a) => a.id === id)) {
      throw new Error("Endereço não encontrado.");
    }
    const nextList = list.map((a) => ({
      ...a,
      isPrimary: a.id === id,
    }));
    book[userId] = nextList;
    this.writeBook(book);
    this.syncPrimaryToProfile(nextList);
  }
}
