"use client";

import type { ShippingOption } from "@/types";
import { Price } from "@/components/ui/Price";
import { Button } from "@/components/ui/Button";
import { formatCurrency } from "@/utils/format";

type ShippingOptionsProps = {
  options: ShippingOption[];
  selectedId?: string | null;
  onSelect: (option: ShippingOption) => void;
  freeShippingHint?: string;
  loading?: boolean;
  error?: string | null;
  onRetry?: () => void;
};

export function ShippingOptions({
  options,
  selectedId,
  onSelect,
  freeShippingHint,
  loading = false,
  error = null,
  onRetry,
}: ShippingOptionsProps) {
  if (loading) {
    return (
      <p className="text-sm text-esotera-muted" role="status">
        Calculando frete…
      </p>
    );
  }

  if (error) {
    return (
      <div className="space-y-3">
        <p role="alert" className="text-sm text-esotera-error">
          {error}
        </p>
        {onRetry ? (
          <Button type="button" variant="secondary" onClick={onRetry}>
            Tentar novamente
          </Button>
        ) : null}
      </div>
    );
  }

  if (!options.length) {
    return (
      <div className="space-y-3">
        <p className="text-sm text-esotera-muted">
          Nenhuma modalidade de entrega disponível para este endereço.
        </p>
        {onRetry ? (
          <Button type="button" variant="secondary" onClick={onRetry}>
            Tentar novamente
          </Button>
        ) : null}
      </div>
    );
  }

  return (
    <fieldset className="space-y-3">
      <legend className="text-sm font-medium text-esotera-text">
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
              className={`flex cursor-pointer items-start gap-3 rounded-md border p-3 transition focus-within:outline focus-within:outline-2 focus-within:outline-offset-2 focus-within:outline-esotera-primary ${
                selected
                  ? "border-esotera-primary bg-esotera-primary/5"
                  : "border-esotera-border hover:border-esotera-muted"
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
                  <span className="text-sm text-esotera-text">
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
