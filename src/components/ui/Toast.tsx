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
          className={`flex items-start gap-3 rounded-md border px-4 py-3 text-sm shadow-md ${
            toast.type === "success"
              ? "border-esotera-success/35 bg-esotera-surface text-esotera-text"
              : toast.type === "error"
                ? "border-esotera-error/40 bg-esotera-surface text-esotera-text"
                : "border-esotera-border bg-esotera-surface text-esotera-text"
          }`}
        >
          <p className="flex-1">{toast.message}</p>
          <button
            type="button"
            onClick={() => dismiss(toast.id)}
            aria-label="Fechar notificação"
            className="text-esotera-muted hover:text-esotera-secondary"
          >
            <X size={16} />
          </button>
        </div>
      ))}
    </div>
  );
}
