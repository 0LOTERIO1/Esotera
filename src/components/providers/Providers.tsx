"use client";

import { useStoreHydration } from "@/hooks/useStoreHydration";
import { ToastViewport } from "@/components/ui/Toast";
import { BrandLogo } from "@/components/brand/BrandLogo";

export function Providers({ children }: { children: React.ReactNode }) {
  const ready = useStoreHydration();

  if (!ready) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-esotera-background text-esotera-text">
        <div className="text-center">
          <BrandLogo variant="dark" href={null} className="mx-auto" />
          <p className="mt-3 text-sm text-esotera-muted">Carregando…</p>
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
