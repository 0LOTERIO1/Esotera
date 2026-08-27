"use client";

import Link from "next/link";
import { useMemo } from "react";
import { useProductsStore } from "@/stores/productsStore";
import { Price } from "@/components/ui/Price";
import { ButtonLink } from "@/components/ui/Button";
import { ProductImage } from "@/components/ui/ProductImage";
import { useStoreHydration } from "@/hooks/useStoreHydration";

export function FeaturedProduct() {
  const hydrated = useStoreHydration();
  const products = useProductsStore((s) => s.products);
  const error = useProductsStore((s) => s.error);
  const product = useMemo(
    () =>
      products.find((p) => p.isFeatured && !p.isDemo) ??
      products.find((p) => p.isFeatured),
    [products],
  );

  if (!hydrated) return null;
  if (error || !product) return null;

  return (
    <section className="mx-auto max-w-6xl px-4 py-8 sm:px-6 sm:py-10">
      <div className="grid items-center gap-6 rounded-xl border border-esotera-border bg-esotera-surface p-4 shadow-sm sm:gap-8 sm:p-6 md:grid-cols-2">
        <div className="relative aspect-square overflow-hidden rounded-lg bg-esotera-surface-secondary">
          <ProductImage
            src={product.images[0]}
            alt={product.name}
            fill
            objectFit="contain"
            className="p-3"
            sizes="(max-width: 768px) 100vw, 50vw"
            priority
          />
        </div>
        <div>
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-esotera-primary">
            Destaque
          </p>
          <h2 className="mt-2 font-serif text-2xl leading-snug text-esotera-secondary sm:text-3xl">
            {product.name}
          </h2>
          <p className="mt-3 text-sm text-esotera-muted sm:text-base">
            {product.shortDescription}
          </p>
          <p className="mt-4">
            <Price value={product.price} className="text-2xl" />
          </p>
          <div className="mt-6 flex flex-wrap gap-3">
            <ButtonLink href={`/produtos/${product.slug}`}>
              Ver produto
            </ButtonLink>
            <Link
              href="/produtos"
              className="inline-flex min-h-11 items-center text-sm font-medium text-esotera-secondary hover:text-esotera-primary"
            >
              Ver catálogo
            </Link>
          </div>
        </div>
      </div>
    </section>
  );
}
