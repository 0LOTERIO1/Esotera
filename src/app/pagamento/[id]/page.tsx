"use client";

import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useCallback, useEffect, useRef, useState } from "react";
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

function isSandboxTestMode(cfg: PaymentEnvironmentConfig | null): boolean {
  if (!cfg) return false;
  const env = (cfg.environment ?? "").trim().toLowerCase();
  return cfg.sandboxPixEnabled === true && (env === "test" || env === "sandbox");
}

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
  const [configError, setConfigError] = useState<string | null>(null);
  const [mpConfig, setMpConfig] = useState<PaymentEnvironmentConfig | null>(
    null,
  );
  const [sandboxLoading, setSandboxLoading] = useState(false);
  const [sandboxError, setSandboxError] = useState<string | null>(null);
  const sandboxInFlight = useRef(false);

  const sandboxMode = isSandboxTestMode(mpConfig);
  const sandboxAmount = mpConfig?.sandboxPixAmount ?? 50;

  useEffect(() => {
    let cancelled = false;
    async function load() {
      if (!isApiMode()) {
        setError("Pagamento disponível apenas no modo API.");
        setLoading(false);
        return;
      }
      try {
        const [data, cfgResult] = await Promise.all([
          ordersApi.getMine(orderId),
          paymentsApi.getConfig().then(
            (cfg) => ({ ok: true as const, cfg }),
            (err: unknown) => ({ ok: false as const, err }),
          ),
        ]);
        if (cancelled) return;

        if (cfgResult.ok) {
          setMpConfig(cfgResult.cfg);
          setConfigError(null);
        } else {
          setMpConfig(null);
          setConfigError(
            cfgResult.err instanceof ApiError
              ? cfgResult.err.userMessage
              : "Não foi possível carregar a configuração de pagamento.",
          );
        }

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

        const sandboxOk =
          cfgResult.ok && isSandboxTestMode(cfgResult.cfg);
        if (!isRealPaymentEnabled() && !sandboxOk) {
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
    if (!order || order.status === "payment_approved" || sandboxMode) return;
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
  }, [order, orderId, router, sandboxMode]);

  const handleSandboxPix = useCallback(async () => {
    if (sandboxInFlight.current || sandboxLoading) return;
    sandboxInFlight.current = true;
    setSandboxLoading(true);
    setSandboxError(null);
    setPixCode(null);
    setPixQr(null);
    setPixLabel(null);
    try {
      const result = await paymentsApi.createSandboxPixTest(
        createIdempotencyKey(),
      );
      if (result.qrCode) setPixCode(result.qrCode);
      if (result.qrCodeBase64) setPixQr(result.qrCodeBase64);
      setPixLabel(
        `Pix de teste de ${formatCurrency(result.amount)} — não é o total do pedido`,
      );
      if (!result.qrCode && !result.qrCodeBase64) {
        setSandboxError(
          result.message ||
            "Pix de teste criado, mas o QR Code não veio na resposta.",
        );
      }
    } catch (err) {
      const message =
        err instanceof ApiError
          ? err.userMessage
          : err instanceof Error
            ? err.message
            : "Falha ao gerar Pix de teste.";
      setSandboxError(message);
    } finally {
      sandboxInFlight.current = false;
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

  // Em Test+sandbox: só fluxo isolado (sem Brick / sem "Pagar" comercial).
  // Em Production: Brick comercial (somente Pix).
  const showCommercialBrick = !sandboxMode && isRealPaymentEnabled();

  return (
    <div className="mx-auto max-w-lg px-4 py-12 sm:px-6">
      <h1 className="font-serif text-3xl text-esotera-secondary">Pagar pedido</h1>
      <p className="mt-2 text-sm text-esotera-muted">
        Pedido {order.orderNumber} · total do pedido{" "}
        <span className="font-medium text-esotera-text">
          {formatCurrency(order.total)}
        </span>
        . O status só será confirmado após o Mercado Pago (webhook/consulta).
      </p>

      {sandboxMode ? (
        <div
          role="status"
          className="mt-6 rounded-md border border-amber-800/50 bg-amber-50 px-4 py-3 text-sm text-amber-950"
        >
          Ambiente de teste — nenhuma cobrança real será realizada
        </div>
      ) : null}

      {configError ? (
        <p className="mt-4 text-sm text-esotera-error" role="alert">
          {configError}
        </p>
      ) : null}

      <div className="mt-6 space-y-2 text-sm text-esotera-muted">
        <p className="font-medium text-esotera-text">Formas de pagamento</p>
        <ul className="space-y-1">
          <li>Pix — {sandboxMode ? "teste isolado disponível" : "disponível"}</li>
          <li className="opacity-60">Cartão — Em breve</li>
          <li className="opacity-60">Boleto — Em breve</li>
        </ul>
      </div>

      {sandboxMode ? (
        <div className="mt-6 space-y-4 rounded-md border-2 border-esotera-secondary bg-esotera-surface px-4 py-5">
          <p className="text-sm text-esotera-text">
            Use o Pix oficial de teste do Mercado Pago (
            {formatCurrency(sandboxAmount)}). Este valor{" "}
            <strong className="font-semibold">não</strong> é o total do seu
            pedido ({formatCurrency(order.total)}), não altera o pedido, não
            consome cupom e não marca como pago.
          </p>
          <button
            type="button"
            disabled={sandboxLoading}
            aria-busy={sandboxLoading}
            onClick={() => void handleSandboxPix()}
            className="w-full rounded-md bg-esotera-secondary px-4 py-3 text-sm font-medium text-white transition hover:opacity-95 disabled:cursor-not-allowed disabled:opacity-60"
          >
            {sandboxLoading
              ? "Gerando Pix de teste…"
              : `Gerar Pix de teste de ${formatCurrency(sandboxAmount)}`}
          </button>
          <p className="text-xs text-esotera-muted">
            O pagamento comercial deste pedido fica desabilitado enquanto o
            Mercado Pago estiver em ambiente de teste.
          </p>
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
            isTestEnvironment={false}
            onPaid={() => router.replace(`/pedido-confirmado/${order.id}`)}
            onPending={({ qrCode, qrCodeBase64 }) => {
              if (qrCode) setPixCode(qrCode);
              if (qrCodeBase64) setPixQr(qrCodeBase64);
              setPixLabel(`Pix do pedido · ${formatCurrency(order.total)}`);
            }}
          />
        </div>
      ) : null}

      {!sandboxMode && !showCommercialBrick ? (
        <p className="mt-6 text-sm text-esotera-muted" role="status">
          Pagamento comercial indisponível neste ambiente.
        </p>
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
              alt="QR Code Pix de teste"
              className="mx-auto h-48 w-48"
            />
          ) : null}
          {pixCode ? (
            <div>
              <p className="text-xs text-esotera-muted">Pix copia e cola</p>
              <textarea
                readOnly
                className="mt-1 w-full rounded-md border border-esotera-border bg-esotera-surface p-2 text-xs"
                rows={4}
                value={pixCode}
              />
              <button
                type="button"
                className="mt-2 text-sm text-esotera-primary underline"
                onClick={() => {
                  void navigator.clipboard?.writeText(pixCode);
                }}
              >
                Copiar código
              </button>
            </div>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}
