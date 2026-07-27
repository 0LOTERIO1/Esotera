"use client";

import { Minus, Plus } from "lucide-react";

type QuantitySelectorProps = {
  value: number;
  onChange: (value: number) => void;
  min?: number;
  max?: number;
  disabled?: boolean;
  id?: string;
};

export function QuantitySelector({
  value,
  onChange,
  min = 1,
  max = 99,
  disabled,
  id,
}: QuantitySelectorProps) {
  return (
    <div className="inline-flex items-center rounded-md border border-esotera-graphite bg-esotera-black/40">
      <button
        type="button"
        aria-label="Diminuir quantidade"
        disabled={disabled || value <= min}
        onClick={() => onChange(Math.max(min, value - 1))}
        className="flex h-11 w-11 items-center justify-center text-esotera-beige disabled:opacity-40"
      >
        <Minus size={16} />
      </button>
      <input
        id={id}
        type="number"
        inputMode="numeric"
        aria-label="Quantidade"
        disabled={disabled}
        min={min}
        max={max}
        value={value}
        onChange={(e) => {
          const next = Number(e.target.value);
          if (Number.isNaN(next)) return;
          onChange(Math.min(max, Math.max(min, next)));
        }}
        className="h-11 w-12 border-x border-esotera-graphite bg-transparent text-center text-sm text-esotera-white [appearance:textfield] [&::-webkit-inner-spin-button]:appearance-none [&::-webkit-outer-spin-button]:appearance-none"
      />
      <button
        type="button"
        aria-label="Aumentar quantidade"
        disabled={disabled || value >= max}
        onClick={() => onChange(Math.min(max, value + 1))}
        className="flex h-11 w-11 items-center justify-center text-esotera-beige disabled:opacity-40"
      >
        <Plus size={16} />
      </button>
    </div>
  );
}
