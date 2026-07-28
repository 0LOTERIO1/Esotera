"use client";

import { shippingOrigin } from "@/config/shipping";
import { useSettingsStore } from "@/stores/settingsStore";
import { formatCurrency } from "@/utils/format";

export function ShippingInfoSection() {
  const settings = useSettingsStore((s) => s.settings);
  const statesLabel = settings.freeShippingStates.join(", ");

  return (
    <section className="border-y border-esotera-border bg-esotera-surface-secondary py-10">
      <div className="mx-auto max-w-6xl px-4 sm:px-6">
        <h2 className="font-serif text-2xl text-esotera-secondary sm:text-3xl">
          Entrega
        </h2>
        <p className="mt-3 max-w-2xl text-sm text-esotera-muted">
          Enviamos a partir de {shippingOrigin.region}, {shippingOrigin.city}{" "}
          (CEP {shippingOrigin.cep}). Embalagem de referência:{" "}
          {shippingOrigin.package.widthCm} × {shippingOrigin.package.heightCm} ×{" "}
          {shippingOrigin.package.lengthCm} cm · {shippingOrigin.package.weightGrams}{" "}
          g.
        </p>
        <p className="mt-3 max-w-2xl text-sm text-esotera-muted">
          Frete grátis a partir de {formatCurrency(settings.freeShippingMin)}{" "}
          (após desconto) para {statesLabel || "estados configurados"}.
          Modalidade J3 simulada (a partir de {formatCurrency(settings.j3Price)}
          ) para CEPs elegíveis em São Paulo.
        </p>
      </div>
    </section>
  );
}
