"use client";

import { ProductImage } from "@/components/ui/ProductImage";
import { use, useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuthStore } from "@/stores/authStore";
import { useOrdersStore } from "@/stores/ordersStore";
import { ButtonLink } from "@/components/ui/Button";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { Price } from "@/components/ui/Price";
import { EmptyState } from "@/components/ui/EmptyState";
import { paymentMethodLabels } from "@/utils/labels";
import { formatDate } from "@/utils/format";

export default function OrderDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = use(params);
  const router = useRouter();
  const user = useAuthStore((s) => s.user);
  const hydrated = useAuthStore((s) => s.hydrated);
  const order = useOrdersStore((s) => s.getById(id));

  useEffect(() => {
    if (hydrated && !user) {
      router.replace(`/login?returnUrl=/minha-conta/pedidos/${id}`);
    }
  }, [hydrated, user, router, id]);

  if (!hydrated || !user) {
    return (
      <div className="px-4 py-16 text-center text-esotera-muted">Carregando…</div>
    );
  }

  if (!order || (order.userId !== user.id && user.role !== "admin")) {
    return (
      <div className="mx-auto max-w-6xl px-4 py-10 sm:px-6">
        <EmptyState
          title="Pedido não encontrado"
          action={<ButtonLink href="/minha-conta">Voltar</ButtonLink>}
        />
      </div>
    );
  }

  const address = order.shipping.address;

  return (
    <div className="mx-auto max-w-3xl px-4 py-10 sm:px-6">
      <ButtonLink href="/minha-conta" variant="ghost" className="mb-4 px-0">
        ← Voltar
      </ButtonLink>
      <h1 className="font-serif text-3xl text-esotera-white">Pedido {order.id}</h1>
      <p className="mt-2 flex flex-wrap items-center gap-2 text-sm text-esotera-muted">
        {formatDate(order.createdAt)} <StatusBadge status={order.status} />
      </p>

      <section className="mt-8 space-y-3">
        {order.items.map((item) => (
          <div
            key={`${item.productId}-${item.variation ?? ""}`}
            className="flex gap-3 rounded-lg border border-esotera-graphite p-3"
          >
            <div className="relative h-16 w-12 overflow-hidden rounded">
              <ProductImage src={item.image} alt={item.name} fill className="object-cover" sizes="48px" />
            </div>
            <div className="text-sm">
              <p className="text-esotera-beige">{item.name}</p>
              <p className="text-esotera-muted">
                {item.quantity} × <Price value={item.price} />
              </p>
            </div>
          </div>
        ))}
      </section>

      <section className="mt-6 grid gap-4 text-sm text-esotera-muted sm:grid-cols-2">
        <div className="rounded-lg border border-esotera-graphite p-4">
          <h2 className="text-esotera-beige">Status da entrega</h2>
          <p className="mt-2">
            <StatusBadge status={order.status} />
          </p>
          <p className="mt-2">{order.shipping.methodName}</p>
          <p>{order.shipping.estimatedDays}</p>
        </div>
        <div className="rounded-lg border border-esotera-graphite p-4">
          <h2 className="text-esotera-beige">Pagamento</h2>
          <p className="mt-2">{paymentMethodLabels[order.payment.method]}</p>
          <p className="mt-1 text-xs">{order.payment.status}</p>
        </div>
      </section>

      <section className="mt-4 rounded-lg border border-esotera-graphite p-4 text-sm text-esotera-muted">
        <h2 className="text-esotera-beige">Endereço</h2>
        <p className="mt-2">
          {address.street}, {address.number}
          <br />
          {address.city}/{address.state} · CEP {address.cep}
        </p>
      </section>

      <dl className="mt-4 space-y-2 text-sm">
        <div className="flex justify-between">
          <dt className="text-esotera-muted">Subtotal</dt>
          <dd>
            <Price value={order.subtotal} />
          </dd>
        </div>
        <div className="flex justify-between">
          <dt className="text-esotera-muted">Desconto</dt>
          <dd>
            <Price value={order.discount} />
          </dd>
        </div>
        <div className="flex justify-between">
          <dt className="text-esotera-muted">Frete</dt>
          <dd>
            <Price value={order.shippingPrice} />
          </dd>
        </div>
        <div className="flex justify-between text-base">
          <dt className="text-esotera-beige">Total</dt>
          <dd>
            <Price value={order.total} />
          </dd>
        </div>
      </dl>
    </div>
  );
}
