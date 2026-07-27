"use client";

import Link from "next/link";
import { useMemo } from "react";
import { useOrdersStore } from "@/stores/ordersStore";
import { useProductsStore } from "@/stores/productsStore";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { Price } from "@/components/ui/Price";
import { formatCurrency, formatDate } from "@/utils/format";

export default function AdminDashboardPage() {
  const orders = useOrdersStore((s) => s.orders);
  const products = useProductsStore((s) => s.products);

  const stats = useMemo(() => {
    const sales = orders
      .filter((o) => o.status !== "cancelled")
      .reduce((sum, o) => sum + o.total, 0);
    const available = products.filter((p) => p.isAvailable).length;
    return {
      orderCount: orders.length,
      sales,
      available,
      recent: orders.slice(0, 5),
    };
  }, [orders, products]);

  const maxBar = Math.max(...stats.recent.map((o) => o.total), 1);

  return (
    <div>
      <h1 className="font-serif text-3xl text-esotera-white">Dashboard</h1>
      <p className="mt-1 text-sm text-esotera-muted">
        Visão simulada do painel administrativo.
      </p>

      <div className="mt-8 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {[
          { label: "Total de pedidos", value: String(stats.orderCount) },
          {
            label: "Vendas simuladas",
            value: formatCurrency(stats.sales),
          },
          {
            label: "Produtos disponíveis",
            value: String(stats.available),
          },
          {
            label: "Produtos no catálogo",
            value: String(products.length),
          },
        ].map((card) => (
          <div
            key={card.label}
            className="rounded-lg border border-esotera-graphite bg-esotera-black/30 p-4"
          >
            <p className="text-xs text-esotera-muted">{card.label}</p>
            <p className="mt-2 font-serif text-2xl text-esotera-gold">
              {card.value}
            </p>
          </div>
        ))}
      </div>

      <section className="mt-8 rounded-lg border border-esotera-graphite p-5">
        <h2 className="font-serif text-xl text-esotera-beige">
          Vendas recentes (gráfico simples)
        </h2>
        {!stats.recent.length ? (
          <p className="mt-4 text-sm text-esotera-muted">
            Ainda não há pedidos simulados.
          </p>
        ) : (
          <ul className="mt-6 space-y-3">
            {stats.recent.map((order) => (
              <li key={order.id} className="flex items-center gap-3 text-sm">
                <span className="w-28 truncate text-esotera-muted">
                  {order.id.slice(-8)}
                </span>
                <div className="h-2 flex-1 rounded bg-esotera-graphite/50">
                  <div
                    className="h-2 rounded bg-esotera-gold/70"
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
        <h2 className="font-serif text-xl text-esotera-beige">
          Pedidos recentes
        </h2>
        <ul className="mt-4 space-y-2">
          {stats.recent.map((order) => (
            <li
              key={order.id}
              className="flex flex-wrap items-center justify-between gap-2 rounded-md border border-esotera-graphite px-4 py-3 text-sm"
            >
              <Link
                href="/admin/pedidos"
                className="text-esotera-beige hover:text-esotera-gold"
              >
                {order.id}
              </Link>
              <span className="text-esotera-muted">
                {formatDate(order.createdAt)}
              </span>
              <StatusBadge status={order.status} />
              <Price value={order.total} />
            </li>
          ))}
        </ul>
      </section>
    </div>
  );
}
