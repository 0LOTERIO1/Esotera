"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { getAdminRepository } from "@/services/repositories";
import { adminApi } from "@/services/api/adminApi";
import { ApiError } from "@/services/api/apiClient";
import {
  j3EligibilityUserMessage,
  j3FulfillmentAdminApi,
} from "@/services/api/j3FulfillmentAdminApi";
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

/** Labels para status fiscal reais do backend (FiscalInvoiceStatus + awaiting_xml). */
function fiscalStatusLabel(status: string): string {
  switch (status) {
    case "authorized":
      return "Autorizado";
    case "awaiting_xml":
      return "Sem XML";
    case "unknown":
      return "XML importado, autorização não confirmada";
    default:
      return status.trim() ? status : "—";
  }
}

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
  const [importingFiscal, setImportingFiscal] = useState(false);
  const [fiscalXmlFile, setFiscalXmlFile] = useState<File | null>(null);
  const [sendingJ3, setSendingJ3] = useState(false);
  const fiscalXmlInputRef = useRef<HTMLInputElement>(null);

  function clearFiscalXmlSelection() {
    setFiscalXmlFile(null);
    if (fiscalXmlInputRef.current) {
      fiscalXmlInputRef.current.value = "";
    }
  }

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
    clearFiscalXmlSelection();
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

  async function importFiscalXml(file: File | null) {
    if (!selected || !file || importingFiscal) return;
    if (selected.fiscal.fiscalStatus === "authorized") return;
    setImportingFiscal(true);
    try {
      const result = await adminApi.importFiscalInvoiceXml(selected.id, file);
      const refreshed = await getAdminRepository().getOrder(selected.id);
      if (refreshed) setSelected(refreshed);
      clearFiscalXmlSelection();
      push(
        "success",
        result.idempotentReplay
          ? "XML já importado anteriormente (idempotente)."
          : `NF-e importada · ${result.maskedChNFe}`,
      );
    } catch (err) {
      push(
        "error",
        err instanceof ApiError
          ? err.userMessage
          : "Não foi possível importar o XML da NF-e.",
      );
    } finally {
      setImportingFiscal(false);
    }
  }

  async function sendToJ3() {
    if (!selected || sendingJ3) return;
    setSendingJ3(true);
    try {
      const result = await j3FulfillmentAdminApi.processOrder(selected.id);
      push(
        "success",
        result.processed
          ? `J3: ${result.status}`
          : `J3 (estado atual): ${result.status}`,
      );
      const refreshed = await getAdminRepository().getOrder(selected.id);
      if (refreshed) setSelected(refreshed);
    } catch (err) {
      push(
        "error",
        err instanceof ApiError
          ? err.detail ||
              j3EligibilityUserMessage(err.reasonCode) ||
              err.userMessage
          : "Não foi possível enviar para a J3.",
      );
    } finally {
      setSendingJ3(false);
    }
  }

  const canExportUpSeller =
    !!selected &&
    ["payment_approved", "preparing"].includes(selected.status);

  const isJ3Shipping =
    !!selected && selected.shipping.methodId.toLowerCase() === "j3";
  const isMelhorEnvioShipping =
    !!selected &&
    selected.shipping.methodId.toLowerCase().startsWith("melhor_");

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
                    {order.shippingServiceName
                      ? ` (${order.shippingServiceName}${
                          order.shippingCarrierName
                            ? ` · ${order.shippingCarrierName}`
                            : ""
                        })`
                      : ""}
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
              <p className="mt-2">
                {selected.address.street}, {selected.address.number}
                <br />
                {selected.address.neighborhood} · {selected.address.city}/
                {selected.address.state} · CEP {selected.address.cep}
              </p>
            </div>

            <div className="mt-4 rounded border border-esotera-border/70 p-3 text-sm">
              <h3 className="text-esotera-text">Entrega</h3>
              <dl className="mt-1 space-y-1 text-esotera-muted">
                <div>Método: {selected.shipping.methodName}</div>
                <div>
                  Serviço escolhido:{" "}
                  {selected.shipping.serviceName ?? "não registrado"}
                  {selected.shipping.serviceId
                    ? ` (id ${selected.shipping.serviceId})`
                    : ""}
                </div>
                <div>
                  Transportadora:{" "}
                  {selected.shipping.carrierName ?? "não registrada"}
                </div>
                <div>Prazo: {selected.shipping.estimatedDays}</div>
                <div>
                  Frete cobrado do cliente:{" "}
                  <Price value={selected.shippingPrice} />
                </div>
                {selected.shipping.originalPrice !== undefined ? (
                  <div>
                    Frete cotado na transportadora:{" "}
                    <Price value={selected.shipping.originalPrice} />
                    {selected.shipping.freeShippingApplied
                      ? " · frete grátis aplicado"
                      : ""}
                    {selected.shipping.subsidyApplied
                      ? " · subsídio aplicado"
                      : ""}
                  </div>
                ) : null}
                {selected.shipping.quoteEnvironment ? (
                  <div>
                    Cotação: {selected.shipping.quoteEnvironment}
                    {selected.shipping.quotedAtUtc
                      ? ` · ${formatDate(selected.shipping.quotedAtUtc)}`
                      : ""}
                  </div>
                ) : null}
              </dl>
              {isMelhorEnvioShipping ? (
                <p className="mt-2 text-xs text-esotera-muted">
                  Envio no Melhor Envio: não criado. A criação de envio e a
                  etiqueta ainda não estão implementadas.
                </p>
              ) : null}
            </div>

            <div className="mt-4 rounded border border-esotera-border/70 p-3 text-sm">
              <h3 className="text-esotera-text">NF-e</h3>
              <p className="mt-1 text-esotera-muted">
                Status: {fiscalStatusLabel(selected.fiscal.fiscalStatus)}
              </p>
              {selected.fiscal.fiscalStatus === "unknown" ? (
                <p
                  role="status"
                  className="mt-2 rounded-md border border-amber-500/40 bg-amber-50 px-3 py-2 text-amber-900"
                >
                  XML importado, mas autorização da NF-e não foi confirmada.
                </p>
              ) : null}
              {selected.fiscal.maskedChNFe ? (
                <p className="mt-1 font-mono text-xs text-esotera-muted">
                  chNFe: {selected.fiscal.maskedChNFe}
                </p>
              ) : null}
              {selected.fiscal.invoiceNumber || selected.fiscal.invoiceSeries ? (
                <p className="mt-1 text-esotera-muted">
                  Nº {selected.fiscal.invoiceNumber ?? "—"} · Série{" "}
                  {selected.fiscal.invoiceSeries ?? "—"}
                </p>
              ) : null}

              {selected.fiscal.fiscalStatus === "authorized" ? (
                <p
                  role="status"
                  className="mt-3 rounded-md border border-esotera-success/30 bg-esotera-success/5 px-3 py-2 text-esotera-success"
                >
                  NF-e já importada e autorizada.
                </p>
              ) : (
                <div className="mt-3 space-y-2">
                  <input
                    ref={fiscalXmlInputRef}
                    id="admin-fiscal-xml-input"
                    type="file"
                    accept=".xml,application/xml,text/xml"
                    className="sr-only"
                    disabled={importingFiscal}
                    onChange={(e) => {
                      const f = e.target.files?.[0] ?? null;
                      setFiscalXmlFile(f);
                    }}
                  />
                  <div className="flex flex-wrap gap-2">
                    <Button
                      type="button"
                      variant="secondary"
                      disabled={importingFiscal}
                      aria-label="Escolher arquivo XML da NF-e"
                      onClick={() => fiscalXmlInputRef.current?.click()}
                    >
                      Escolher XML
                    </Button>
                    <Button
                      type="button"
                      disabled={
                        !fiscalXmlFile ||
                        importingFiscal ||
                        selected.fiscal.fiscalStatus === "authorized"
                      }
                      onClick={() => void importFiscalXml(fiscalXmlFile)}
                    >
                      {importingFiscal ? "Importando…" : "Importar XML"}
                    </Button>
                  </div>
                  {fiscalXmlFile ? (
                    <p className="text-xs text-esotera-muted">
                      Nome do arquivo: {fiscalXmlFile.name}
                    </p>
                  ) : (
                    <p className="text-xs text-esotera-muted">
                      Selecione um arquivo .xml e clique em Importar XML.
                    </p>
                  )}
                </div>
              )}
            </div>

            {isJ3Shipping ? (
              <div className="mt-4 rounded border border-esotera-border/70 p-3 text-sm">
                <h3 className="text-esotera-text">Entrega J3</h3>
                <p className="mt-1 text-xs text-esotera-muted">
                  Envio manual para a J3 (backend é a autoridade; flag off =
                  desabilitado).
                </p>
                <div className="mt-3">
                  <Button
                    type="button"
                    disabled={sendingJ3}
                    onClick={() => void sendToJ3()}
                  >
                    {sendingJ3 ? "Enviando…" : "Enviar para J3"}
                  </Button>
                </div>
              </div>
            ) : null}

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
                onClick={() => {
                  clearFiscalXmlSelection();
                  setSelected(null);
                }}
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
