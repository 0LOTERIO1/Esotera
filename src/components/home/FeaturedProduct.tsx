"use client";

import Link from "next/link";
import { useMemo } from "react";
import { useProductsStore } from "@/stores/productsStore";
import { Price } from "@/components/ui/Price";
import { ButtonLink } from "@/components/ui/Button";
import { ProductImage } from "@/components/ui/ProductImage";

export function FeaturedProduct() {
  const products = useProductsStore((s) => s.products);
  const product = useMemo(
    () =>
      products.find((p) => p.isFeatured && !p.isDemo) ??
      products.find((p) => p.isFeatured),
    [products],
  );

  if (!product) return null;

  return (
    <section className="mx-auto max-w-6xl px-4 py-16 sm:px-6">
      <div className="grid items-center gap-10 md:grid-cols-2">
        <div className="relative aspect-[4/5] overflow-hidden rounded-lg border border-esotera-graphite bg-esotera-black/40">
          <ProductImage
            src={product.images[0]}
            alt={product.name}
            fill
            className="object-cover"
            sizes="(max-width: 768px) 100vw, 50vw"
            priority
          />
        </div>
        <div>
          <p className="text-sm uppercase tracking-[0.22em] text-esotera-gold">
            Destaque
          </p>
          <h2 className="mt-4 font-serif text-3xl leading-tight text-esotera-white sm:text-4xl md:text-[2.75rem]">
            {product.name}
          </h2>
          <p className="mt-5 text-base leading-relaxed text-esotera-muted">
            {product.shortDescription}
          </p>
          <p className="mt-6">
            <Price value={product.price} className="text-2xl" />
          </p>
          <div className="mt-8 flex flex-wrap gap-3">
            <ButtonLink href={`/produtos/${product.slug}`}>
              Ver produto
            </ButtonLink>
            <Link
              href="/produtos"
              className="inline-flex min-h-11 items-center text-sm text-esotera-beige hover:text-esotera-gold"
            >
              Ver catálogo
            </Link>
          </div>
        </div>
      </div>
    </section>
  );
}
