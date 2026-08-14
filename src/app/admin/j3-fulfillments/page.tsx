"use client";

import { useCallback, useEffect, useState } from "react";
import { Button } from "@/components/ui/Button";
import { FormField, inputClassName } from "@/components/ui/FormField";
import { ApiError } from "@/services/api/apiClient";
import {
  j3FulfillmentAdminApi,
  type J3FulfillmentAdminDetail,
  type J3FulfillmentAdminListItem,
} from "@/services/api/j3FulfillmentAdminApi";
import { sessionService } from "@/services/api/sessionService";
import { useAuthStore } from "@/stores/authStore";
import { formatDate } from "@/utils/format";

const STATUS_OPTIONS = [
  { value: "", label: "Todos" },
  { value: "pending", label: "Pending" },
  { value: "processing", label: "Processing" },
  { value: "created", label: "Created" },
  { value: "retryable_failure", label: "Retryable Failure" },
  { value: "unknown_outcome", label: "Unknown Outcome" },
] as const;

const STATUS_STYLES: Record<string, string> = {
  pending: "border-esotera-border text-esotera-secondary bg-esotera-surface-secondary",
  processing: "border-sky-500/40 text-sky-800 bg-sky-50",
  created: "border-esotera-success/40 text-esotera-success bg-esotera-success/10",
  retryable_failure: "border-amber-500/40 text-amber-800 bg-amber-50",
  unknown_outcome: "border-esotera-error/40 text-esotera-error bg-esotera-error/10",
};

const STATUS_LABELS: Record<string, string> = {
  pending: "Pending",
  processing: "Processing",
  created: "Created",
  retryable_failure: "Retryable Failure",
  unknown_outcome: "Unknown Outcome",
};

function J3StatusBadge({ status }: { status: string }) {
  const style =
    STATUS_STYLES[status] ??
    "border-esotera-border text-esotera-muted bg-esotera-surface-secondary";
  return (
    <span className={`inline-flex rounded border px-2 py-0.5 text-xs ${style}`}>
      {STATUS_LABELS[status] ?? status}
    </span>
  );
}

export default function AdminJ3FulfillmentsPage() {
  const user = useAuthStore((s) => s.user);
  const hydrated = useAuthStore((s) => s.hydrated);
  const sessionReady = useAuthStore((s) => s.sessionReady);
  const authReady = hydrated && sessionReady;
  const isAdmin = user?.role?.toLowerCase() === "admin";

  const [status, setStatus] = useState("");
  const [orderId, setOrderId] = useState("");
  const [trackingNumber, setTrackingNumber] = useState("");
  const [page, setPage] = useState(1);
  const [items, setItems] = useState<J3FulfillmentAdminListItem[]>([]);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selected, setSelected] = useState<J3FulfillmentAdminDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);

  const load = useCallback(async () => {
    if (!authReady || !isAdmin) return;
    if (!sessionService.getToken()) {
      setItems([]);
      setTotalCount(0);
      setError(
        "Esta tela exige sessão na API (JWT). Faça login com NEXT_PUBLIC_DATA_MODE=api.",
      );
      setLoading(false);
      return;
    }

    setLoading(true);
    setError(null);
    try {
      const result = await j3FulfillmentAdminApi.list({
        status: status || undefined,
        orderId: orderId.trim() || undefined,
        trackingNumber: trackingNumber.trim() || undefined,
        page,
        pageSize: 20,
      });
      setItems(result.items);
      setTotalPages(Math.max(1, result.totalPages));
      setTotalCount(result.totalCount);
    } catch (err) {
      setItems([]);
      setError(
        err instanceof ApiError
          ? err.userMessage
          : err instanceof Error
            ? err.message
            : "Não foi possível carregar os fulfillments J3.",
      );
    } finally {
      setLoading(false);
    }
  }, [authReady, isAdmin, status, orderId, trackingNumber, page]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void load();
    }, 0);
    return () => window.clearTimeout(timer);
  }, [load]);

  async function openDetail(id: string) {
    setDetailLoading(true);
    try {
      const detail = await j3FulfillmentAdminApi.get(id);
      setSelected(detail);
    } catch (err) {
      setSelected(null);
      setError(
        err instanceof ApiError
          ? err.userMessage
          : "Fulfillment não encontrado.",
      );
    } finally {
      setDetailLoading(false);
    }
  }

  if (!authReady) {
    return <p className="text-sm text-esotera-muted">Carregando sessão…</p>;
  }

  return (
    <div className="grid gap-8 lg:grid-cols-[minmax(0,1fr)_22rem]">
      <div>
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div>
            <h1 className="font-serif text-3xl text-esotera-secondary">
              Entregas J3
            </h1>
            <p className="mt-1 text-sm text-esotera-muted">
              Somente leitura · {totalCount} registro(s)
            </p>
          </div>
          <Button type="button" variant="secondary" onClick={() => void load()}>
            Atualizar
          </Button>
        </div>

        {error ? (
          <p role="alert" className="mt-4 text-sm text-esotera-error">
            {error}
          </p>
        ) : null}

        <div className="mt-6 grid gap-3 sm:grid-cols-3">
          <FormField label="Status" id="j3-status">
            <select
              id="j3-status"
              className={inputClassName}
              value={status}
              onChange={(e) => {
                setPage(1);
                setStatus(e.target.value);
              }}
            >
              {STATUS_OPTIONS.map((opt) => (
                <option key={opt.value || "all"} value={opt.value}>
                  {opt.label}
                </option>
              ))}
            </select>
          </FormField>
          <FormField label="Order ID" id="j3-order-id">
            <input
              id="j3-order-id"
              className={inputClassName}
              value={orderId}
              onChange={(e) => {
                setPage(1);
                setOrderId(e.target.value);
              }}
              placeholder="GUID do pedido"
            />
          </FormField>
          <FormField label="Tracking" id="j3-tracking">
            <input
              id="j3-tracking"
              className={inputClassName}
              value={trackingNumber}
              onChange={(e) => {
                setPage(1);
                setTrackingNumber(e.target.value);
              }}
              placeholder="J3 tracking"
            />
          </FormField>
        </div>

        <div className="mt-6 overflow-x-auto">
          <table className="min-w-full text-left text-sm">
            <thead className="border-b border-esotera-border text-esotera-muted">
              <tr>
                <th className="px-2 py-2 font-medium">Pedido Esotera</th>
                <th className="px-2 py-2 font-medium">Status J3</th>
                <th className="px-2 py-2 font-medium">J3 Order</th>
                <th className="px-2 py-2 font-medium">Tracking</th>
                <th className="px-2 py-2 font-medium">Tentativas</th>
                <th className="px-2 py-2 font-medium">Último erro</th>
                <th className="px-2 py-2 font-medium">Atualizado em</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={7} className="px-2 py-6 text-esotera-muted">
                    Carregando…
                  </td>
                </tr>
              ) : items.length === 0 ? (
                <tr>
                  <td colSpan={7} className="px-2 py-6 text-esotera-muted">
                    Nenhum fulfillment J3 encontrado.
                  </td>
                </tr>
              ) : (
                items.map((item) => (
                  <tr
                    key={item.id}
                    className="cursor-pointer border-b border-esotera-border/60 hover:bg-esotera-surface-secondary/60"
                    onClick={() => void openDetail(item.id)}
                  >
                    <td className="px-2 py-2 font-medium text-esotera-text">
                      {item.orderNumber}
                    </td>
                    <td className="px-2 py-2">
                      <J3StatusBadge status={item.status} />
                    </td>
                    <td className="px-2 py-2 text-esotera-muted">
                      {item.j3OrderCode || item.j3OrderId || "—"}
                    </td>
                    <td className="px-2 py-2 text-esotera-muted">
                      {item.j3TrackingNumber || "—"}
                    </td>
                    <td className="px-2 py-2">{item.attemptCount}</td>
                    <td className="px-2 py-2 text-esotera-muted">
                      {item.lastErrorCode || "—"}
                    </td>
                    <td className="px-2 py-2 text-esotera-muted">
                      {formatDate(item.updatedAtUtc)}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {totalPages > 1 ? (
          <div className="mt-4 flex gap-2">
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
        ) : null}
      </div>

      <aside className="rounded-lg border border-esotera-border bg-esotera-surface p-4">
        {detailLoading ? (
          <p className="text-sm text-esotera-muted">Carregando detalhe…</p>
        ) : selected ? (
          <J3FulfillmentDetail detail={selected} />
        ) : (
          <p className="text-sm text-esotera-muted">
            Selecione um registro para ver o detalhe operacional.
          </p>
        )}
      </aside>
    </div>
  );
}

function J3FulfillmentDetail({ detail }: { detail: J3FulfillmentAdminDetail }) {
  return (
    <div className="space-y-4 text-sm">
      <div>
        <h2 className="font-serif text-xl text-esotera-secondary">
          {detail.orderNumber}
        </h2>
        <p className="mt-1">
          <J3StatusBadge status={detail.status} />
        </p>
      </div>

      {detail.status === "unknown_outcome" ? (
        <p
          role="alert"
          className="rounded-md border border-esotera-error/40 bg-esotera-error/10 px-3 py-2 text-esotera-error"
        >
          Resultado da criação na J3 é desconhecido. Não tente criar novamente
          sem verificar manualmente o portal J3.
        </p>
      ) : null}

      {detail.isPossiblyStuck ? (
        <p
          role="alert"
          className="rounded-md border border-sky-500/40 bg-sky-50 px-3 py-2 text-sky-900"
        >
          Processamento pode ter sido concluído remotamente. Não reenvie
          automaticamente.
        </p>
      ) : null}

      {detail.status === "retryable_failure" ? (
        <p
          role="alert"
          className="rounded-md border border-amber-500/40 bg-amber-50 px-3 py-2 text-amber-900"
        >
          Falha ocorreu antes de uma criação remota confirmada. Pode ser
          elegível para nova tentativa manual futuramente.
        </p>
      ) : null}

      {detail.status === "created" ? (
        <div className="rounded-md border border-esotera-success/30 bg-esotera-success/5 px-3 py-2">
          <p className="font-medium text-esotera-success">Criado na J3</p>
          <p className="mt-1 text-esotera-muted">
            J3 Order ID: {detail.j3OrderId || "—"}
          </p>
          <p className="text-esotera-muted">
            J3 Order Code: {detail.j3OrderCode || "—"}
          </p>
          <p className="text-esotera-muted">
            Tracking: {detail.j3TrackingNumber || "—"}
          </p>
        </div>
      ) : null}

      <dl className="grid gap-2">
        <Row label="Fulfillment ID" value={detail.id} />
        <Row label="Pedido ID" value={detail.orderId} />
        <Row label="Frete" value={detail.shippingMethodId} />
        <Row label="Status pedido" value={detail.orderStatus} />
        <Row label="Pagamento" value={detail.paymentStatus} />
        <Row label="Delivery point" value={detail.j3DeliveryPointId || "—"} />
        <Row label="Tentativas" value={String(detail.attemptCount)} />
        <Row label="Último erro" value={detail.lastErrorCode || "—"} />
        <Row
          label="Erro em"
          value={detail.lastErrorAtUtc ? formatDate(detail.lastErrorAtUtc) : "—"}
        />
        <Row label="Criado em" value={formatDate(detail.createdAtUtc)} />
        <Row label="Atualizado em" value={formatDate(detail.updatedAtUtc)} />
        <Row
          label="Concluído em"
          value={detail.completedAtUtc ? formatDate(detail.completedAtUtc) : "—"}
        />
        <Row
          label="CanRetrySafely"
          value={detail.canRetrySafely ? "sim" : "não"}
        />
        <Row
          label="NeedsManualReview"
          value={detail.needsManualReview ? "sim" : "não"}
        />
        <Row
          label="IsPossiblyStuck"
          value={detail.isPossiblyStuck ? "sim" : "não"}
        />
        <Row label="Etiqueta" value="ainda não implementada" />
      </dl>
    </div>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs text-esotera-muted">{label}</dt>
      <dd className="break-all text-esotera-text">{value}</dd>
    </div>
  );
}
