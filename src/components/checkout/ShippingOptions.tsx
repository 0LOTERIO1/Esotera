"use client";

import type { ShippingOption } from "@/types";
import { Price } from "@/components/ui/Price";
import { formatCurrency } from "@/utils/format";

type ShippingOptionsProps = {
  options: ShippingOption[];
  selectedId?: string;
  onSelect: (option: ShippingOption) => void;
  freeShippingHint?: string;
};

export function ShippingOptions({
  options,
  selectedId,
  onSelect,
  freeShippingHint,
}: ShippingOptionsProps) {
  if (!options.length) {
    return (
      <p className="text-sm text-esotera-muted">
        Informe um CEP válido para calcular o frete.
      </p>
    );
  }

  return (
    <fieldset className="space-y-3">
      <legend className="text-sm font-medium text-esotera-beige">
        Modalidades de entrega
      </legend>
      {freeShippingHint ? (
        <p className="text-xs text-esotera-success">{freeShippingHint}</p>
      ) : null}
      <p className="text-xs text-esotera-muted">
        Cobertura J3 simulada — não representa a cobertura oficial.
      </p>
      <div className="space-y-2">
        {options.map((option) => {
          const selected = selectedId === option.id;
          return (
            <label
              key={option.id}
              className={`flex cursor-pointer items-start gap-3 rounded-md border p-3 transition ${
                selected
                  ? "border-esotera-gold bg-esotera-gold/5"
                  : "border-esotera-graphite hover:border-esotera-muted"
              }`}
            >
              <input
                type="radio"
                name="shipping"
                value={option.id}
                checked={selected}
                onChange={() => onSelect(option)}
                className="mt-1"
              />
              <span className="flex-1">
                <span className="flex flex-wrap items-baseline justify-between gap-2">
                  <span className="text-sm text-esotera-beige">
                    {option.provider} — {option.name}
                  </span>
                  <span className="text-sm">
                    {option.price === 0 ? (
                      <span className="text-esotera-success">Grátis</span>
                    ) : (
                      <Price value={option.price} />
                    )}
                    {option.originalPrice > option.price ? (
                      <span className="ml-2 text-xs text-esotera-muted line-through">
                        {formatCurrency(option.originalPrice)}
                      </span>
                    ) : null}
                  </span>
                </span>
                <span className="mt-1 block text-xs text-esotera-muted">
                  {option.estimatedDays}. {option.description}
                </span>
              </span>
            </label>
          );
        })}
      </div>
    </fieldset>
  );
}
