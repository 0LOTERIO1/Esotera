"use client";

import { useCallback, useId, useRef, useState } from "react";
import { CreditCard, Barcode, QrCode } from "lucide-react";
import { paymentsApi } from "@/services/api/paymentsApi";
import { createIdempotencyKey } from "@/utils/orderIdempotency";
import { formatCurrency } from "@/utils/format";
import { ApiError } from "@/services/api/apiClient";

type MethodId = "card" | "pix" | "boleto";

type SandboxPaymentCheckoutProps = {
  orderNumber: string;
  orderTotal: number;
  sandboxAmount: number;
};

/**
 * Interface visual semelhante ao Payment Brick do Mercado Pago,
 * exclusiva para ambiente Test. Só chama POST /api/payments/sandbox/pix-test.
 * Não inicializa SDK/Brick comercial e não altera o pedido.
 */
export function SandboxPaymentCheckout({
  orderNumber,
  orderTotal,
  sandboxAmount,
}: SandboxPaymentCheckoutProps) {
  const groupId = useId();
  const [method, setMethod] = useState<MethodId>("pix");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);
  const [qrCode, setQrCode] = useState<string | null>(null);
  const [qrBase64, setQrBase64] = useState<string | null>(null);
  const [txAmount, setTxAmount] = useState<number | null>(null);
  const inFlight = useRef(false);

  const handleGenerate = useCallback(async () => {
    if (inFlight.current || loading || method !== "pix") return;
    inFlight.current = true;
    setLoading(true);
    setError(null);
    setCopied(false);
    setQrCode(null);
    setQrBase64(null);
    setTxAmount(null);
    try {
      const result = await paymentsApi.createSandboxPixTest(
        createIdempotencyKey(),
      );
      setQrCode(result.qrCode ?? null);
      setQrBase64(result.qrCodeBase64 ?? null);
      setTxAmount(result.amount);
      if (!result.qrCode && !result.qrCodeBase64) {
        setError(
          result.message ||
            "Pix de teste criado, mas o QR Code não veio na resposta.",
        );
      }
    } catch (err) {
      setError(
        err instanceof ApiError
          ? err.userMessage
          : err instanceof Error
            ? err.message
            : "Falha ao gerar Pix de teste.",
      );
    } finally {
      inFlight.current = false;
      setLoading(false);
    }
  }, [loading, method]);

  const handleCopy = useCallback(async () => {
    if (!qrCode) return;
    try {
      await navigator.clipboard.writeText(qrCode);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 2000);
    } catch {
      setError("Não foi possível copiar o código. Selecione e copie manualmente.");
    }
  }, [qrCode]);

  const methods: Array<{
    id: MethodId;
    label: string;
    icon: typeof QrCode;
    soon: boolean;
  }> = [
    { id: "card", label: "Cartão", icon: CreditCard, soon: true },
    { id: "pix", label: "Pix", icon: QrCode, soon: false },
    { id: "boleto", label: "Boleto", icon: Barcode, soon: true },
  ];

  return (
    <div className="overflow-hidden rounded-xl border border-esotera-border bg-esotera-surface shadow-sm">
      <div className="border-b border-esotera-border bg-esotera-surface-secondary/60 px-4 py-3 sm:px-5">
        <p className="text-xs font-medium uppercase tracking-wide text-esotera-muted">
          Resumo do pagamento
        </p>
        <p className="mt-1 text-sm text-esotera-muted">Pedido {orderNumber}</p>
        <dl className="mt-3 space-y-2 text-sm">
          <div className="flex items-baseline justify-between gap-3">
            <dt className="text-esotera-text">Total verdadeiro do pedido</dt>
            <dd className="font-semibold text-esotera-secondary tabular-nums">
              {formatCurrency(orderTotal)}
            </dd>
          </div>
          <div className="flex items-baseline justify-between gap-3">
            <dt className="text-esotera-muted">
              Valor da transação sandbox do Pix
            </dt>
            <dd className="font-medium text-esotera-primary tabular-nums">
              {formatCurrency(sandboxAmount)}
            </dd>
          </div>
        </dl>
      </div>

      <div className="px-4 py-4 sm:px-5">
        <fieldset>
          <legend className="text-sm font-medium text-esotera-text">
            Forma de pagamento
          </legend>
          <div
            className="mt-3 grid grid-cols-3 gap-2"
            role="radiogroup"
            aria-label="Forma de pagamento"
          >
            {methods.map(({ id, label, icon: Icon, soon }) => {
              const selected = method === id;
              const inputId = `${groupId}-${id}`;
              return (
                <label
                  key={id}
                  htmlFor={inputId}
                  className={[
                    "relative flex min-h-[4.5rem] flex-col items-center justify-center gap-1.5 rounded-lg border px-2 py-3 text-center transition",
                    soon
                      ? "cursor-not-allowed border-esotera-border bg-esotera-background/80 opacity-55"
                      : selected
                        ? "cursor-pointer border-esotera-primary bg-esotera-primary/10 ring-2 ring-esotera-primary/30"
                        : "cursor-pointer border-esotera-border bg-esotera-surface hover:border-esotera-primary/50",
                    "focus-within:outline-none focus-within:ring-2 focus-within:ring-esotera-primary focus-within:ring-offset-2",
                  ].join(" ")}
                >
                  <input
                    id={inputId}
                    type="radio"
                    name={`${groupId}-method`}
                    value={id}
                    className="sr-only"
                    checked={selected}
                    disabled={soon}
                    onChange={() => {
                      if (!soon) setMethod(id);
                    }}
                  />
                  <Icon
                    className="h-5 w-5 text-esotera-secondary"
                    aria-hidden
                  />
                  <span className="text-xs font-medium text-esotera-text sm:text-sm">
                    {label}
                  </span>
                  {soon ? (
                    <span className="absolute right-1 top-1 rounded bg-esotera-surface-secondary px-1.5 py-0.5 text-[10px] font-medium text-esotera-muted">
                      Em breve
                    </span>
                  ) : null}
                </label>
              );
            })}
          </div>
        </fieldset>

        {method === "pix" ? (
          <div className="mt-5 space-y-4">
            <div className="rounded-lg border border-esotera-border bg-esotera-background/70 px-3 py-3 text-sm text-esotera-muted">
              <p className="font-medium text-esotera-text">Pix — teste controlado</p>
              <p className="mt-1">
                Gera uma transação oficial de sandbox de{" "}
                {formatCurrency(sandboxAmount)}. Este valor{" "}
                <strong className="font-medium text-esotera-text">não</strong> é
                o total do pedido ({formatCurrency(orderTotal)}), não altera o
                pedido, não consome cupom e não marca como pago.
              </p>
            </div>

            <button
              type="button"
              disabled={loading}
              aria-busy={loading}
              onClick={() => void handleGenerate()}
              className="w-full rounded-lg bg-[#009ee3] px-4 py-3.5 text-sm font-semibold text-white shadow-sm transition hover:bg-[#0088c6] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#009ee3] focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {loading
                ? "Gerando Pix de teste…"
                : `Gerar Pix de teste de ${formatCurrency(sandboxAmount)}`}
            </button>

            {error ? (
              <p className="text-sm text-esotera-error" role="alert">
                {error}
              </p>
            ) : null}

            {qrBase64 || qrCode ? (
              <div className="space-y-3 rounded-lg border border-esotera-border p-4">
                <p className="text-sm font-medium text-esotera-text">
                  Pix de teste
                  {txAmount != null
                    ? ` · ${formatCurrency(txAmount)}`
                    : ""}{" "}
                  <span className="font-normal text-esotera-muted">
                    (não é o total do pedido)
                  </span>
                </p>
                {qrBase64 ? (
                  // eslint-disable-next-line @next/next/no-img-element
                  <img
                    src={
                      qrBase64.startsWith("data:")
                        ? qrBase64
                        : `data:image/png;base64,${qrBase64}`
                    }
                    alt="QR Code Pix de teste"
                    className="mx-auto h-48 w-48 rounded-md bg-white p-2"
                  />
                ) : null}
                {qrCode ? (
                  <div>
                    <label
                      htmlFor={`${groupId}-pix-code`}
                      className="text-xs text-esotera-muted"
                    >
                      Pix copia e cola
                    </label>
                    <textarea
                      id={`${groupId}-pix-code`}
                      readOnly
                      rows={4}
                      value={qrCode}
                      className="mt-1 w-full rounded-md border border-esotera-border bg-esotera-surface p-2 font-mono text-xs text-esotera-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-esotera-primary"
                    />
                    <button
                      type="button"
                      onClick={() => void handleCopy()}
                      className="mt-2 rounded-md border border-esotera-border bg-esotera-surface px-3 py-2 text-sm font-medium text-esotera-primary transition hover:bg-esotera-surface-secondary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-esotera-primary focus-visible:ring-offset-2"
                    >
                      {copied ? "Código copiado" : "Copiar código"}
                    </button>
                  </div>
                ) : null}
              </div>
            ) : null}
          </div>
        ) : null}

        {method === "card" || method === "boleto" ? (
          <p className="mt-5 text-sm text-esotera-muted" role="status">
            Esta modalidade estará disponível em breve. Use Pix para o teste
            controlado de sandbox.
          </p>
        ) : null}
      </div>
    </div>
  );
}
