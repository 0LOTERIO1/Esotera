"use client";

import { create } from "zustand";
import { persist } from "zustand/middleware";
import { STORAGE_KEYS } from "@/utils/storage";
import { defaultStoreSettings } from "@/config/shipping";
import type { StoreSettings } from "@/types";

type SettingsState = {
  settings: StoreSettings;
  hydrated: boolean;
  setHydrated: (value: boolean) => void;
  updateSettings: (partial: Partial<StoreSettings>) => void;
  resetSettings: () => void;
};

export const useSettingsStore = create<SettingsState>()(
  persist(
    (set) => ({
      settings: defaultStoreSettings,
      hydrated: false,
      setHydrated: (value) => set({ hydrated: value }),
      updateSettings: (partial) =>
        set((state) => ({
          settings: {
            ...state.settings,
            ...partial,
            shippingSubsidy: {
              ...state.settings.shippingSubsidy,
              ...(partial.shippingSubsidy ?? {}),
            },
          },
        })),
      resetSettings: () => set({ settings: defaultStoreSettings }),
    }),
    {
      name: STORAGE_KEYS.settings,
      onRehydrateStorage: () => (state) => {
        state?.setHydrated(true);
      },
      partialize: (state) => ({ settings: state.settings }),
    },
  ),
);
