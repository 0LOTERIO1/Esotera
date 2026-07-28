"use client";

import { create } from "zustand";
import { initialProducts } from "@/data/products";
import { getProductRepository } from "@/services/repositories";
import { isApiMode } from "@/config/dataMode";
import { STORAGE_KEYS } from "@/utils/storage";
import type { ProductInput } from "@/services/repositories/IProductRepository";
import type { Product } from "@/types";

type ProductsState = {
  products: Product[];
  hydrated: boolean;
  loading: boolean;
  error: string | null;
  setHydrated: (value: boolean) => void;
  /** Catálogo público (nunca inclui arquivados). */
  refresh: () => Promise<void>;
  getBySlug: (slug: string) => Product | undefined;
  getById: (id: string) => Product | undefined;
  fetchBySlug: (slug: string) => Promise<Product | null>;
  upsert: (product: ProductInput, imageFile?: File) => Promise<Product>;
  setAvailability: (id: string, isAvailable: boolean) => Promise<void>;
  resetToSeed: () => void;
  clearError: () => void;
};

export const useProductsStore = create<ProductsState>((set, get) => ({
  products: isApiMode() ? [] : initialProducts,
  hydrated: false,
  loading: false,
  error: null,
  setHydrated: (value) => set({ hydrated: value }),
  clearError: () => set({ error: null }),
  refresh: async () => {
    set({ loading: true, error: null });
    try {
      const repo = getProductRepository();
      const products = await repo.getCatalog();
      set({ products, loading: false, error: null, hydrated: true });
    } catch (error) {
      const message =
        error instanceof Error
          ? error.message
          : "Não foi possível carregar o catálogo.";
      if (isApiMode()) {
        set({ products: [], loading: false, error: message, hydrated: true });
        return;
      }
      set({
        products: initialProducts,
        loading: false,
        error: null,
        hydrated: true,
      });
    }
  },
  getBySlug: (slug) => get().products.find((p) => p.slug === slug),
  getById: (id) => get().products.find((p) => p.id === id),
  fetchBySlug: async (slug) => {
    try {
      const repo = getProductRepository();
      const product = await repo.getBySlug(slug);
      if (product) {
        set((state) => {
          const exists = state.products.some((p) => p.id === product.id);
          return {
            products: exists
              ? state.products.map((p) => (p.id === product.id ? product : p))
              : [...state.products, product],
          };
        });
      }
      return product ?? null;
    } catch (error) {
      const message =
        error instanceof Error
          ? error.message
          : "Não foi possível carregar o produto.";
      if (isApiMode()) {
        set({ error: message });
        throw error;
      }
      return get().getBySlug(slug) ?? null;
    }
  },
  upsert: async (product, imageFile) => {
    const repo = getProductRepository();
    const saved = await repo.upsert(product, imageFile);
    await get().refresh();
    return saved;
  },
  setAvailability: async (id, isAvailable) => {
    const repo = getProductRepository();
    await repo.setAvailability(id, isAvailable);
    await get().refresh();
  },
  resetToSeed: () => {
    if (typeof window !== "undefined") {
      localStorage.removeItem(STORAGE_KEYS.products);
    }
    set({ products: initialProducts, error: null });
  },
}));
