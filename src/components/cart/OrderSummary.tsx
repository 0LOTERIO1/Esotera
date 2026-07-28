"use client";

import { Price } from "@/components/ui/Price";
import { useCartTotals } from "@/hooks/useCartTotals";

type OrderSummaryProps = {
  /** omit = não mostra frete; pending = "A calcular"; selected = valor/grátis */
  shippingMode?: "omit" | "pending" | "selected";
  shippingPrice?: number;
  className?: string;
};

export function OrderSummary({
  shippingMode = "omit",
  shippingPrice = 0,
  className = "",
}: OrderSummaryProps) {
  const { subtotal, discount, productsTotal } = useCartTotals();
  const shippingInTotal =
    shippingMode === "selected" ? Math.max(0, shippingPrice) : 0;
  const total = Math.max(0, productsTotal + shippingInTotal);

  return (
    <aside
      className={`w-full rounded-lg border border-esotera-border bg-esotera-surface p-5 shadow-sm ${className}`}
      aria-label="Resumo do pedido"
    >
      <h2 className="font-serif text-xl text-esotera-secondary">Resumo</h2>
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
        {shippingMode !== "omit" ? (
          <div className="flex justify-between gap-4">
            <dt className="text-esotera-muted">Frete</dt>
            <dd>
              {shippingMode === "pending" ? (
                <span className="text-esotera-muted">A calcular</span>
              ) : shippingPrice === 0 ? (
                <span className="text-esotera-success">Grátis</span>
              ) : (
                <Price value={shippingPrice} />
              )}
            </dd>
          </div>
        ) : null}
        <div className="flex justify-between gap-4 border-t border-esotera-border pt-3 text-base">
          <dt className="text-esotera-text">Total</dt>
          <dd>
            <Price value={total} className="text-lg" />
          </dd>
        </div>
      </dl>
    </aside>
  );
}
