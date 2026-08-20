"use client";

import { useCallback, useEffect, useState } from "react";
import { getAdminRepository } from "@/services/repositories";
import { adminApi } from "@/services/api/adminApi";
import { ApiError } from "@/services/api/apiClient";
import type {
  AdminOrderDetail,
  AdminOrderSummary,
} from "@/services/api/adminTypes";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { Price } from "@/components/ui/Price";
import { FormField, inputClassName } from "@/components/ui/FormField";
import { Button } from "@/components/ui/Button";
import { EmptyState } from "@/components/ui/EmptyState";
import { ProductImage } from "@/components/ui/ProductImage";
import { ConfirmModal } from "@/components/ui/ConfirmModal";
import { formatDate } from "@/utils/format";
import { orderStatusLabels, paymentMethodLabels } from "@/utils/labels";
import type { OrderStatus } from "@/types";
import { useToastStore } from "@/stores/toastStore";

export default function AdminOrdersPage() {
  const push = useToastStore((s) => s.push);
  const [query, setQuery] = useState("");
  const [status, setStatus] = useState("all");
  const [page, setPage] = useState(1);
  const [orders, setOrders] = useState<AdminOrderSummary[]>([]);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selected, setSelected] = useState<AdminOrderDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [pendingStatus, setPendingStatus] = useState<OrderStatus | null>(null);
  const [updating, setUpdating] = useState(false);
  const [exporting, setExporting] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await getAdminRepository().listOrders({
        search: query.trim() || undefined,
        status: status === "all" ? undefined : status,
        page,
        pageSize: 20,
      });
      setOrders(result.items);
      setTotalPages(result.totalPages);
      setTotalCount(result.totalCount);
    } catch (err) {
      setOrders([]);
      setError(
        err instanceof ApiError
          ? err.userMessage
          : err instanceof Error
            ? err.message
            : "Não foi possível carregar os pedidos.",
      );
    } finally {
      setLoading(false);
    }
  }, [query, status, page]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void load();
    }, 0);
    return () => window.clearTimeout(timer);
  }, [load]);

  async function openDetail(id: string) {
    setDetailLoading(true);
    try {
      const detail = await getAdminRepository().getOrder(id);
      if (!detail) {
        push("error", "Pedido não encontrado.");
        return;
      }
      setSelected(detail);
    } catch (err) {
      push(
        "error",
        err instanceof ApiError
          ? err.userMessage
          : "Não foi possível abrir o pedido.",
      );
    } finally {
      setDetailLoading(false);
    }
  }

  async function confirmStatusChange() {
    if (!selected || !pendingStatus) return;
    setUpdating(true);
    try {
      const updated = await getAdminRepository().updateOrderStatus(
        selected.id,
        pendingStatus,
        selected.rowVersion,
      );
      setSelected(updated);
      setPendingStatus(null);
      push("success", "Status atualizado.");
      await load();
    } catch (err) {
      push(
        "error",
        err instanceof ApiError
          ? err.userMessage
          : err instanceof Error
            ? err.message
            : "Erro ao atualizar status.",
      );
    } finally {
      setUpdating(false);
    }
  }

  async function exportUpSeller() {
    if (!selected) return;
    setExporting(true);
    try {
      const blob = await adminApi.exportOrderUpSeller(selected.id);
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `upseller-pedido-${selected.orderNumber}.xlsx`;
      a.click();
      URL.revokeObjectURL(url);
      push("success", "Arquivo UpSeller gerado.");
    } catch (err) {
      push(
        "error",
        err instanceof ApiError
          ? err.userMessage
          : "Não foi possível exportar para o UpSeller.",
      );
    } finally {
      setExporting(false);
    }
  }

  const canExportUpSeller =
    !!selected &&
    ["payment_approved", "preparing"].includes(selected.status);

  return (
    <div>
      <h1 className="font-serif text-3xl text-esotera-secondary">Pedidos</h1>
      <p className="mt-1 text-sm text-esotera-muted">
        Pedidos da loja
      </p>

      <div className="mt-6 grid gap-3 sm:grid-cols-2">
        <FormField label="Pesquisar" id="order-search">
          <input
            id="order-search"
            className={inputClassName}
            value={query}
            onChange={(e) => {
              setPage(1);
              setQuery(e.target.value);
            }}
            placeholder="Número ou cliente"
          />
        </FormField>
        <FormField label="Status" id="order-status">
          <select
            id="order-status"
            className={inputClassName}
            value={status}
            onChange={(e) => {
              setPage(1);
              setStatus(e.target.value);
            }}
          >
            <option value="all">Todos</option>
            {(Object.keys(orderStatusLabels) as OrderStatus[]).map((key) => (
              <option key={key} value={key}>
                {orderStatusLabels[key]}
              </option>
            ))}
          </select>
        </FormField>
      </div>

      {loading ? (
        <p className="mt-8 text-sm text-esotera-muted">Carregando pedidos…</p>
      ) : error ? (
        <div className="mt-8">
          <EmptyState
            title="Erro ao carregar pedidos"
            description={error}
            action={
              <Button type="button" onClick={() => void load()}>
                Tentar novamente
              </Button>
            }
          />
        </div>
      ) : !orders.length ? (
        <div className="mt-8">
          <EmptyState title="Nenhum pedido encontrado" />
        </div>
      ) : (
        <>
          <ul className="mt-6 space-y-3">
            {orders.map((order) => (
              <li
                key={order.id}
                className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-esotera-border p-4"
              >
                <div>
                  <p className="text-esotera-text">{order.orderNumber}</p>
                  <p className="text-xs text-esotera-muted">
                    {order.customerName} · {formatDate(order.createdAt)} ·{" "}
                    {order.itemCount} item(ns)
                  </p>
                  <p className="text-xs text-esotera-muted">
                    {paymentMethodLabels[
                      order.paymentMethod as keyof typeof paymentMethodLabels
                    ] ?? order.paymentMethod}{" "}
                    · {order.shippingMethodName}
                  </p>
                </div>
                <StatusBadge status={order.status} />
                <Price value={order.total} />
                <Button
                  type="button"
                  variant="secondary"
                  disabled={detailLoading}
                  onClick={() => void openDetail(order.id)}
                >
                  Detalhes
                </Button>
              </li>
            ))}
          </ul>
          <div className="mt-4 flex flex-wrap items-center justify-between gap-3 text-sm text-esotera-muted">
            <span>
              {totalCount} pedido(s) · página {page} de {totalPages}
            </span>
            <div className="flex gap-2">
              <Button
                type="button"
                variant="secondary"
                disabled={page <= 1}
                onClick={() => setPage((p) => Math.max(1, p - 1))}
              >
                Anterior
              </Button>
              <Button
                type="button"
                variant="secondary"
                disabled={page >= totalPages}
                onClick={() => setPage((p) => p + 1)}
              >
                Próxima
              </Button>
            </div>
          </div>
        </>
      )}

      {selected ? (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-esotera-secondary/40 p-4">
          <div
            role="dialog"
            aria-modal
            className="max-h-[90vh] w-full max-w-lg overflow-y-auto rounded-lg border border-esotera-border bg-esotera-surface p-6"
          >
            <h2 className="font-serif text-xl text-esotera-secondary">
              {selected.orderNumber}
            </h2>
            <p className="mt-2 text-sm text-esotera-muted">
              {formatDate(selected.createdAt)} ·{" "}
              <StatusBadge status={selected.status} />
            </p>
            <p className="mt-3 text-sm text-esotera-muted">
              Cliente: {selected.customer.name}
              <br />
              E-mail: {selected.customer.email}
              <br />
              Telefone: {selected.customer.phone || "—"}
            </p>
            <p className="mt-2 text-xs text-esotera-muted">
              Gateway de pagamento ainda não integrado — status gerenciado
              manualmente.
            </p>

            <FormField label="Alterar status" id="change-status">
              <select
                id="change-status"
                className={inputClassName}
                value={selected.status}
                disabled={updating}
                onChange={(e) => {
                  const next = e.target.value as OrderStatus;
                  if (next !== selected.status) setPendingStatus(next);
                }}
              >
                {(Object.keys(orderStatusLabels) as OrderStatus[]).map((key) => (
                  <option key={key} value={key}>
                    {orderStatusLabels[key]}
                  </option>
                ))}
              </select>
            </FormField>

            <ul className="mt-4 space-y-3">
              {selected.items.map((item) => (
                <li key={item.id} className="flex gap-3 text-sm">
                  <div className="relative h-14 w-11 shrink-0 overflow-hidden rounded">
                    <ProductImage
                      src={item.image}
                      alt={item.name}
                      fill
                      sizes="44px"
                    />
                  </div>
                  <div>
                    <p className="text-esotera-text">
                      {item.quantity}× {item.name}
                    </p>
                    <p className="text-esotera-muted">
                      <Price value={item.price} /> · linha{" "}
                      <Price value={item.lineTotal} />
                    </p>
                  </div>
                </li>
              ))}
            </ul>

            <dl className="mt-4 space-y-1 text-sm">
              <div className="flex justify-between">
                <dt className="text-esotera-muted">Subtotal</dt>
                <dd>
                  <Price value={selected.subtotal} />
                </dd>
              </div>
              <div className="flex justify-between">
                <dt className="text-esotera-muted">
                  Desconto{selected.couponCode ? ` (${selected.couponCode})` : ""}
                </dt>
                <dd>
                  <Price value={selected.discount} />
                </dd>
              </div>
              <div className="flex justify-between">
                <dt className="text-esotera-muted">Frete</dt>
                <dd>
                  <Price value={selected.shippingPrice} />
                </dd>
              </div>
              <div className="flex justify-between text-base">
                <dt className="text-esotera-text">Total</dt>
                <dd>
                  <Price value={selected.total} />
                </dd>
              </div>
            </dl>

            <div className="mt-4 text-sm text-esotera-muted">
              <p>
                Pagamento:{" "}
                {paymentMethodLabels[
                  selected.payment.method as keyof typeof paymentMethodLabels
                ] ?? selected.payment.method}
                {selected.payment.installments
                  ? ` · ${selected.payment.installments}x`
                  : ""}
              </p>
              <p>
                Entrega: {selected.shipping.methodName} ·{" "}
                {selected.shipping.estimatedDays}
              </p>
              <p className="mt-2">
                {selected.address.street}, {selected.address.number}
                <br />
                {selected.address.neighborhood} · {selected.address.city}/
                {selected.address.state} · CEP {selected.address.cep}
              </p>
            </div>

            {selected.statusHistory.length ? (
              <div className="mt-4">
                <h3 className="text-sm text-esotera-text">Histórico de status</h3>
                <ul className="mt-2 space-y-1 text-xs text-esotera-muted">
                  {selected.statusHistory.map((h, i) => (
                    <li key={`${h.toStatus}-${h.createdAt}-${i}`}>
                      {formatDate(h.createdAt)} ·{" "}
                      {h.fromStatus ? `${h.fromStatus} → ` : ""}
                      {h.toStatus}
                      {h.note ? ` — ${h.note}` : ""}
                    </li>
                  ))}
                </ul>
              </div>
            ) : null}

            <div className="mt-6 flex flex-wrap justify-end gap-2">
              <Button
                type="button"
                variant="secondary"
                disabled={!canExportUpSeller || exporting}
                onClick={() => void exportUpSeller()}
              >
                {exporting ? "Gerando…" : "Exportar para UpSeller"}
              </Button>
              <Button
                type="button"
                variant="secondary"
                onClick={() => setSelected(null)}
              >
                Fechar
              </Button>
            </div>
          </div>
        </div>
      ) : null}

      <ConfirmModal
        open={pendingStatus !== null}
        title="Alterar status do pedido?"
        description={
          pendingStatus
            ? `Confirmar alteração para “${orderStatusLabels[pendingStatus]}”?`
            : ""
        }
        confirmLabel="Confirmar"
        busy={updating}
        onCancel={() => !updating && setPendingStatus(null)}
        onConfirm={() => void confirmStatusChange()}
      />
    </div>
  );
}
