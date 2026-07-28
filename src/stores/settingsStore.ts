"use client";

import { create } from "zustand";
import { persist } from "zustand/middleware";
import { STORAGE_KEYS } from "@/utils/storage";
import { defaultStoreSettings } from "@/config/shipping";
import { isApiMode } from "@/config/dataMode";
import { getSettingsRepository } from "@/services/repositories";
import { MockSettingsRepository } from "@/services/repositories/MockSettingsRepository";
import { toUpdateSettingsPayload } from "@/services/api/settingsMapper";
import type { StoreSettings } from "@/types";

type SettingsState = {
  settings: StoreSettings;
  hydrated: boolean;
  loading: boolean;
  setHydrated: (value: boolean) => void;
  updateSettings: (partial: Partial<StoreSettings>) => void;
  resetSettings: () => void;
  /** Carrega configurações públicas (API) ou sincroniza mock */
  refreshPublic: () => Promise<void>;
  /** Persiste via repositório (API PUT ou mock) e atualiza o store */
  saveSettings: (settings: StoreSettings) => Promise<StoreSettings>;
};

export const useSettingsStore = create<SettingsState>()(
  persist(
    (set) => ({
      settings: defaultStoreSettings,
      hydrated: false,
      loading: false,
      setHydrated: (value) => set({ hydrated: value }),
      updateSettings: (partial) =>
        set((state) => {
          const next = {
            ...state.settings,
            ...partial,
            shippingSubsidy: {
              ...state.settings.shippingSubsidy,
              ...(partial.shippingSubsidy ?? {}),
            },
            freeShippingStates:
              partial.freeShippingStates ?? state.settings.freeShippingStates,
          };
          if (!isApiMode()) {
            MockSettingsRepository.syncFromStore(next);
          }
          return { settings: next };
        }),
      resetSettings: () => {
        if (!isApiMode()) {
          MockSettingsRepository.syncFromStore(defaultStoreSettings);
        }
        set({ settings: defaultStoreSettings });
      },
      refreshPublic: async () => {
        set({ loading: true });
        try {
          const repo = getSettingsRepository();
          const settings = await repo.getPublic();
          set({ settings });
          if (!isApiMode()) {
            MockSettingsRepository.syncFromStore(settings);
          }
        } finally {
          set({ loading: false });
        }
      },
      saveSettings: async (settings) => {
        const repo = getSettingsRepository();
        const saved = await repo.update(toUpdateSettingsPayload(settings));
        set({ settings: saved });
        if (!isApiMode()) {
          MockSettingsRepository.syncFromStore(saved);
        }
        return saved;
      },
    }),
    {
      name: STORAGE_KEYS.settings,
      // Em modo API não persistimos configurações comerciais no localStorage
      skipHydration: false,
      onRehydrateStorage: () => (state) => {
        state?.setHydrated(true);
      },
      partialize: (state) =>
        isApiMode() ? {} : { settings: state.settings },
    },
  ),
);
