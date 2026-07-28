"use client";

import { ProductImage } from "@/components/ui/ProductImage";
import Link from "next/link";
import { useState } from "react";
import { useCartStore } from "@/stores/cartStore";
import { useAuthStore } from "@/stores/authStore";
import { useCartTotals } from "@/hooks/useCartTotals";
import { QuantitySelector } from "@/components/ui/QuantitySelector";
import { Price } from "@/components/ui/Price";
import { Button, ButtonLink } from "@/components/ui/Button";
import { EmptyState } from "@/components/ui/EmptyState";
import { storeConfig } from "@/config/store";
import { CouponForm } from "@/components/cart/CouponForm";
import { OrderSummary } from "@/components/cart/OrderSummary";
import { ConfirmModal } from "@/components/ui/ConfirmModal";

export default function CartPage() {
  const updateQuantity = useCartStore((s) => s.updateQuantity);
  const removeItem = useCartStore((s) => s.removeItem);
  const clearCart = useCartStore((s) => s.clearCart);
  const user = useAuthStore((s) => s.user);
  const { lines } = useCartTotals();
  const [confirmClear, setConfirmClear] = useState(false);
  const checkoutHref = user
    ? "/checkout"
    : "/login?returnUrl=/checkout";

  if (!lines.length) {
    return (
      <div className="mx-auto max-w-6xl px-4 py-10 sm:px-6">
        <h1 className="font-serif text-4xl text-esotera-secondary">Carrinho</h1>
        <div className="mt-8">
          <EmptyState
            title="Seu carrinho está vazio"
            description="Adicione produtos do catálogo para continuar."
            action={<ButtonLink href="/produtos">Ver produtos</ButtonLink>}
          />
        </div>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-6xl px-4 py-10 sm:px-6">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <h1 className="font-serif text-4xl text-esotera-secondary">Carrinho</h1>
        <Button type="button" variant="danger" onClick={() => setConfirmClear(true)}>
          Esvaziar carrinho
        </Button>
      </div>

      <div className="mt-8 grid gap-8 lg:grid-cols-[1fr_320px]">
        <div className="space-y-4">
          {lines.map((line) => (
            <article
              key={`${line.productId}-${line.variation ?? ""}`}
              className="flex flex-col gap-4 rounded-lg border border-esotera-border p-4 sm:flex-row"
            >
              <Link
                href={`/produtos/${line.product.slug}`}
                className="relative h-28 w-full shrink-0 overflow-hidden rounded-md sm:w-24"
              >
                <ProductImage
                  src={line.product.images[0]}
                  alt={line.product.name}
                  fill
                  className="object-cover"
                  sizes="96px"
                />
              </Link>
              <div className="flex flex-1 flex-col gap-3">
                <div className="flex flex-wrap items-start justify-between gap-2">
                  <div>
                    <Link
                      href={`/produtos/${line.product.slug}`}
                      className="font-serif text-lg text-esotera-text hover:text-esotera-primary"
                    >
                      {line.product.name}
                    </Link>
                    {line.variation ? (
                      <p className="text-xs text-esotera-muted">
                        Variação: {line.variation}
                      </p>
                    ) : null}
                    <p className="mt-1">
                      <Price value={line.unitPrice} />
                    </p>
                  </div>
                  <button
                    type="button"
                    onClick={() => removeItem(line.productId, line.variation)}
                    className="text-sm text-esotera-muted hover:text-esotera-error"
                  >
                    Remover
                  </button>
                </div>
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <QuantitySelector
                    value={line.quantity}
                    onChange={(qty) =>
                      updateQuantity(line.productId, qty, line.variation)
                    }
                  />
                  <Price value={line.lineTotal} />
                </div>
              </div>
            </article>
          ))}
          <CouponForm />
        </div>

        <div className="space-y-4 lg:sticky lg:top-24 lg:self-start">
          <OrderSummary />
          <p className="text-xs text-esotera-muted">{storeConfig.includedCardNotice}</p>
          <ButtonLink href={checkoutHref} className="w-full">
            Ir para o checkout
          </ButtonLink>
          <ButtonLink href="/produtos" variant="secondary" className="w-full">
            Continuar comprando
          </ButtonLink>
        </div>
      </div>

      <ConfirmModal
        open={confirmClear}
        title="Esvaziar carrinho?"
        description="Todos os itens e o cupom aplicado serão removidos."
        confirmLabel="Esvaziar"
        onCancel={() => setConfirmClear(false)}
        onConfirm={() => {
          clearCart();
          setConfirmClear(false);
        }}
      />
    </div>
  );
}
