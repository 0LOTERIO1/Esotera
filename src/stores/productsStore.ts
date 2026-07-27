"use client";

import { create } from "zustand";
import { persist } from "zustand/middleware";
import { STORAGE_KEYS } from "@/utils/storage";
import { initialProducts } from "@/data/products";
import { generateId } from "@/utils/format";
import type { Product } from "@/types";

type ProductsState = {
  products: Product[];
  hydrated: boolean;
  setHydrated: (value: boolean) => void;
  getBySlug: (slug: string) => Product | undefined;
  getById: (id: string) => Product | undefined;
  upsert: (product: Omit<Product, "id"> & { id?: string }) => void;
  setAvailability: (id: string, isAvailable: boolean) => void;
  resetToSeed: () => void;
};

export const useProductsStore = create<ProductsState>()(
  persist(
    (set, get) => ({
      products: initialProducts,
      hydrated: false,
      setHydrated: (value) => set({ hydrated: value }),
      getBySlug: (slug) => get().products.find((p) => p.slug === slug),
      getById: (id) => get().products.find((p) => p.id === id),
      upsert: (product) => {
        set((state) => {
          if (product.id) {
            const exists = state.products.some((p) => p.id === product.id);
            if (exists) {
              return {
                products: state.products.map((p) =>
                  p.id === product.id ? ({ ...p, ...product } as Product) : p,
                ),
              };
            }
          }
          const id = product.id ?? generateId("prod");
          return {
            products: [...state.products, { ...product, id } as Product],
          };
        });
      },
      setAvailability: (id, isAvailable) => {
        set((state) => ({
          products: state.products.map((p) =>
            p.id === id ? { ...p, isAvailable } : p,
          ),
        }));
      },
      resetToSeed: () => set({ products: initialProducts }),
    }),
    {
      name: STORAGE_KEYS.products,
      onRehydrateStorage: () => (state) => {
        state?.setHydrated(true);
      },
      partialize: (state) => ({ products: state.products }),
    },
  ),
);
