"use client";

import { useMemo } from "react";
import { useProductsStore } from "@/stores/productsStore";
import { ProductCard } from "@/components/products/ProductCard";
import { ButtonLink } from "@/components/ui/Button";

export function SelectedProducts() {
  const products = useProductsStore((s) => s.products);
  const selected = useMemo(
    () => products.filter((p) => p.isAvailable).slice(0, 4),
    [products],
  );

  return (
    <section className="border-t border-esotera-graphite/60 bg-esotera-black/20 py-16">
      <div className="mx-auto max-w-6xl px-4 sm:px-6">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div>
            <h2 className="font-serif text-3xl text-esotera-white">
              Selecionados
            </h2>
            <p className="mt-2 text-sm text-esotera-muted">
              Uma curadoria inicial para o protótipo da loja.
            </p>
          </div>
          <ButtonLink href="/produtos" variant="secondary">
            Ver todos
          </ButtonLink>
        </div>
        <div className="mt-10 grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
          {selected.map((product) => (
            <ProductCard key={product.id} product={product} />
          ))}
        </div>
      </div>
    </section>
  );
}
