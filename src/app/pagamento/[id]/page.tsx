"use client";

import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import {
  MercadoPagoBrick,
  type PaymentOutcomeInfo,
} from "@/components/checkout/MercadoPagoBrick";
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

function formatExpiration(value: string | null | undefined): string | null {
  if (!value) return null;
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return value;
  return d.toLocaleString("pt-BR");
}

export default function PagarPedidoPage() {
  const params = useParams();
  const router = useRouter();
  const orderId = String(params.id ?? "");
  const [order, setOrder] = useState<Order | null>(null);
  const [loading, setLoading] = useState(true);
  const [outcome, setOutcome] = useState<PaymentOutcomeInfo | null>(null);
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

  // Soft refresh: só redireciona quando a API do pedido confirmar payment_approved.
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

  const showCommercialBrick = !sandboxMode && isRealPaymentEnabled();
  const outcomeStatus = (outcome?.status ?? "").toLowerCase();
  const showPix =
    outcome?.paymentMethodType === "bank_transfer" &&
    (outcome.qrCode || outcome.qrCodeBase64);
  const showBoleto =
    outcome?.paymentMethodType === "ticket" &&
    (outcome.ticketUrl || outcome.digitableLine || outcome.barcodeContent);
  const showCardStatus =
    (outcome?.paymentMethodType === "credit_card" ||
      outcome?.paymentMethodType === "debit_card") &&
    outcomeStatus.length > 0;

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
            onPaid={async () => {
              // Não marca order.status localmente; só redireciona se a API confirmar.
              try {
                const fresh = await ordersApi.getMine(order.id);
                if (fresh?.status === "payment_approved") {
                  router.replace(`/pedido-confirmado/${order.id}`);
                }
              } catch {
                // ignore — webhook/soft-refresh permanece autoridade
              }
            }}
            onOutcome={setOutcome}
          />
        </div>
      ) : null}

      {!sandboxMode && !showCommercialBrick ? (
        <p className="mt-6 text-sm text-esotera-muted" role="status">
          Pagamento comercial indisponível neste ambiente.
        </p>
      ) : null}

      {!sandboxMode && showPix ? (
        <div className="mt-8 space-y-3 rounded-md border border-esotera-border p-4">
          <p className="text-sm font-medium text-esotera-text">
            Pix do pedido · {formatCurrency(order.total)}
          </p>
          <p className="text-sm text-esotera-muted">Aguardando pagamento</p>
          {outcome?.qrCodeBase64 ? (
            // eslint-disable-next-line @next/next/no-img-element
            <img
              src={
                outcome.qrCodeBase64.startsWith("data:")
                  ? outcome.qrCodeBase64
                  : `data:image/png;base64,${outcome.qrCodeBase64}`
              }
              alt="QR Code Pix"
              className="mx-auto h-48 w-48"
            />
          ) : null}
          {outcome?.qrCode ? (
            <div>
              <p className="text-xs text-esotera-muted">Pix copia e cola</p>
              <textarea
                readOnly
                className="mt-1 w-full rounded-md border border-esotera-border bg-esotera-surface p-2 text-xs"
                rows={4}
                value={outcome.qrCode}
              />
            </div>
          ) : null}
          {outcome?.dateOfExpiration ? (
            <p className="text-xs text-esotera-muted">
              Expira em: {formatExpiration(outcome.dateOfExpiration)}
            </p>
          ) : null}
        </div>
      ) : null}

      {!sandboxMode && showBoleto ? (
        <div className="mt-8 space-y-3 rounded-md border border-esotera-border p-4">
          <p className="text-sm font-medium text-esotera-text">Boleto gerado</p>
          <p className="text-sm text-esotera-muted">Aguardando pagamento</p>
          {outcome?.ticketUrl ? (
            <a
              href={outcome.ticketUrl}
              target="_blank"
              rel="noopener noreferrer"
              className="inline-block text-sm text-esotera-primary hover:underline"
            >
              Abrir boleto
            </a>
          ) : null}
          {outcome?.digitableLine ? (
            <div>
              <p className="text-xs text-esotera-muted">Linha digitável</p>
              <textarea
                readOnly
                className="mt-1 w-full rounded-md border border-esotera-border bg-esotera-surface p-2 text-xs"
                rows={3}
                value={outcome.digitableLine}
              />
            </div>
          ) : null}
          {outcome?.barcodeContent ? (
            <p className="text-xs text-esotera-muted break-all">
              Código de barras: {outcome.barcodeContent}
            </p>
          ) : null}
          {outcome?.dateOfExpiration ? (
            <p className="text-xs text-esotera-muted">
              Vencimento: {formatExpiration(outcome.dateOfExpiration)}
            </p>
          ) : null}
        </div>
      ) : null}

      {!sandboxMode && showCardStatus ? (
        <div className="mt-8 space-y-2 rounded-md border border-esotera-border p-4">
          <p className="text-sm font-medium text-esotera-text">
            {outcomeStatus === "approved" || outcomeStatus === "processed"
              ? "Pagamento aprovado"
              : outcomeStatus === "rejected"
                ? "Pagamento não aprovado"
                : "Pagamento em processamento"}
          </p>
          {outcome?.message ? (
            <p className="text-sm text-esotera-muted">{outcome.message}</p>
          ) : null}
          {outcomeStatus === "rejected" ? (
            <p className="text-sm text-esotera-muted">
              Você pode tentar outro cartão ou meio de pagamento no checkout
              acima.
            </p>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}
