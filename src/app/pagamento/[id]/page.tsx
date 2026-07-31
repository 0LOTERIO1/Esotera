"use client";

import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { MercadoPagoBrick } from "@/components/checkout/MercadoPagoBrick";
import { SandboxPaymentCheckout } from "@/components/checkout/SandboxPaymentCheckout";
import { LoadingState } from "@/components/ui/LoadingState";
import { isRealPaymentEnabled } from "@/config/storeMode";
import { isApiMode } from "@/config/dataMode";
import { ordersApi } from "@/services/api/ordersApi";
import {
  paymentsApi,
  type PaymentEnvironmentConfig,
} from "@/services/api/paymentsApi";
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

        const sandboxOk = cfgResult.ok && isSandboxTestMode(cfgResult.cfg);
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

  // Test+sandbox: UI estilo MP sem Brick comercial.
  // Production: Brick comercial (valor real do pedido).
  const showCommercialBrick = !sandboxMode && isRealPaymentEnabled();

  return (
    <div className="mx-auto max-w-lg px-4 py-12 sm:px-6">
      <h1 className="font-serif text-3xl text-esotera-secondary">Pagar pedido</h1>
      <p className="mt-2 text-sm text-esotera-muted">
        Pedido {order.orderNumber}. O status só será confirmado após o Mercado
        Pago (webhook/consulta) — o retorno do navegador não marca como pago.
      </p>

      {sandboxMode ? (
        <div
          role="status"
          className="mt-6 rounded-md border border-amber-800/50 bg-amber-50 px-4 py-3 text-sm text-amber-950"
        >
          Ambiente de teste — nenhuma cobrança real será realizada.
        </div>
      ) : null}

      {configError ? (
        <p className="mt-4 text-sm text-esotera-error" role="alert">
          {configError}
        </p>
      ) : null}

      {sandboxMode ? (
        <div className="mt-6">
          <SandboxPaymentCheckout
            orderNumber={order.orderNumber ?? order.id}
            orderTotal={order.total}
            sandboxAmount={sandboxAmount}
          />
        </div>
      ) : null}

      {showCommercialBrick ? (
        <div className="mt-8">
          <p className="mb-4 text-sm text-esotera-muted">
            Total do pedido:{" "}
            <span className="font-medium text-esotera-text">
              {formatCurrency(order.total)}
            </span>
          </p>
          <MercadoPagoBrick
            orderId={order.id}
            amount={order.total}
            payerEmail={order.upSellerExport?.customerEmail}
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

      {!sandboxMode && (pixQr || pixCode) ? (
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
              <p className="text-xs text-esotera-muted">Pix copia e cola</p>
              <textarea
                readOnly
                className="mt-1 w-full rounded-md border border-esotera-border bg-esotera-surface p-2 text-xs"
                rows={4}
                value={pixCode}
              />
            </div>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}
