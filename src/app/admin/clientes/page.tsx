"use client";

import { useCallback, useEffect, useState } from "react";
import { getAdminRepository } from "@/services/repositories";
import { ApiError } from "@/services/api/apiClient";
import type { AdminCustomer } from "@/services/api/adminTypes";
import { Button } from "@/components/ui/Button";
import { EmptyState } from "@/components/ui/EmptyState";
import { Price } from "@/components/ui/Price";
import { formatDate } from "@/utils/format";

export default function AdminCustomersPage() {
  const [customers, setCustomers] = useState<AdminCustomer[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const list = await getAdminRepository().listCustomers();
      setCustomers(list);
    } catch (err) {
      setCustomers([]);
      setError(
        err instanceof ApiError
          ? err.userMessage
          : err instanceof Error
            ? err.message
            : "Não foi possível carregar os clientes.",
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

  return (
    <div>
      <h1 className="font-serif text-3xl text-esotera-secondary">Clientes</h1>
      <p className="mt-1 text-sm text-esotera-muted">
        Clientes com pedidos — sem CPF, senha ou dados de pagamento.
      </p>

      {loading ? (
        <p className="mt-6 text-sm text-esotera-muted">Carregando…</p>
      ) : error ? (
        <div className="mt-6">
          <EmptyState
            title="Erro ao carregar clientes"
            description={error}
            action={
              <Button type="button" onClick={() => void load()}>
                Tentar novamente
              </Button>
            }
          />
        </div>
      ) : !customers.length ? (
        <div className="mt-6">
          <EmptyState title="Nenhum cliente com pedidos" />
        </div>
      ) : (
        <ul className="mt-6 space-y-3">
          {customers.map((customer) => (
            <li
              key={customer.id}
              className="rounded-lg border border-esotera-border p-4 text-sm"
            >
              <p className="text-esotera-text">{customer.name}</p>
              <p className="text-esotera-muted">
                {customer.email}
                {customer.phone ? ` · ${customer.phone}` : ""}
              </p>
              <p className="mt-1 text-xs text-esotera-muted">
                {customer.orderCount} pedido(s) · total{" "}
                <Price value={customer.totalSpent} />
                {customer.lastOrderAt
                  ? ` · último em ${formatDate(customer.lastOrderAt)}`
                  : ""}
              </p>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
