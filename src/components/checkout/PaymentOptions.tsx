"use client";

import { useState } from "react";
import type { PaymentMethod } from "@/types";
import { storeConfig } from "@/config/store";
import { FormField, inputClassName } from "@/components/ui/FormField";
import { maskCardExpiry, maskCardNumber } from "@/utils/masks";
import { formatCurrency } from "@/utils/format";

type PaymentOptionsProps = {
  method: PaymentMethod;
  installments: number;
  total: number;
  onMethodChange: (method: PaymentMethod) => void;
  onInstallmentsChange: (value: number) => void;
};

export function PaymentOptions({
  method,
  installments,
  total,
  onMethodChange,
  onInstallmentsChange,
}: PaymentOptionsProps) {
  const [cardNumber, setCardNumber] = useState("");
  const [cardName, setCardName] = useState("");
  const [cardExpiry, setCardExpiry] = useState("");
  const [cardCvv, setCardCvv] = useState("");

  return (
    <div className="space-y-4">
      <div
        role="status"
        className="rounded-md border border-esotera-gold/40 bg-esotera-gold/10 px-4 py-3 text-sm text-esotera-gold-soft"
      >
        {storeConfig.demoNotice}
      </div>

      <fieldset className="space-y-2">
        <legend className="text-sm font-medium text-esotera-beige">
          Forma de pagamento
        </legend>
        {(
          [
            ["pix", "Pix"],
            ["card", "Cartão de crédito"],
            ["boleto", "Boleto"],
          ] as const
        ).map(([value, label]) => (
          <label
            key={value}
            className={`flex cursor-pointer items-center gap-3 rounded-md border p-3 ${
              method === value
                ? "border-esotera-gold bg-esotera-gold/5"
                : "border-esotera-graphite"
            }`}
          >
            <input
              type="radio"
              name="payment"
              value={value}
              checked={method === value}
              onChange={() => onMethodChange(value)}
            />
            <span className="text-sm text-esotera-beige">{label}</span>
          </label>
        ))}
      </fieldset>

      {method === "pix" ? (
        <div className="rounded-md border border-esotera-graphite p-4 text-sm text-esotera-muted">
          <p>QR Code fictício — simulação apenas.</p>
          <div
            className="mx-auto mt-4 flex h-40 w-40 items-center justify-center border border-dashed border-esotera-gold/40 bg-esotera-black/40 text-center text-xs text-esotera-gold"
            aria-hidden
          >
            QR Code
            <br />
            demonstração
          </div>
          <p className="mt-3">Sem desconto adicional no Pix neste protótipo.</p>
        </div>
      ) : null}

      {method === "card" ? (
        <div className="grid gap-3 sm:grid-cols-2">
          <FormField label="Número do cartão" id="card-number">
            <input
              id="card-number"
              className={inputClassName}
              value={cardNumber}
              onChange={(e) => setCardNumber(maskCardNumber(e.target.value))}
              placeholder="0000 0000 0000 0000"
              autoComplete="off"
            />
          </FormField>
          <FormField label="Nome no cartão" id="card-name">
            <input
              id="card-name"
              className={inputClassName}
              value={cardName}
              onChange={(e) => setCardName(e.target.value)}
              autoComplete="off"
            />
          </FormField>
          <FormField label="Validade" id="card-expiry">
            <input
              id="card-expiry"
              className={inputClassName}
              value={cardExpiry}
              onChange={(e) => setCardExpiry(maskCardExpiry(e.target.value))}
              placeholder="MM/AA"
              autoComplete="off"
            />
          </FormField>
          <FormField label="CVV" id="card-cvv">
            <input
              id="card-cvv"
              className={inputClassName}
              value={cardCvv}
              onChange={(e) =>
                setCardCvv(e.target.value.replace(/\D/g, "").slice(0, 4))
              }
              autoComplete="off"
            />
          </FormField>
          <FormField label="Parcelas" id="card-installments">
            <select
              id="card-installments"
              className={inputClassName}
              value={installments}
              onChange={(e) => onInstallmentsChange(Number(e.target.value))}
            >
              <option value={1}>1x de {formatCurrency(total)} sem juros</option>
              <option value={2}>
                2x de {formatCurrency(total / 2)} sem juros
              </option>
            </select>
          </FormField>
          <p className="sm:col-span-2 text-xs text-esotera-muted">
            Os dados do cartão são apenas visuais e não são armazenados nem
            enviados.
          </p>
        </div>
      ) : null}

      {method === "boleto" ? (
        <div className="rounded-md border border-esotera-graphite p-4 text-sm text-esotera-muted">
          Será gerado um boleto fictício sem validade real ao finalizar o
          pedido.
        </div>
      ) : null}
    </div>
  );
}
