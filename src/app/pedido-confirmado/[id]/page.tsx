"use client";

import { ProductImage } from "@/components/ui/ProductImage";
import { use } from "react";
import { useOrdersStore } from "@/stores/ordersStore";
import { ButtonLink } from "@/components/ui/Button";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { Price } from "@/components/ui/Price";
import { EmptyState } from "@/components/ui/EmptyState";
import { paymentMethodLabels } from "@/utils/labels";
import { formatDate } from "@/utils/format";
import { storeConfig } from "@/config/store";

export default function OrderConfirmedPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = use(params);
  const order = useOrdersStore((s) => s.getById(id));

  if (!order) {
    return (
      <div className="mx-auto max-w-6xl px-4 py-10 sm:px-6">
        <EmptyState
          title="Pedido não encontrado"
          description="Verifique o número ou consulte Minha conta."
          action={<ButtonLink href="/minha-conta">Minha conta</ButtonLink>}
        />
      </div>
    );
  }

  const address = order.shipping.address;

  return (
    <div className="mx-auto max-w-3xl px-4 py-10 sm:px-6">
      <p className="text-sm text-esotera-gold-soft">{storeConfig.demoNotice}</p>
      <h1 className="mt-2 font-serif text-4xl text-esotera-white">
        Pedido confirmado
      </h1>
      <p className="mt-2 text-sm text-esotera-muted">
        Número: <span className="text-esotera-beige">{order.id}</span>
      </p>
      <p className="mt-1 text-sm text-esotera-muted">
        {formatDate(order.createdAt)} · <StatusBadge status={order.status} />
      </p>

      <section className="mt-8 space-y-4 rounded-lg border border-esotera-graphite p-5">
        <h2 className="font-serif text-xl text-esotera-beige">Produtos</h2>
        {order.items.map((item) => (
          <div key={`${item.productId}-${item.variation ?? ""}`} className="flex gap-3">
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

      <section className="mt-4 grid gap-4 sm:grid-cols-2">
        <div className="rounded-lg border border-esotera-graphite p-5 text-sm text-esotera-muted">
          <h2 className="font-serif text-lg text-esotera-beige">Pagamento</h2>
          <p className="mt-2">{paymentMethodLabels[order.payment.method]}</p>
          {order.payment.installments ? (
            <p>{order.payment.installments}x sem juros (simulado)</p>
          ) : null}
          <p className="mt-1 text-xs">{order.payment.status}</p>
        </div>
        <div className="rounded-lg border border-esotera-graphite p-5 text-sm text-esotera-muted">
          <h2 className="font-serif text-lg text-esotera-beige">Entrega</h2>
          <p className="mt-2">{order.shipping.methodName}</p>
          <p>{order.shipping.estimatedDays}</p>
          <p className="mt-2">
            {address.street}, {address.number}
            {address.complement ? ` — ${address.complement}` : ""}
          </p>
          <p>
            {address.neighborhood} · {address.city}/{address.state}
          </p>
          <p>CEP {address.cep}</p>
        </div>
      </section>

      <dl className="mt-4 space-y-2 rounded-lg border border-esotera-graphite p-5 text-sm">
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
            {order.shippingPrice === 0 ? (
              <span className="text-esotera-success">Grátis</span>
            ) : (
              <Price value={order.shippingPrice} />
            )}
          </dd>
        </div>
        <div className="flex justify-between border-t border-esotera-graphite pt-2 text-base">
          <dt className="text-esotera-beige">Total</dt>
          <dd>
            <Price value={order.total} className="text-lg" />
          </dd>
        </div>
      </dl>

      <div className="mt-8 flex flex-wrap gap-3">
        <ButtonLink href={`/minha-conta/pedidos/${order.id}`}>
          Acompanhar pedido
        </ButtonLink>
        <ButtonLink href="/produtos" variant="secondary">
          Continuar comprando
        </ButtonLink>
      </div>
    </div>
  );
}
