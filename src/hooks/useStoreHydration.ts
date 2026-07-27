"use client";

import { useEffect } from "react";
import { useCartStore } from "@/stores/cartStore";
import { useAuthStore } from "@/stores/authStore";
import { useProductsStore } from "@/stores/productsStore";
import { useOrdersStore } from "@/stores/ordersStore";
import { useSettingsStore } from "@/stores/settingsStore";

function markHydrated() {
  if (useCartStore.persist.hasHydrated()) useCartStore.getState().setHydrated(true);
  if (useAuthStore.persist.hasHydrated()) useAuthStore.getState().setHydrated(true);
  if (useProductsStore.persist.hasHydrated())
    useProductsStore.getState().setHydrated(true);
  if (useOrdersStore.persist.hasHydrated())
    useOrdersStore.getState().setHydrated(true);
  if (useSettingsStore.persist.hasHydrated())
    useSettingsStore.getState().setHydrated(true);
}

/** Evita mismatch de hidratação com localStorage */
export function useStoreHydration() {
  const cart = useCartStore((s) => s.hydrated);
  const auth = useAuthStore((s) => s.hydrated);
  const products = useProductsStore((s) => s.hydrated);
  const orders = useOrdersStore((s) => s.hydrated);
  const settings = useSettingsStore((s) => s.hydrated);

  useEffect(() => {
    const unsubs = [
      useCartStore.persist.onFinishHydration(() =>
        useCartStore.getState().setHydrated(true),
      ),
      useAuthStore.persist.onFinishHydration(() =>
        useAuthStore.getState().setHydrated(true),
      ),
      useProductsStore.persist.onFinishHydration(() =>
        useProductsStore.getState().setHydrated(true),
      ),
      useOrdersStore.persist.onFinishHydration(() =>
        useOrdersStore.getState().setHydrated(true),
      ),
      useSettingsStore.persist.onFinishHydration(() =>
        useSettingsStore.getState().setHydrated(true),
      ),
    ];

    markHydrated();

    return () => unsubs.forEach((u) => u());
  }, []);

  return cart && auth && products && orders && settings;
}
