"use client";

import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { MercadoPagoBrick } from "@/components/checkout/MercadoPagoBrick";
import { LoadingState } from "@/components/ui/LoadingState";
import { isRealPaymentEnabled } from "@/config/storeMode";
import { isApiMode } from "@/config/dataMode";
import { ordersApi } from "@/services/api/ordersApi";
import { formatCurrency } from "@/utils/format";
import type { Order } from "@/types";

export default function PagarPedidoPage() {
  const params = useParams();
  const router = useRouter();
  const orderId = String(params.id ?? "");
  const [order, setOrder] = useState<Order | null>(null);
  const [loading, setLoading] = useState(true);
  const [pixCode, setPixCode] = useState<string | null>(null);
  const [pixQr, setPixQr] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    async function load() {
      if (!isApiMode() || !isRealPaymentEnabled()) {
        setError(
          "Pagamento real não está ativo neste ambiente. Configure a Public Key ou use o modo de homologação.",
        );
        setLoading(false);
        return;
      }
      try {
        const data = await ordersApi.getMine(orderId);
        if (cancelled) return;
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

  // Polling leve: status só muda via webhook/consulta — nunca pelo “return” do browser.
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

  if (loading) return <LoadingState label="Carregando pagamento…" />;

  if (error || !order) {
    return (
      <div className="mx-auto max-w-lg px-4 py-12">
        <h1 className="font-serif text-3xl text-esotera-secondary">Pagamento</h1>
        <p className="mt-3 text-sm text-esotera-error" role="alert">
          {error ?? "Pedido indisponível."}
        </p>
        <Link href="/minha-conta" className="mt-6 inline-block text-esotera-primary hover:underline">
          Voltar à minha conta
        </Link>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-lg px-4 py-12 sm:px-6">
      <h1 className="font-serif text-3xl text-esotera-secondary">Pagar pedido</h1>
      <p className="mt-2 text-sm text-esotera-muted">
        Pedido {order.orderNumber} · {formatCurrency(order.total)}. O status só
        será confirmado após o Mercado Pago (webhook/consulta) — o retorno do
        navegador não marca como pago.
      </p>

      <div className="mt-8">
        <MercadoPagoBrick
          orderId={order.id}
          amount={order.total}
          payerEmail={order.upSellerExport?.customerEmail}
          onPaid={() => router.replace(`/pedido-confirmado/${order.id}`)}
          onPending={({ qrCode, qrCodeBase64 }) => {
            if (qrCode) setPixCode(qrCode);
            if (qrCodeBase64) setPixQr(qrCodeBase64);
          }}
        />
      </div>

      {pixQr || pixCode ? (
        <div className="mt-8 space-y-3 rounded-md border border-esotera-border p-4">
          <p className="text-sm font-medium text-esotera-text">Pix</p>
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
