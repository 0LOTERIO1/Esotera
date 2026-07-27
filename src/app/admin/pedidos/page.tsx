"use client";

import { useMemo, useState } from "react";
import { useOrdersStore } from "@/stores/ordersStore";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { Price } from "@/components/ui/Price";
import { FormField, inputClassName } from "@/components/ui/FormField";
import { Button } from "@/components/ui/Button";
import { EmptyState } from "@/components/ui/EmptyState";
import { formatDate } from "@/utils/format";
import { orderStatusLabels } from "@/utils/labels";
import type { Order, OrderStatus } from "@/types";
import { useToastStore } from "@/stores/toastStore";

export default function AdminOrdersPage() {
  const orders = useOrdersStore((s) => s.orders);
  const updateStatus = useOrdersStore((s) => s.updateStatus);
  const push = useToastStore((s) => s.push);
  const [query, setQuery] = useState("");
  const [status, setStatus] = useState("all");
  const [selected, setSelected] = useState<Order | null>(null);

  const filtered = useMemo(() => {
    return orders.filter((order) => {
      const matchQuery =
        !query.trim() ||
        order.id.toLowerCase().includes(query.toLowerCase()) ||
        order.upSellerExport?.customerName
          .toLowerCase()
          .includes(query.toLowerCase());
      const matchStatus = status === "all" || order.status === status;
      return matchQuery && matchStatus;
    });
  }, [orders, query, status]);

  return (
    <div>
      <h1 className="font-serif text-3xl text-esotera-white">Pedidos</h1>
      <div className="mt-6 grid gap-3 sm:grid-cols-2">
        <FormField label="Pesquisar" id="order-search">
          <input
            id="order-search"
            className={inputClassName}
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="ID ou cliente"
          />
        </FormField>
        <FormField label="Status" id="order-status">
          <select
            id="order-status"
            className={inputClassName}
            value={status}
            onChange={(e) => setStatus(e.target.value)}
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

      {!filtered.length ? (
        <div className="mt-8">
          <EmptyState title="Nenhum pedido encontrado" />
        </div>
      ) : (
        <ul className="mt-6 space-y-3">
          {filtered.map((order) => (
            <li
              key={order.id}
              className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-esotera-graphite p-4"
            >
              <div>
                <p className="text-esotera-beige">{order.id}</p>
                <p className="text-xs text-esotera-muted">
                  {order.upSellerExport?.customerName} · {formatDate(order.createdAt)}
                </p>
              </div>
              <StatusBadge status={order.status} />
              <Price value={order.total} />
              <Button type="button" variant="secondary" onClick={() => setSelected(order)}>
                Detalhes
              </Button>
            </li>
          ))}
        </ul>
      )}

      {selected ? (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4">
          <div
            role="dialog"
            aria-modal
            className="max-h-[90vh] w-full max-w-lg overflow-y-auto rounded-lg border border-esotera-graphite bg-esotera-navy p-6"
          >
            <h2 className="font-serif text-xl text-esotera-white">
              {selected.id}
            </h2>
            <p className="mt-2 text-sm text-esotera-muted">
              Cliente: {selected.upSellerExport?.customerName}
              <br />
              E-mail: {selected.upSellerExport?.customerEmail}
              <br />
              Telefone: {selected.upSellerExport?.customerPhone}
              <br />
              CPF: {selected.upSellerExport?.customerCpf}
            </p>
            <p className="mt-3 text-xs text-esotera-muted">
              Dados organizados para futura exportação ao UpSeller (não
              implementada nesta etapa).
            </p>
            <FormField label="Alterar status" id="change-status">
              <select
                id="change-status"
                className={inputClassName}
                value={selected.status}
                onChange={(e) => {
                  const next = e.target.value as OrderStatus;
                  updateStatus(selected.id, next);
                  setSelected({ ...selected, status: next });
                  push("success", "Status atualizado.");
                }}
              >
                {(Object.keys(orderStatusLabels) as OrderStatus[]).map((key) => (
                  <option key={key} value={key}>
                    {orderStatusLabels[key]}
                  </option>
                ))}
              </select>
            </FormField>
            <ul className="mt-4 space-y-2 text-sm text-esotera-muted">
              {selected.items.map((item) => (
                <li key={`${item.productId}-${item.variation ?? ""}`}>
                  {item.quantity}× {item.name} — <Price value={item.price} />
                </li>
              ))}
            </ul>
            <div className="mt-6 flex justify-end">
              <Button type="button" variant="secondary" onClick={() => setSelected(null)}>
                Fechar
              </Button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}
