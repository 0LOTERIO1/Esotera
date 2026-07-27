"use client";

import { create } from "zustand";
import { generateId } from "@/utils/format";
import type { ToastMessage } from "@/types";

type ToastState = {
  toasts: ToastMessage[];
  push: (type: ToastMessage["type"], message: string) => void;
  dismiss: (id: string) => void;
};

export const useToastStore = create<ToastState>((set) => ({
  toasts: [],
  push: (type, message) => {
    const id = generateId("toast");
    set((state) => ({
      toasts: [...state.toasts, { id, type, message }],
    }));
    setTimeout(() => {
      set((state) => ({
        toasts: state.toasts.filter((t) => t.id !== id),
      }));
    }, 4000);
  },
  dismiss: (id) =>
    set((state) => ({
      toasts: state.toasts.filter((t) => t.id !== id),
    })),
}));
