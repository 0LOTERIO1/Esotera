"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useAuthStore } from "@/stores/authStore";
import { useOrdersStore } from "@/stores/ordersStore";
import { Button, ButtonLink } from "@/components/ui/Button";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { Price } from "@/components/ui/Price";
import { EmptyState } from "@/components/ui/EmptyState";
import { AddressSection } from "@/components/account/AddressSection";
import { formatDate } from "@/utils/format";
import { useToastStore } from "@/stores/toastStore";
import { ApiError } from "@/services/api/apiClient";
import type { OrderListItem } from "@/services/api/ordersApi.types";

export default function AccountPage() {
  const router = useRouter();
  const user = useAuthStore((s) => s.user);
  const hydrated = useAuthStore((s) => s.hydrated);
  const sessionReady = useAuthStore((s) => s.sessionReady);
  const logout = useAuthStore((s) => s.logout);
  const authReady = hydrated && sessionReady;
  const fetchMineSummaries = useOrdersStore((s) => s.fetchMineSummaries);
  const push = useToastStore((s) => s.push);
  const [returnRequest, setReturnRequest] = useState(false);
  const [orders, setOrders] = useState<OrderListItem[]>([]);
  const [loadingOrders, setLoadingOrders] = useState(true);
  const [ordersError, setOrdersError] = useState<string | null>(null);

  const loadOrders = useCallback(async () => {
    if (!user) return;
    setLoadingOrders(true);
    setOrdersError(null);
    try {
      const list = await fetchMineSummaries(user.id);
      setOrders(list);
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) {
        await logout();
        router.replace("/login?returnUrl=/minha-conta");
        return;
      }
      setOrdersError(
        err instanceof ApiError
          ? err.userMessage
          : err instanceof Error
            ? err.message
            : "Não foi possível carregar os pedidos.",
      );
      setOrders([]);
    } finally {
      setLoadingOrders(false);
    }
  }, [fetchMineSummaries, logout, router, user]);

  useEffect(() => {
    if (authReady && !user) {
      router.replace("/login?returnUrl=/minha-conta");
    }
  }, [authReady, user, router]);

  useEffect(() => {
    if (authReady && user) {
      const timer = window.setTimeout(() => {
        void loadOrders();
      }, 0);
      return () => window.clearTimeout(timer);
    }
  }, [authReady, user, loadOrders]);

  if (!authReady || !user) {
    return (
      <div className="px-4 py-16 text-center text-esotera-muted">
        Carregando conta…
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-6xl px-4 py-10 sm:px-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="font-serif text-4xl text-esotera-secondary">Minha conta</h1>
          <p className="mt-2 text-sm text-esotera-muted">Olá, {user.name}</p>
        </div>
        <div className="flex flex-wrap gap-2">
          {user.role === "admin" ? (
            <ButtonLink href="/admin" variant="secondary">
              Painel admin
            </ButtonLink>
          ) : null}
          <Button
            type="button"
            variant="ghost"
            onClick={() => {
              void (async () => {
                await logout();
                push("info", "Sessão encerrada.");
                router.push("/");
              })();
            }}
          >
            Sair
          </Button>
        </div>
      </div>

      <div className="mt-8 grid gap-6 lg:grid-cols-2">
        <section className="rounded-lg border border-esotera-border p-5">
          <h2 className="font-serif text-xl text-esotera-text">Perfil</h2>
          <dl className="mt-4 space-y-2 text-sm text-esotera-muted">
            <div>
              <dt className="text-esotera-text">E-mail</dt>
              <dd>{user.email}</dd>
            </div>
            <div>
              <dt className="text-esotera-text">CPF</dt>
              <dd>{user.cpf}</dd>
            </div>
            <div>
              <dt className="text-esotera-text">Telefone</dt>
              <dd>{user.phone}</dd>
            </div>
          </dl>
        </section>

        <AddressSection />
      </div>

      <section className="mt-8">
        <h2 className="font-serif text-2xl text-esotera-secondary">
          Histórico de pedidos
        </h2>
        {loadingOrders ? (
          <p className="mt-4 text-sm text-esotera-muted">Carregando pedidos…</p>
        ) : ordersError ? (
          <div className="mt-4">
            <EmptyState
              title="Erro ao carregar pedidos"
              description={ordersError}
              action={
                <Button type="button" onClick={() => void loadOrders()}>
                  Tentar novamente
                </Button>
              }
            />
          </div>
        ) : !orders.length ? (
          <div className="mt-4">
            <EmptyState
              title="Nenhum pedido ainda"
              description="Finalize uma compra para ver o histórico."
              action={<ButtonLink href="/produtos">Ver produtos</ButtonLink>}
            />
          </div>
        ) : (
          <ul className="mt-4 space-y-3">
            {orders.map((order) => (
              <li
                key={order.id}
                className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-esotera-border p-4"
              >
                <div>
                  <Link
                    href={`/minha-conta/pedidos/${order.id}`}
                    className="text-esotera-text hover:text-esotera-primary"
                  >
                    {order.orderNumber || order.id}
                  </Link>
                  <p className="text-xs text-esotera-muted">
                    {formatDate(order.createdAt)}
                  </p>
                </div>
                <div className="flex items-center gap-3">
                  <StatusBadge status={order.status} />
                  <Price value={order.total} />
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section className="mt-8 rounded-lg border border-esotera-border p-5">
        <h2 className="font-serif text-xl text-esotera-text">
          Troca ou devolução
        </h2>
        <p className="mt-2 text-sm text-esotera-muted">
          Solicitação apenas visual neste protótipo — nenhum processo real é
          iniciado.
        </p>
        <Button
          type="button"
          variant="secondary"
          className="mt-4"
          onClick={() => {
            setReturnRequest(true);
            push("info", "Solicitação visual registrada.");
          }}
        >
          Solicitar troca/devolução
        </Button>
        {returnRequest ? (
          <p role="status" className="mt-3 text-xs text-esotera-success">
            Pedido de troca/devolução simulado enviado.
          </p>
        ) : null}
      </section>
    </div>
  );
}
