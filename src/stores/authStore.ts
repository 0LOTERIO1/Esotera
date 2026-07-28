"use client";

import { create } from "zustand";
import { persist } from "zustand/middleware";
import { STORAGE_KEYS } from "@/utils/storage";
import { getAuthRepository } from "@/services/repositories";
import { sessionService } from "@/services/api/sessionService";
import { isApiMode } from "@/config/dataMode";
import type { RegisterInput } from "@/services/repositories/IAuthRepository";
import type { User } from "@/types";

type AuthState = {
  user: User | null;
  rememberMe: boolean;
  hydrated: boolean;
  sessionReady: boolean;
  setHydrated: (value: boolean) => void;
  restoreSession: () => Promise<void>;
  login: (email: string, password: string, remember?: boolean) => Promise<User>;
  loginDemoCustomer: () => Promise<User>;
  loginDemoAdmin: () => Promise<User>;
  register: (input: RegisterInput) => Promise<User>;
  logout: () => Promise<void>;
  updateProfile: (partial: Partial<User>) => void;
  isAdmin: () => boolean;
  isAuthenticated: () => boolean;
};

export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      user: null,
      rememberMe: false,
      hydrated: false,
      sessionReady: false,
      setHydrated: (value) => set({ hydrated: value }),
      restoreSession: async () => {
        sessionService.onUnauthorized(() => {
          // Token rejeitado pela API após envio do Bearer.
          set({ user: null });
        });

        if (!isApiMode()) {
          // Modo mock: não exige JWT.
          set({ sessionReady: true });
          return;
        }

        const token = sessionService.getToken();
        if (!token) {
          set({ user: null, sessionReady: true });
          return;
        }

        const repo = getAuthRepository();
        if (repo.restoreSession) {
          const user = await repo.restoreSession();
          set({ user, sessionReady: true });
          return;
        }

        set({ sessionReady: true });
      },
      login: async (email, password, remember = false) => {
        const repo = getAuthRepository();
        const user = await repo.login(email, password);
        set({ user, rememberMe: remember });
        return user;
      },
      loginDemoCustomer: async () => {
        const repo = getAuthRepository();
        const user = await repo.loginDemoCustomer();
        set({ user, rememberMe: true });
        return user;
      },
      loginDemoAdmin: async () => {
        const repo = getAuthRepository();
        const user = await repo.loginDemoAdmin();
        set({ user, rememberMe: true });
        return user;
      },
      register: async (input) => {
        const repo = getAuthRepository();
        const user = await repo.register(input);
        set({ user, rememberMe: true });
        return user;
      },
      logout: async () => {
        const repo = getAuthRepository();
        await repo.logout();
        sessionService.clear();
        set({ user: null });
      },
      updateProfile: (partial) => {
        const current = get().user;
        if (!current) return;
        set({ user: { ...current, ...partial } });
      },
      isAdmin: () => get().user?.role?.toLowerCase() === "admin",
      isAuthenticated: () => Boolean(get().user),
    }),
    {
      name: STORAGE_KEYS.auth,
      onRehydrateStorage: () => (state) => {
        state?.setHydrated(true);
      },
      partialize: (state) => ({
        user: state.user,
        rememberMe: state.rememberMe,
      }),
    },
  ),
);
