"use client";

import Link from "next/link";
import { useEffect, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { useAuthStore } from "@/stores/authStore";
import { useOrdersStore } from "@/stores/ordersStore";
import { Button, ButtonLink } from "@/components/ui/Button";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { Price } from "@/components/ui/Price";
import { EmptyState } from "@/components/ui/EmptyState";
import { formatDate } from "@/utils/format";
import { useToastStore } from "@/stores/toastStore";

export default function AccountPage() {
  const router = useRouter();
  const user = useAuthStore((s) => s.user);
  const hydrated = useAuthStore((s) => s.hydrated);
  const logout = useAuthStore((s) => s.logout);
  const allOrders = useOrdersStore((s) => s.orders);
  const orders = useMemo(
    () => (user ? allOrders.filter((o) => o.userId === user.id) : []),
    [allOrders, user],
  );
  const push = useToastStore((s) => s.push);
  const [returnRequest, setReturnRequest] = useState(false);

  useEffect(() => {
    if (hydrated && !user) {
      router.replace("/login?returnUrl=/minha-conta");
    }
  }, [hydrated, user, router]);

  if (!hydrated || !user) {
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
          <h1 className="font-serif text-4xl text-esotera-white">Minha conta</h1>
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
              logout();
              push("info", "Sessão encerrada.");
              router.push("/");
            }}
          >
            Sair
          </Button>
        </div>
      </div>

      <div className="mt-8 grid gap-6 lg:grid-cols-2">
        <section className="rounded-lg border border-esotera-graphite p-5">
          <h2 className="font-serif text-xl text-esotera-beige">Perfil</h2>
          <dl className="mt-4 space-y-2 text-sm text-esotera-muted">
            <div>
              <dt className="text-esotera-beige">E-mail</dt>
              <dd>{user.email}</dd>
            </div>
            <div>
              <dt className="text-esotera-beige">CPF</dt>
              <dd>{user.cpf}</dd>
            </div>
            <div>
              <dt className="text-esotera-beige">Telefone</dt>
              <dd>{user.phone}</dd>
            </div>
          </dl>
        </section>

        <section className="rounded-lg border border-esotera-graphite p-5">
          <h2 className="font-serif text-xl text-esotera-beige">Endereços</h2>
          <p className="mt-4 text-sm text-esotera-muted">
            {user.address.street}, {user.address.number}
            {user.address.complement ? ` — ${user.address.complement}` : ""}
            <br />
            {user.address.neighborhood} · {user.address.city}/
            {user.address.state}
            <br />
            CEP {user.address.cep}
          </p>
        </section>
      </div>

      <section className="mt-8">
        <h2 className="font-serif text-2xl text-esotera-white">
          Histórico de pedidos
        </h2>
        {!orders.length ? (
          <div className="mt-4">
            <EmptyState
              title="Nenhum pedido ainda"
              description="Finalize uma compra simulada para ver o histórico."
              action={<ButtonLink href="/produtos">Ver produtos</ButtonLink>}
            />
          </div>
        ) : (
          <ul className="mt-4 space-y-3">
            {orders.map((order) => (
              <li
                key={order.id}
                className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-esotera-graphite p-4"
              >
                <div>
                  <Link
                    href={`/minha-conta/pedidos/${order.id}`}
                    className="text-esotera-beige hover:text-esotera-gold"
                  >
                    {order.id}
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

      <section className="mt-8 rounded-lg border border-esotera-graphite p-5">
        <h2 className="font-serif text-xl text-esotera-beige">
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
