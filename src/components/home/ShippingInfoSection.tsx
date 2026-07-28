"use client";

import { useSettingsStore } from "@/stores/settingsStore";
import { formatCurrency } from "@/utils/format";

export function ShippingInfoSection() {
  const settings = useSettingsStore((s) => s.settings);

  return (
    <section className="border-y border-esotera-border bg-esotera-surface-secondary py-10">
      <div className="mx-auto max-w-6xl px-4 sm:px-6">
        <h2 className="font-serif text-2xl text-esotera-secondary sm:text-3xl">
          Entrega para todo o Brasil
        </h2>
        <p className="mt-3 max-w-2xl text-sm text-esotera-muted">
          Enviamos seus pedidos para todo o Brasil com opções de entrega
          apresentadas durante a compra. O prazo e o valor do frete são
          calculados de acordo com o CEP informado.
        </p>
        <p className="mt-3 max-w-2xl text-sm text-esotera-muted">
          Frete grátis em compras a partir de{" "}
          {formatCurrency(settings.freeShippingMin)} para destinos elegíveis
          das regiões Sul e Sudeste.
        </p>
        <p className="mt-3 max-w-2xl text-sm text-esotera-muted">
          Clientes de regiões elegíveis de São Paulo também poderão visualizar
          a opção de entrega no mesmo dia durante o checkout.
        </p>
      </div>
    </section>
  );
}
