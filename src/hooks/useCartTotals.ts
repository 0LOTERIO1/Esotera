"use client";

import { useMemo } from "react";
import { useCartStore } from "@/stores/cartStore";
import { useProductsStore } from "@/stores/productsStore";

export function useCartTotals() {
  const items = useCartStore((s) => s.items);
  const coupon = useCartStore((s) => s.coupon);
  const products = useProductsStore((s) => s.products);

  return useMemo(() => {
    const lines = items
      .map((item) => {
        const product = products.find((p) => p.id === item.productId);
        if (!product) return null;
        return {
          ...item,
          product,
          lineTotal: product.price * item.quantity,
        };
      })
      .filter(Boolean) as Array<{
      productId: string;
      quantity: number;
      variation?: string;
      product: (typeof products)[number];
      lineTotal: number;
    }>;

    const subtotal = lines.reduce((sum, l) => sum + l.lineTotal, 0);
    const discount = coupon
      ? Math.min(coupon.discountAmount, subtotal)
      : 0;
    const productsTotal = subtotal - discount;

    return { lines, subtotal, discount, productsTotal, coupon };
  }, [items, coupon, products]);
}
