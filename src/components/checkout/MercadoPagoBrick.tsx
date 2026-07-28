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
  onPaid?: () => void;
  onPending?: (info: { qrCode?: string | null; qrCodeBase64?: string | null }) => void;
};

/**
 * Payment Brick oficial. Nunca envia PAN/CVV ao nosso backend — só o token.
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
  const publicKey = getMercadoPagoPublicKey();

  const ready = useMemo(() => {
    if (!publicKey) return false;
    initMercadoPago(publicKey, { locale: "pt-BR" });
    return true;
  }, [publicKey]);

  const configError = publicKey
    ? null
    : "Public Key do Mercado Pago não configurada.";

  const customization = useMemo(
    () => ({
      paymentMethods: {
        maxInstallments: 2,
        minInstallments: 1,
        creditCard: "all" as const,
        bankTransfer: ["pix"],
        ticket: "all" as const,
      },
    }),
    [],
  );

  const handleSubmit = useCallback(
    async (param: { formData?: Record<string, unknown> } & Record<string, unknown>) => {
      setSubmitError(null);
      const formData = (param.formData ?? param) as Record<string, unknown>;
      try {
        const paymentMethodId = String(formData.payment_method_id ?? "");
        const token =
          typeof formData.token === "string" ? formData.token : undefined;
        const installmentsRaw = formData.installments;
        const installments =
          typeof installmentsRaw === "number"
            ? installmentsRaw
            : installmentsRaw
              ? Number(installmentsRaw)
              : undefined;
        const issuerId =
          formData.issuer_id != null ? String(formData.issuer_id) : undefined;

        const result = await paymentsApi.createForOrder(
          orderId,
          {
            token,
            paymentMethodId,
            installments:
              installments && installments >= 1 && installments <= 2
                ? installments
                : 1,
            issuerId,
            payerEmail,
          },
          createIdempotencyKey(),
        );

        if (result.status === "approved") {
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
            "Pix gerado. Conclua o pagamento para confirmar o pedido.",
          );
          return;
        }

        push("info", result.message || "Pagamento em processamento.");
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
      }
    },
    [orderId, onPaid, onPending, payerEmail, push],
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
        Total a pagar:{" "}
        <span className="font-medium text-esotera-text">
          {formatCurrency(amount)}
        </span>
        . Cartão em até 2x sem juros ou Pix.
      </p>
      {submitError ? (
        <p className="text-sm text-esotera-error" role="alert">
          {submitError}
        </p>
      ) : null}
      <Payment
        initialization={{
          amount,
          payer: payerEmail ? { email: payerEmail } : undefined,
        }}
        customization={customization}
        onSubmit={handleSubmit as never}
      />
    </div>
  );
}
