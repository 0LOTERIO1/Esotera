"use client";

import { useState } from "react";
import type { PaymentMethod } from "@/types";
import { FormField, inputClassName } from "@/components/ui/FormField";
import { maskCardExpiry, maskCardNumber } from "@/utils/masks";
import { formatCurrency } from "@/utils/format";
import {
  canCompleteCheckoutWithoutRealPayment,
  isRealPaymentEnabled,
} from "@/config/storeMode";

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
  const paymentsLive = isRealPaymentEnabled();
  const allowTestingCheckout = canCompleteCheckoutWithoutRealPayment();

  if (!paymentsLive && !allowTestingCheckout) {
    return (
      <div
        role="status"
        className="rounded-md border border-esotera-border bg-esotera-surface-secondary px-4 py-5 text-sm text-esotera-muted"
      >
        <p className="font-medium text-esotera-text">Pagamento em preparação</p>
        <p className="mt-2">
          Em breve você poderá finalizar sua compra com Pix, cartão ou boleto
          de forma segura. Enquanto isso, explore o catálogo e prepare seu
          carrinho.
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {!paymentsLive && allowTestingCheckout ? (
        <div
          role="status"
          className="rounded-md border border-esotera-border bg-esotera-surface-secondary px-4 py-3 text-sm text-esotera-muted"
        >
          Ambiente de homologação: o pedido será registrado para testes
          internos. A cobrança real será ativada com a integração de pagamento.
        </div>
      ) : null}

      <fieldset className="space-y-2">
        <legend className="text-sm font-medium text-esotera-text">
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
                ? "border-esotera-primary bg-esotera-primary/5"
                : "border-esotera-border"
            }`}
          >
            <input
              type="radio"
              name="payment"
              value={value}
              checked={method === value}
              onChange={() => onMethodChange(value)}
            />
            <span className="text-sm text-esotera-text">{label}</span>
          </label>
        ))}
      </fieldset>

      {method === "pix" ? (
        <div className="rounded-md border border-esotera-border p-4 text-sm text-esotera-muted">
          <p>
            Ao finalizar, você receberá as instruções de pagamento Pix na
            confirmação do pedido.
          </p>
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
            Os dados do cartão serão processados de forma segura quando o
            pagamento estiver integrado.
          </p>
        </div>
      ) : null}

      {method === "boleto" ? (
        <div className="rounded-md border border-esotera-border p-4 text-sm text-esotera-muted">
          Ao finalizar, você receberá o boleto com as instruções de pagamento.
        </div>
      ) : null}
    </div>
  );
}
