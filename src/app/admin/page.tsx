"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { getAdminRepository } from "@/services/repositories";
import { ApiError } from "@/services/api/apiClient";
import type { AdminDashboard } from "@/services/api/adminTypes";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { Price } from "@/components/ui/Price";
import { Button } from "@/components/ui/Button";
import { ProductImage } from "@/components/ui/ProductImage";
import { EmptyState } from "@/components/ui/EmptyState";
import { formatCurrency, formatDate } from "@/utils/format";

export default function AdminDashboardPage() {
  const [dashboard, setDashboard] = useState<AdminDashboard | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getAdminRepository().getDashboard();
      setDashboard(data);
    } catch (err) {
      setDashboard(null);
      setError(
        err instanceof ApiError
          ? err.userMessage
          : err instanceof Error
            ? err.message
            : "Não foi possível carregar o dashboard.",
      );
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void load();
    }, 0);
    return () => window.clearTimeout(timer);
  }, [load]);

  if (loading) {
    return (
      <p className="text-sm text-esotera-muted">Carregando dashboard…</p>
    );
  }

  if (error || !dashboard) {
    return (
      <EmptyState
        title="Erro ao carregar dashboard"
        description={error ?? undefined}
        action={
          <Button type="button" onClick={() => void load()}>
            Tentar novamente
          </Button>
        }
      />
    );
  }

  const maxBar = Math.max(...dashboard.recentOrders.map((o) => o.total), 1);
  const cards = [
    { label: "Total de pedidos", value: String(dashboard.totalOrders) },
    { label: "Vendas", value: formatCurrency(dashboard.totalSales) },
    {
      label: "Aguardando pagamento",
      value: String(dashboard.awaitingPayment),
    },
    {
      label: "Pagamento aprovado",
      value: String(dashboard.paymentApproved),
    },
    { label: "Em preparação", value: String(dashboard.preparing) },
    { label: "Enviados", value: String(dashboard.shipped) },
    { label: "Entregues", value: String(dashboard.delivered) },
    { label: "Cancelados", value: String(dashboard.cancelled) },
    {
      label: "Produtos disponíveis",
      value: String(dashboard.availableProducts),
    },
    {
      label: "Clientes com pedidos",
      value: String(dashboard.customersWithOrders),
    },
  ];

  return (
    <div>
      <h1 className="font-serif text-3xl text-esotera-secondary">Dashboard</h1>
      <p className="mt-1 text-sm text-esotera-muted">
        Resumo operacional da loja. Totais excluem pedidos cancelados.
      </p>

      <div className="mt-8 grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
        {cards.map((card) => (
          <div
            key={card.label}
            className="rounded-lg border border-esotera-border bg-esotera-surface p-4"
          >
            <p className="text-xs text-esotera-muted">{card.label}</p>
            <p className="mt-2 font-serif text-2xl text-esotera-primary">
              {card.value}
            </p>
          </div>
        ))}
      </div>

      <section className="mt-8 rounded-lg border border-esotera-border p-5">
        <h2 className="font-serif text-xl text-esotera-text">
          Vendas recentes
        </h2>
        {!dashboard.recentOrders.length ? (
          <p className="mt-4 text-sm text-esotera-muted">
            Ainda não há pedidos.
          </p>
        ) : (
          <ul className="mt-6 space-y-3">
            {dashboard.recentOrders.map((order) => (
              <li key={order.id} className="flex items-center gap-3 text-sm">
                <span className="w-28 truncate text-esotera-muted">
                  {order.orderNumber}
                </span>
                <div className="h-2 flex-1 rounded bg-esotera-graphite/50">
                  <div
                    className="h-2 rounded bg-esotera-primary/70"
                    style={{ width: `${(order.total / maxBar) * 100}%` }}
                  />
                </div>
                <Price value={order.total} />
              </li>
            ))}
          </ul>
        )}
      </section>

      <section className="mt-8">
        <div className="flex items-center justify-between gap-3">
          <h2 className="font-serif text-xl text-esotera-text">
            Pedidos recentes
          </h2>
          <Link
            href="/admin/pedidos"
            className="text-sm text-esotera-primary hover:underline"
          >
            Ver todos
          </Link>
        </div>
        <ul className="mt-4 space-y-2">
          {dashboard.recentOrders.map((order) => (
            <li
              key={order.id}
              className="flex flex-wrap items-center justify-between gap-2 rounded-md border border-esotera-border px-4 py-3 text-sm"
            >
              <Link
                href="/admin/pedidos"
                className="text-esotera-text hover:text-esotera-primary"
              >
                {order.orderNumber}
              </Link>
              <span className="text-esotera-muted">
                {order.customerName} · {formatDate(order.createdAt)}
              </span>
              <StatusBadge status={order.status} />
              <Price value={order.total} />
            </li>
          ))}
        </ul>
      </section>

      <section className="mt-8">
        <h2 className="font-serif text-xl text-esotera-text">
          Produtos mais vendidos
        </h2>
        {!dashboard.topProducts.length ? (
          <p className="mt-4 text-sm text-esotera-muted">
            Nenhuma venda registrada.
          </p>
        ) : (
          <ul className="mt-4 space-y-3">
            {dashboard.topProducts.map((product) => (
              <li
                key={`${product.productId ?? product.productName}-${product.image}`}
                className="flex items-center gap-3 rounded-lg border border-esotera-border p-3"
              >
                <div className="relative h-14 w-11 shrink-0 overflow-hidden rounded">
                  <ProductImage
                    src={product.image}
                    alt={product.productName}
                    fill
                    className="object-cover"
                    sizes="44px"
                  />
                </div>
                <div className="min-w-0 flex-1 text-sm">
                  <p className="truncate text-esotera-text">
                    {product.productName}
                  </p>
                  <p className="text-xs text-esotera-muted">
                    {product.quantitySold} un. · {product.orderCount} pedido(s)
                  </p>
                </div>
                <Price value={product.totalRevenue} />
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}
