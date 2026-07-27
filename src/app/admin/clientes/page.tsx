"use client";

import { useMemo } from "react";
import { mockAuthService } from "@/services/auth/mockAuthService";
import { useOrdersStore } from "@/stores/ordersStore";
import { useAuthStore } from "@/stores/authStore";

export default function AdminCustomersPage() {
  const currentUser = useAuthStore((s) => s.user);
  const orders = useOrdersStore((s) => s.orders);

  const customers = useMemo(() => {
    const users = mockAuthService.listUsers().filter((u) => u.role === "customer");
    // Inclui usuário atual se for cliente cadastrado na sessão (já coberto pela lista)
    void currentUser;
    return users.map((user) => ({
      ...user,
      orderCount: orders.filter((o) => o.userId === user.id).length,
    }));
  }, [orders, currentUser]);

  return (
    <div>
      <h1 className="font-serif text-3xl text-esotera-white">Clientes</h1>
      <p className="mt-1 text-sm text-esotera-muted">
        Lista simulada a partir do localStorage e usuários de demonstração.
      </p>
      <ul className="mt-6 space-y-3">
        {customers.map((customer) => (
          <li
            key={customer.id}
            className="rounded-lg border border-esotera-graphite p-4 text-sm"
          >
            <p className="text-esotera-beige">{customer.name}</p>
            <p className="text-esotera-muted">
              {customer.email} · {customer.phone}
            </p>
            <p className="mt-1 text-xs text-esotera-muted">
              {customer.address.city}/{customer.address.state} ·{" "}
              {customer.orderCount} pedido(s)
            </p>
          </li>
        ))}
      </ul>
    </div>
  );
}
