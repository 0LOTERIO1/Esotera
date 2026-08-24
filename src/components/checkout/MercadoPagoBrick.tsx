"use client";

import { useCallback, useMemo, useRef, useState } from "react";
import { initMercadoPago, Payment } from "@mercadopago/sdk-react";
import type { IPaymentBrickCustomization } from "@mercadopago/sdk-react/esm/bricks/payment/type";
import { getMercadoPagoPublicKey } from "@/config/mercadoPago";
import {
  paymentsApi,
  type CreatePaymentApiRequest,
  type CreatePaymentApiResponse,
} from "@/services/api/paymentsApi";
import { createIdempotencyKey } from "@/utils/orderIdempotency";
import { formatCurrency } from "@/utils/format";
import { ApiError } from "@/services/api/apiClient";
import { useToastStore } from "@/stores/toastStore";

export type PaymentOutcomeInfo = {
  status: string;
  message?: string | null;
  paymentMethodType?: string | null;
  qrCode?: string | null;
  qrCodeBase64?: string | null;
  ticketUrl?: string | null;
  digitableLine?: string | null;
  barcodeContent?: string | null;
  dateOfExpiration?: string | null;
};

type MercadoPagoBrickProps = {
  orderId: string;
  amount: number;
  payerEmail?: string;
  isTestEnvironment?: boolean;
  onPaid?: () => void;
  onOutcome?: (info: PaymentOutcomeInfo) => void;
};

type BrickPayer = {
  email?: string;
  identification?: { type?: string; number?: string };
};

/** Brick paymentType → contrato backend Orders. */
function mapBrickTypeToBackend(
  brickType: string | undefined,
): "bank_transfer" | "credit_card" | "debit_card" | "ticket" | null {
  const t = (brickType ?? "").trim();
  switch (t) {
    case "bank_transfer":
      return "bank_transfer";
    case "creditCard":
      return "credit_card";
    case "debitCard":
      return "debit_card";
    case "ticket":
      return "ticket";
    default:
      return null;
  }
}

function readString(value: unknown): string | undefined {
  if (typeof value === "string" && value.trim()) return value.trim();
  if (typeof value === "number" && Number.isFinite(value)) return String(value);
  return undefined;
}

/**
 * Payment Brick — Checkout Transparente multi-meios (Pix, crédito, débito, boleto).
 * Amount de cobrança é autoridade do backend (order.Total).
 */
export function MercadoPagoBrick({
  orderId,
  amount,
  payerEmail,
  onPaid,
  onOutcome,
}: MercadoPagoBrickProps) {
  const push = useToastStore((s) => s.push);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [brickError, setBrickError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  /** Barreira síncrona contra double-submit (state React sozinho não basta). */
  const submitLockRef = useRef(false);
  const publicKey = getMercadoPagoPublicKey();

  const ready = useMemo(() => {
    if (!publicKey) return false;
    initMercadoPago(publicKey, { locale: "pt-BR" });
    return true;
  }, [publicKey]);

  const configError = publicKey
    ? null
    : "Public Key do Mercado Pago não configurada.";

  // AllOrArray = "all" | string[] — nunca "none".
  const customization = useMemo(
    (): IPaymentBrickCustomization => ({
      paymentMethods: {
        creditCard: "all",
        debitCard: "all",
        ticket: ["bolbradesco"],
        bankTransfer: ["pix"],
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
      const cause =
        typeof err?.cause === "string" ? err.cause.slice(0, 120) : undefined;
      const message =
        typeof err?.message === "string" ? err.message.slice(0, 200) : undefined;
      console.error("Mercado Pago Payment Brick error", { type, cause, message });
      setBrickError(
        "Não foi possível carregar o checkout. Tente novamente em instantes.",
      );
    },
    [],
  );

  const handleBrickReady = useCallback(() => {
    setBrickError(null);
  }, []);

  const buildRequest = useCallback(
    (
      backendType: "bank_transfer" | "credit_card" | "debit_card" | "ticket",
      formData: Record<string, unknown>,
    ): CreatePaymentApiRequest | { error: string } => {
      const paymentMethodId = readString(formData.payment_method_id)?.toLowerCase();
      if (!paymentMethodId) {
        return { error: "Método de pagamento não informado pelo checkout." };
      }

      const payer = (formData.payer ?? {}) as BrickPayer;
      const email =
        readString(payer.email) ??
        (payerEmail && payerEmail.trim() ? payerEmail.trim() : undefined);
      const idType = readString(payer.identification?.type);
      const idNumber = readString(payer.identification?.number);

      if (backendType === "bank_transfer") {
        if (paymentMethodId !== "pix") {
          return { error: "Método Pix inválido." };
        }
        return {
          paymentMethodId: "pix",
          paymentMethodType: "bank_transfer",
          token: null,
          installments: null,
          issuerId: null,
          payerEmail: email ?? null,
        };
      }

      if (backendType === "credit_card") {
        const token = readString(formData.token);
        if (!token) {
          return { error: "Token do cartão ausente. Tente novamente." };
        }
        return {
          paymentMethodId,
          paymentMethodType: "credit_card",
          token,
          installments: 1,
          issuerId: readString(formData.issuer_id) ?? null,
          payerEmail: email ?? null,
          payerIdentificationType: idType ?? null,
          payerIdentificationNumber: idNumber ?? null,
        };
      }

      if (backendType === "debit_card") {
        const token = readString(formData.token);
        if (!token) {
          return { error: "Token do cartão ausente. Tente novamente." };
        }
        return {
          paymentMethodId,
          paymentMethodType: "debit_card",
          token,
          installments: null,
          issuerId: readString(formData.issuer_id) ?? null,
          payerEmail: email ?? null,
          payerIdentificationType: idType ?? null,
          payerIdentificationNumber: idNumber ?? null,
        };
      }

      // ticket / boleto — ID real do Brick (ex.: bolbradesco)
      return {
        paymentMethodId,
        paymentMethodType: "ticket",
        token: null,
        installments: null,
        issuerId: null,
        payerEmail: email ?? null,
        payerIdentificationType: idType ?? null,
        payerIdentificationNumber: idNumber ?? null,
      };
    },
    [payerEmail],
  );

  const handleResult = useCallback(
    (result: CreatePaymentApiResponse, backendType: string) => {
      const status = (result.status ?? "").toLowerCase();
      const outcome: PaymentOutcomeInfo = {
        status,
        message: result.message,
        paymentMethodType: backendType,
        qrCode: result.qrCode,
        qrCodeBase64: result.qrCodeBase64,
        ticketUrl: result.ticketUrl,
        digitableLine: result.digitableLine,
        barcodeContent: result.barcodeContent,
        dateOfExpiration: result.dateOfExpiration,
      };

      if (status === "approved" || status === "processed") {
        push("success", result.message || "Pagamento aprovado.");
        onOutcome?.(outcome);
        onPaid?.();
        return;
      }

      if (status === "rejected") {
        push(
          "error",
          result.message ||
            "Pagamento não aprovado. Você pode tentar outro meio ou cartão.",
        );
        onOutcome?.(outcome);
        return;
      }

      // pending / processing
      if (backendType === "bank_transfer" && (result.qrCode || result.qrCodeBase64)) {
        push(
          "info",
          result.message ||
            "Aguardando pagamento. Conclua o Pix para confirmar o pedido.",
        );
      } else if (backendType === "ticket") {
        push(
          "info",
          result.message ||
            "Boleto gerado. O pedido só será confirmado após a compensação.",
        );
      } else {
        push("info", result.message || "Pagamento em processamento.");
      }
      onOutcome?.(outcome);
    },
    [onOutcome, onPaid, push],
  );

  const handleSubmit = useCallback(
    async (
      param: {
        paymentType?: string;
        selectedPaymentMethod?: string;
        formData?: Record<string, unknown>;
      } & Record<string, unknown>,
    ) => {
      if (submitLockRef.current) {
        return;
      }
      submitLockRef.current = true;
      setSubmitting(true);
      setSubmitError(null);

      try {
        const brickType =
          readString(param.paymentType) ??
          readString(param.selectedPaymentMethod);
        const backendType = mapBrickTypeToBackend(brickType);
        if (!backendType) {
          const message =
            "Meio de pagamento não suportado. Escolha Pix, cartão ou boleto.";
          setSubmitError(message);
          push("error", message);
          throw new Error(message);
        }

        const formData = (param.formData ?? param) as Record<string, unknown>;
        const built = buildRequest(backendType, formData);
        if ("error" in built) {
          setSubmitError(built.error);
          push("error", built.error);
          throw new Error(built.error);
        }

        // Uma key por submissão aceita pelo lock — sem retry de negócio.
        const idempotencyKey = createIdempotencyKey();

        try {
          const result = await paymentsApi.createForOrder(
            orderId,
            built,
            idempotencyKey,
          );
          handleResult(result, backendType);
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
      } finally {
        submitLockRef.current = false;
        setSubmitting(false);
      }
    },
    [buildRequest, handleResult, orderId, push],
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
        .
      </p>
      <p className="text-sm text-esotera-muted">
        Pagamento seguro via Mercado Pago
      </p>
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
