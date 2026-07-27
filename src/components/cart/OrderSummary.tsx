"use client";

import { Price } from "@/components/ui/Price";
import { useCartTotals } from "@/hooks/useCartTotals";

type OrderSummaryProps = {
  shippingPrice?: number;
  showShipping?: boolean;
  className?: string;
};

export function OrderSummary({
  shippingPrice = 0,
  showShipping = false,
  className = "",
}: OrderSummaryProps) {
  const { subtotal, discount, productsTotal } = useCartTotals();
  const total = productsTotal + (showShipping ? shippingPrice : 0);

  return (
    <aside
      className={`rounded-lg border border-esotera-graphite bg-esotera-black/40 p-5 ${className}`}
      aria-label="Resumo do pedido"
    >
      <h2 className="font-serif text-xl text-esotera-beige">Resumo</h2>
      <dl className="mt-4 space-y-3 text-sm">
        <div className="flex justify-between gap-4">
          <dt className="text-esotera-muted">Subtotal</dt>
          <dd>
            <Price value={subtotal} />
          </dd>
        </div>
        <div className="flex justify-between gap-4">
          <dt className="text-esotera-muted">Desconto</dt>
          <dd>
            <Price value={discount} />
          </dd>
        </div>
        <div className="flex justify-between gap-4">
          <dt className="text-esotera-muted">Produtos</dt>
          <dd>
            <Price value={productsTotal} />
          </dd>
        </div>
        {showShipping ? (
          <div className="flex justify-between gap-4">
            <dt className="text-esotera-muted">Frete</dt>
            <dd>
              {shippingPrice === 0 ? (
                <span className="text-esotera-success">Grátis</span>
              ) : (
                <Price value={shippingPrice} />
              )}
            </dd>
          </div>
        ) : null}
        <div className="flex justify-between gap-4 border-t border-esotera-graphite pt-3 text-base">
          <dt className="text-esotera-beige">Total</dt>
          <dd>
            <Price value={total} className="text-lg" />
          </dd>
        </div>
      </dl>
    </aside>
  );
}
