"use client";

import { useStoreHydration } from "@/hooks/useStoreHydration";
import { ToastViewport } from "@/components/ui/Toast";

export function Providers({ children }: { children: React.ReactNode }) {
  const ready = useStoreHydration();

  if (!ready) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-esotera-navy text-esotera-beige">
        <div className="text-center">
          <p className="font-serif text-2xl text-esotera-gold">Esotera</p>
          <p className="mt-2 text-sm text-esotera-muted">Carregando…</p>
        </div>
      </div>
    );
  }

  return (
    <>
      {children}
      <ToastViewport />
    </>
  );
}
