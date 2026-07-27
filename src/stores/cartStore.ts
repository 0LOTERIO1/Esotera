"use client";

import { create } from "zustand";
import { persist } from "zustand/middleware";
import { STORAGE_KEYS } from "@/utils/storage";
import type { AppliedCoupon, CartItem } from "@/types";

type CartState = {
  items: CartItem[];
  coupon: AppliedCoupon | null;
  hydrated: boolean;
  setHydrated: (value: boolean) => void;
  addItem: (productId: string, quantity?: number, variation?: string) => void;
  updateQuantity: (productId: string, quantity: number, variation?: string) => void;
  removeItem: (productId: string, variation?: string) => void;
  clearCart: () => void;
  setCoupon: (coupon: AppliedCoupon | null) => void;
  itemCount: () => number;
};

function sameLine(a: CartItem, productId: string, variation?: string) {
  return a.productId === productId && (a.variation ?? "") === (variation ?? "");
}

export const useCartStore = create<CartState>()(
  persist(
    (set, get) => ({
      items: [],
      coupon: null,
      hydrated: false,
      setHydrated: (value) => set({ hydrated: value }),
      addItem: (productId, quantity = 1, variation) => {
        set((state) => {
          const existing = state.items.find((i) =>
            sameLine(i, productId, variation),
          );
          if (existing) {
            return {
              items: state.items.map((i) =>
                sameLine(i, productId, variation)
                  ? { ...i, quantity: i.quantity + quantity }
                  : i,
              ),
            };
          }
          return {
            items: [...state.items, { productId, quantity, variation }],
          };
        });
      },
      updateQuantity: (productId, quantity, variation) => {
        if (quantity <= 0) {
          get().removeItem(productId, variation);
          return;
        }
        set((state) => ({
          items: state.items.map((i) =>
            sameLine(i, productId, variation) ? { ...i, quantity } : i,
          ),
        }));
      },
      removeItem: (productId, variation) => {
        set((state) => ({
          items: state.items.filter((i) => !sameLine(i, productId, variation)),
        }));
      },
      clearCart: () => set({ items: [], coupon: null }),
      setCoupon: (coupon) => set({ coupon }),
      itemCount: () => get().items.reduce((sum, i) => sum + i.quantity, 0),
    }),
    {
      name: STORAGE_KEYS.cart,
      onRehydrateStorage: () => (state) => {
        state?.setHydrated(true);
      },
      partialize: (state) => ({
        items: state.items,
        coupon: state.coupon,
      }),
    },
  ),
);
