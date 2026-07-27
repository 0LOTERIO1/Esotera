"use client";

import { create } from "zustand";
import { persist } from "zustand/middleware";
import { STORAGE_KEYS } from "@/utils/storage";
import { mockAuthService, type RegisterInput } from "@/services/auth/mockAuthService";
import type { User } from "@/types";

type AuthState = {
  user: User | null;
  rememberMe: boolean;
  hydrated: boolean;
  setHydrated: (value: boolean) => void;
  login: (email: string, password: string, remember?: boolean) => User;
  loginDemoCustomer: () => User;
  loginDemoAdmin: () => User;
  register: (input: RegisterInput) => User;
  logout: () => void;
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
      setHydrated: (value) => set({ hydrated: value }),
      login: (email, password, remember = false) => {
        const user = mockAuthService.login(email, password);
        set({ user, rememberMe: remember });
        return user;
      },
      loginDemoCustomer: () => {
        const user = mockAuthService.loginAsDemoCustomer();
        set({ user, rememberMe: true });
        return user;
      },
      loginDemoAdmin: () => {
        const user = mockAuthService.loginAsDemoAdmin();
        set({ user, rememberMe: true });
        return user;
      },
      register: (input) => {
        const user = mockAuthService.register(input);
        set({ user, rememberMe: true });
        return user;
      },
      logout: () => set({ user: null }),
      updateProfile: (partial) => {
        const current = get().user;
        if (!current) return;
        set({ user: { ...current, ...partial } });
      },
      isAdmin: () => get().user?.role === "admin",
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
