"use client";

import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useCallback, useEffect, useState } from "react";
import { MercadoPagoBrick } from "@/components/checkout/MercadoPagoBrick";
import { LoadingState } from "@/components/ui/LoadingState";
import { isRealPaymentEnabled } from "@/config/storeMode";
import { isApiMode } from "@/config/dataMode";
import { ordersApi } from "@/services/api/ordersApi";
import {
  paymentsApi,
  type PaymentEnvironmentConfig,
} from "@/services/api/paymentsApi";
import { createIdempotencyKey } from "@/utils/orderIdempotency";
import { formatCurrency } from "@/utils/format";
import { ApiError } from "@/services/api/apiClient";
import type { Order } from "@/types";

export default function PagarPedidoPage() {
  const params = useParams();
  const router = useRouter();
  const orderId = String(params.id ?? "");
  const [order, setOrder] = useState<Order | null>(null);
  const [loading, setLoading] = useState(true);
  const [pixCode, setPixCode] = useState<string | null>(null);
  const [pixQr, setPixQr] = useState<string | null>(null);
  const [pixLabel, setPixLabel] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [mpConfig, setMpConfig] = useState<PaymentEnvironmentConfig | null>(
    null,
  );
  const [sandboxLoading, setSandboxLoading] = useState(false);
  const [sandboxError, setSandboxError] = useState<string | null>(null);

  const isTestEnv =
    (mpConfig?.environment ?? "").toLowerCase() === "test" ||
    mpConfig?.sandboxPixEnabled === true;

  useEffect(() => {
    let cancelled = false;
    async function load() {
      if (!isApiMode()) {
        setError("Pagamento disponível apenas no modo API.");
        setLoading(false);
        return;
      }
      try {
        const [data, cfg] = await Promise.all([
          ordersApi.getMine(orderId),
          paymentsApi.getConfig().catch(() => null),
        ]);
        if (cancelled) return;
        if (cfg) setMpConfig(cfg);
        if (!data) {
          setError("Pedido não encontrado.");
          setLoading(false);
          return;
        }
        if (data.status === "payment_approved") {
          router.replace(`/pedido-confirmado/${orderId}`);
          return;
        }
        setOrder(data);
        if (
          !isRealPaymentEnabled() &&
          !(cfg?.sandboxPixEnabled && cfg.environment.toLowerCase() === "test")
        ) {
          setError(
            "Pagamento real não está ativo neste ambiente. Configure a Public Key ou use o modo de homologação.",
          );
        }
      } catch {
        if (!cancelled) setError("Não foi possível carregar o pedido.");
      } finally {
        if (!cancelled) setLoading(false);
      }
    }
    void load();
    return () => {
      cancelled = true;
    };
  }, [orderId, router]);

  useEffect(() => {
    if (!order || order.status === "payment_approved") return;
    const id = window.setInterval(async () => {
      try {
        const fresh = await ordersApi.getMine(orderId);
        if (fresh?.status === "payment_approved") {
          router.replace(`/pedido-confirmado/${orderId}`);
        }
      } catch {
        // ignore transient errors
      }
    }, 5000);
    return () => window.clearInterval(id);
  }, [order, orderId, router]);

  const handleSandboxPix = useCallback(async () => {
    if (sandboxLoading) return;
    setSandboxLoading(true);
    setSandboxError(null);
    try {
      const result = await paymentsApi.createSandboxPixTest(
        createIdempotencyKey(),
      );
      if (result.qrCode) setPixCode(result.qrCode);
      if (result.qrCodeBase64) setPixQr(result.qrCodeBase64);
      setPixLabel(
        `Pix de teste de ${formatCurrency(result.amount)} — não é o total do seu pedido`,
      );
    } catch (err) {
      const message =
        err instanceof ApiError
          ? err.userMessage
          : err instanceof Error
            ? err.message
            : "Falha ao gerar Pix de teste.";
      setSandboxError(message);
    } finally {
      setSandboxLoading(false);
    }
  }, [sandboxLoading]);

  if (loading) return <LoadingState label="Carregando pagamento…" />;

  if (error || !order) {
    return (
      <div className="mx-auto max-w-lg px-4 py-12">
        <h1 className="font-serif text-3xl text-esotera-secondary">Pagamento</h1>
        <p className="mt-3 text-sm text-esotera-error" role="alert">
          {error ?? "Pedido indisponível."}
        </p>
        <Link
          href="/minha-conta"
          className="mt-6 inline-block text-esotera-primary hover:underline"
        >
          Voltar à minha conta
        </Link>
      </div>
    );
  }

  const showCommercialBrick = isRealPaymentEnabled();
  const showSandboxAction = Boolean(mpConfig?.sandboxPixEnabled && isTestEnv);

  return (
    <div className="mx-auto max-w-lg px-4 py-12 sm:px-6">
      <h1 className="font-serif text-3xl text-esotera-secondary">Pagar pedido</h1>
      <p className="mt-2 text-sm text-esotera-muted">
        Pedido {order.orderNumber} · total do pedido{" "}
        {formatCurrency(order.total)}. O status só será confirmado após o
        Mercado Pago (webhook/consulta) — o retorno do navegador não marca como
        pago.
      </p>

      {isTestEnv ? (
        <div
          role="status"
          className="mt-6 rounded-md border border-amber-700/40 bg-amber-50 px-4 py-3 text-sm text-amber-950"
        >
          Ambiente de teste — nenhuma cobrança real será realizada
        </div>
      ) : null}

      {showSandboxAction ? (
        <div className="mt-6 space-y-3 rounded-md border border-esotera-border p-4">
          <p className="text-sm text-esotera-text">
            Teste isolado do Mercado Pago (sandbox). Gera um Pix oficial de{" "}
            {formatCurrency(mpConfig?.sandboxPixAmount ?? 50)} —{" "}
            <strong className="font-medium">não</strong> substitui o total do
            seu pedido ({formatCurrency(order.total)}), não consome cupom e não
            marca o pedido como pago.
          </p>
          <button
            type="button"
            disabled={sandboxLoading}
            onClick={() => void handleSandboxPix()}
            className="rounded-md bg-esotera-secondary px-4 py-2 text-sm text-white disabled:cursor-not-allowed disabled:opacity-60"
          >
            {sandboxLoading
              ? "Gerando Pix de teste…"
              : `Gerar Pix de teste de ${formatCurrency(mpConfig?.sandboxPixAmount ?? 50)}`}
          </button>
          {sandboxError ? (
            <p className="text-sm text-esotera-error" role="alert">
              {sandboxError}
            </p>
          ) : null}
        </div>
      ) : null}

      {showCommercialBrick ? (
        <div className="mt-8">
          <MercadoPagoBrick
            orderId={order.id}
            amount={order.total}
            payerEmail={order.upSellerExport?.customerEmail}
            isTestEnvironment={isTestEnv}
            onPaid={() => router.replace(`/pedido-confirmado/${order.id}`)}
            onPending={({ qrCode, qrCodeBase64 }) => {
              if (qrCode) setPixCode(qrCode);
              if (qrCodeBase64) setPixQr(qrCodeBase64);
              setPixLabel(`Pix do pedido · ${formatCurrency(order.total)}`);
            }}
          />
        </div>
      ) : null}

      {pixQr || pixCode ? (
        <div className="mt-8 space-y-3 rounded-md border border-esotera-border p-4">
          <p className="text-sm font-medium text-esotera-text">
            {pixLabel ?? "Pix"}
          </p>
          {pixQr ? (
            // eslint-disable-next-line @next/next/no-img-element
            <img
              src={
                pixQr.startsWith("data:")
                  ? pixQr
                  : `data:image/png;base64,${pixQr}`
              }
              alt="QR Code Pix"
              className="mx-auto h-48 w-48"
            />
          ) : null}
          {pixCode ? (
            <div>
              <p className="text-xs text-esotera-muted">Copia e cola</p>
              <textarea
                readOnly
                className="mt-1 w-full rounded-md border border-esotera-border bg-esotera-surface p-2 text-xs"
                rows={3}
                value={pixCode}
              />
            </div>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}
