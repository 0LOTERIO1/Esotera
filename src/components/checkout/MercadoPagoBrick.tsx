"use client";

import { useCallback, useMemo, useState } from "react";
import { initMercadoPago, Payment } from "@mercadopago/sdk-react";
import { getMercadoPagoPublicKey } from "@/config/mercadoPago";
import { paymentsApi } from "@/services/api/paymentsApi";
import { createIdempotencyKey } from "@/utils/orderIdempotency";
import { formatCurrency } from "@/utils/format";
import { ApiError } from "@/services/api/apiClient";
import { useToastStore } from "@/stores/toastStore";

type MercadoPagoBrickProps = {
  orderId: string;
  amount: number;
  payerEmail?: string;
  isTestEnvironment?: boolean;
  onPaid?: () => void;
  onPending?: (info: {
    qrCode?: string | null;
    qrCodeBase64?: string | null;
  }) => void;
};

/**
 * Payment Brick — somente produção / checkout comercial Pix.
 * Em sandbox Test o fluxo isolado vive na página de pagamento (sem Brick).
 */
export function MercadoPagoBrick({
  orderId,
  amount,
  payerEmail,
  onPaid,
  onPending,
}: MercadoPagoBrickProps) {
  const push = useToastStore((s) => s.push);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [brickError, setBrickError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const publicKey = getMercadoPagoPublicKey();

  const ready = useMemo(() => {
    if (!publicKey) return false;
    initMercadoPago(publicKey, { locale: "pt-BR" });
    return true;
  }, [publicKey]);

  const configError = publicKey
    ? null
    : "Public Key do Mercado Pago não configurada.";

  // Somente Pix: AllOrArray = "all" | string[] — "none" NÃO é válido (422 no init).
  const customization = useMemo(
    () => ({
      paymentMethods: {
        bankTransfer: ["pix"] as string[],
        maxInstallments: 1,
        minInstallments: 1,
      },
      visual: {
        hidePaymentButton: false,
      },
    }),
    [],
  );

  const handleBrickError = useCallback(
    (err: { type?: string; message?: string; cause?: string }) => {
      const type = typeof err?.type === "string" ? err.type : "unknown";
      const cause = typeof err?.cause === "string" ? err.cause.slice(0, 120) : undefined;
      const message =
        typeof err?.message === "string" ? err.message.slice(0, 200) : undefined;
      console.error("Mercado Pago Payment Brick error", { type, cause, message });
      setBrickError(
        "Não foi possível carregar o checkout Pix. Tente novamente em instantes.",
      );
    },
    [],
  );

  const handleBrickReady = useCallback(() => {
    setBrickError(null);
  }, []);

  const handleSubmit = useCallback(
    async (
      param: { formData?: Record<string, unknown> } & Record<string, unknown>,
    ) => {
      if (submitting) {
        throw new Error("Pagamento em andamento. Aguarde.");
      }
      setSubmitting(true);
      setSubmitError(null);
      const formData = (param.formData ?? param) as Record<string, unknown>;
      try {
        const paymentMethodId = String(
          formData.payment_method_id ?? "",
        ).toLowerCase();
        if (paymentMethodId !== "pix") {
          const message =
            "Nesta fase somente Pix está disponível. Cartão e boleto em breve.";
          setSubmitError(message);
          push("error", message);
          throw new Error(message);
        }

        const result = await paymentsApi.createForOrder(
          orderId,
          {
            paymentMethodId: "pix",
            payerEmail,
          },
          createIdempotencyKey(),
        );

        if (result.status === "approved" || result.status === "processed") {
          push("success", "Pagamento aprovado.");
          onPaid?.();
          return;
        }

        if (result.qrCode || result.qrCodeBase64) {
          onPending?.({
            qrCode: result.qrCode,
            qrCodeBase64: result.qrCodeBase64,
          });
          push(
            "info",
            result.message ||
              "Aguardando pagamento. Conclua o Pix para confirmar o pedido.",
          );
          return;
        }

        push("info", result.message || "Aguardando pagamento.");
        onPending?.({});
      } catch (err) {
        const message =
          err instanceof ApiError
            ? err.userMessage
            : err instanceof Error
              ? err.message
              : "Falha ao processar pagamento.";
        setSubmitError(message);
        push("error", message);
        throw err;
      } finally {
        setSubmitting(false);
      }
    },
    [orderId, onPaid, onPending, payerEmail, push, submitting],
  );

  if (!ready) {
    return (
      <p className="text-sm text-esotera-muted">
        {configError ?? "Carregando pagamento seguro…"}
      </p>
    );
  }

  return (
    <div className="space-y-3">
      <p className="text-sm text-esotera-muted">
        Total do pedido:{" "}
        <span className="font-medium text-esotera-text">
          {formatCurrency(amount)}
        </span>
        . Pagamento comercial somente via Pix.
      </p>
      <ul className="text-sm text-esotera-muted">
        <li>Pix — disponível</li>
        <li>Cartão — Em breve</li>
        <li>Boleto — Em breve</li>
      </ul>
      {submitError ? (
        <p className="text-sm text-esotera-error" role="alert">
          {submitError}
        </p>
      ) : null}
      {brickError ? (
        <p className="text-sm text-esotera-error" role="alert">
          {brickError}
        </p>
      ) : null}
      <div className={submitting ? "pointer-events-none opacity-60" : undefined}>
        <Payment
          initialization={{
            amount,
            payer: payerEmail ? { email: payerEmail } : undefined,
          }}
          customization={customization}
          onSubmit={handleSubmit as never}
          onReady={handleBrickReady}
          onError={handleBrickError}
        />
      </div>
    </div>
  );
}
