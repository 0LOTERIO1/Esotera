"use client";

import { useToastStore } from "@/stores/toastStore";
import { X } from "lucide-react";

export function ToastViewport() {
  const toasts = useToastStore((s) => s.toasts);
  const dismiss = useToastStore((s) => s.dismiss);

  if (!toasts.length) return null;

  return (
    <div
      className="fixed bottom-4 right-4 z-[100] flex w-[min(100%-2rem,22rem)] flex-col gap-2"
      aria-live="polite"
      aria-relevant="additions"
    >
      {toasts.map((toast) => (
        <div
          key={toast.id}
          role="status"
          className={`flex items-start gap-3 rounded-md border px-4 py-3 text-sm shadow-lg backdrop-blur ${
            toast.type === "success"
              ? "border-esotera-success/40 bg-esotera-navy/95 text-esotera-beige"
              : toast.type === "error"
                ? "border-esotera-error/50 bg-esotera-navy/95 text-esotera-beige"
                : "border-esotera-gold/30 bg-esotera-navy/95 text-esotera-beige"
          }`}
        >
          <p className="flex-1">{toast.message}</p>
          <button
            type="button"
            onClick={() => dismiss(toast.id)}
            aria-label="Fechar notificação"
            className="text-esotera-muted hover:text-esotera-white"
          >
            <X size={16} />
          </button>
        </div>
      ))}
    </div>
  );
}
