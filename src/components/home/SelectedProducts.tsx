"use client";

import { useMemo } from "react";
import { useProductsStore } from "@/stores/productsStore";
import { ProductCard } from "@/components/products/ProductCard";
import { ProductGrid } from "@/components/products/ProductGrid";
import { Button, ButtonLink } from "@/components/ui/Button";
import { LoadingState } from "@/components/ui/LoadingState";
import { EmptyState } from "@/components/ui/EmptyState";
import { useStoreHydration } from "@/hooks/useStoreHydration";

export function SelectedProducts() {
  const hydrated = useStoreHydration();
  const products = useProductsStore((s) => s.products);
  const loading = useProductsStore((s) => s.loading);
  const error = useProductsStore((s) => s.error);
  const refresh = useProductsStore((s) => s.refresh);
  const selected = useMemo(
    () => products.filter((p) => p.isAvailable).slice(0, 10),
    [products],
  );

  return (
    <section className="border-y border-esotera-border bg-esotera-surface-secondary py-8 sm:py-10">
      <div className="mx-auto max-w-6xl px-4 sm:px-6">
        <div className="mb-5 flex flex-wrap items-end justify-between gap-3">
          <div>
            <h2 className="font-serif text-2xl text-esotera-secondary sm:text-3xl">
              Produtos
            </h2>
            <p className="mt-1 text-sm text-esotera-muted">
              Mais opções para você escolher.
            </p>
          </div>
          <ButtonLink href="/produtos" variant="secondary">
            Ver todos
          </ButtonLink>
        </div>

        {!hydrated || loading ? (
          <LoadingState label="Carregando produtos…" />
        ) : error ? (
          <EmptyState
            title="Catálogo indisponível"
            description={error}
            action={
              <Button type="button" onClick={() => void refresh()}>
                Tentar novamente
              </Button>
            }
          />
        ) : selected.length === 0 ? (
          <EmptyState
            title="Nenhum produto disponível"
            description="Em breve novos itens no catálogo."
          />
        ) : (
          <ProductGrid>
            {selected.map((product) => (
              <ProductCard key={product.id} product={product} />
            ))}
          </ProductGrid>
        )}
      </div>
    </section>
  );
}
