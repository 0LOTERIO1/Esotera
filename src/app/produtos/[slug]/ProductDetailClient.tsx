"use client";

import { ProductImage } from "@/components/ui/ProductImage";
import { use, useEffect, useMemo, useState } from "react";
import { notFound } from "next/navigation";
import { useProductsStore } from "@/stores/productsStore";
import { useCartStore } from "@/stores/cartStore";
import { useToastStore } from "@/stores/toastStore";
import { Price } from "@/components/ui/Price";
import { QuantitySelector } from "@/components/ui/QuantitySelector";
import { Button } from "@/components/ui/Button";
import { ProductCard } from "@/components/products/ProductCard";
import { ProductGrid } from "@/components/products/ProductGrid";
import { storeConfig } from "@/config/store";
import { findVariation } from "@/utils/productPricing";
import { FormField, inputClassName } from "@/components/ui/FormField";
import { LoadingState } from "@/components/ui/LoadingState";
import { EmptyState } from "@/components/ui/EmptyState";
import { useStoreHydration } from "@/hooks/useStoreHydration";
import type { Product } from "@/types";

export function ProductDetailClient({
  params,
}: {
  params: Promise<{ slug: string }>;
}) {
  const { slug } = use(params);
  const hydrated = useStoreHydration();
  const products = useProductsStore((s) => s.products);
  const fetchBySlug = useProductsStore((s) => s.fetchBySlug);
  const addItem = useCartStore((s) => s.addItem);
  const push = useToastStore((s) => s.push);

  const [product, setProduct] = useState<Product | null | undefined>(undefined);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [quantity, setQuantity] = useState(1);
  const [variation, setVariation] = useState("");
  const [activeImage, setActiveImage] = useState(0);

  useEffect(() => {
    if (!hydrated) return;
    let cancelled = false;

    void (async () => {
      setLoadError(null);
      try {
        const full = await fetchBySlug(slug);
        if (cancelled) return;
        setProduct(full);
      } catch (error) {
        if (cancelled) return;
        setLoadError(
          error instanceof Error
            ? error.message
            : "Não foi possível carregar o produto.",
        );
        const fallback = useProductsStore.getState().getBySlug(slug);
        setProduct(fallback ?? null);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [hydrated, slug, fetchBySlug]);

  const related = useMemo(() => {
    if (!product) return [];
    return products
      .filter((p) => p.category === product.category && p.id !== product.id)
      .slice(0, 10);
  }, [product, products]);

  if (!hydrated || product === undefined) {
    return (
      <div className="mx-auto max-w-6xl px-4 py-10 sm:px-6">
        <LoadingState label="Carregando produto…" />
      </div>
    );
  }

  if (loadError && !product) {
    return (
      <div className="mx-auto max-w-6xl px-4 py-10 sm:px-6">
        <EmptyState
          title="Produto indisponível"
          description={loadError}
          action={
            <Button type="button" onClick={() => window.location.reload()}>
              Tentar novamente
            </Button>
          }
        />
      </div>
    );
  }

  if (!product) {
    notFound();
  }

  return (
    <div className="mx-auto max-w-6xl px-4 py-8 sm:px-6 sm:py-10">
      {loadError ? (
        <p role="status" className="mb-4 text-sm text-esotera-muted">
          {loadError}
        </p>
      ) : null}
      <div className="grid gap-8 lg:grid-cols-2 lg:gap-10">
        <div>
          <div className="relative aspect-square overflow-hidden rounded-lg border border-esotera-border bg-esotera-surface-secondary">
            <ProductImage
              src={product.images[activeImage] ?? product.images[0]}
              alt={product.name}
              fill
              objectFit="contain"
              className="p-2"
              sizes="(max-width: 1024px) 100vw, 50vw"
              priority
            />
          </div>
          {product.images.length > 1 ? (
            <div className="mt-3 flex gap-2 overflow-x-auto">
              {product.images.map((src, index) => (
                <button
                  key={`${src}-${index}`}
                  type="button"
                  onClick={() => setActiveImage(index)}
                  className={`relative h-16 w-16 shrink-0 overflow-hidden rounded border ${
                    activeImage === index
                      ? "border-esotera-primary"
                      : "border-esotera-border"
                  }`}
                  aria-label={`Ver imagem ${index + 1}`}
                >
                  <ProductImage
                    src={src}
                    alt=""
                    fill
                    className="object-cover"
                    sizes="64px"
                  />
                </button>
              ))}
            </div>
          ) : null}
        </div>

        <div>
          <p className="text-xs uppercase tracking-wide text-esotera-muted">
            {product.category}
          </p>
          <h1 className="mt-2 font-serif text-3xl text-esotera-secondary sm:text-4xl">
            {product.name}
          </h1>
          <p className="mt-4">
            <Price
              value={
                findVariation(product, variation)?.price ?? product.price
              }
              className="text-2xl"
            />
          </p>
          {!product.isAvailable ? (
            <p role="status" className="mt-4 text-sm text-esotera-error">
              Produto indisponível
            </p>
          ) : null}
          <p className="mt-6 text-sm leading-relaxed text-esotera-muted">
            {product.description}
          </p>

          {product.features.length ? (
            <div className="mt-6">
              <h2 className="font-serif text-xl text-esotera-secondary">
                Características
              </h2>
              <ul className="mt-2 list-disc space-y-1 pl-5 text-sm text-esotera-muted">
                {product.features.map((f) => (
                  <li key={f}>{f}</li>
                ))}
              </ul>
            </div>
          ) : null}

          {product.packageContents?.length ? (
            <div className="mt-6">
              <h2 className="font-serif text-xl text-esotera-secondary">
                Conteúdo da embalagem
              </h2>
              <ul className="mt-2 list-disc space-y-1 pl-5 text-sm text-esotera-muted">
                {product.packageContents.map((f) => (
                  <li key={f}>{f}</li>
                ))}
              </ul>
            </div>
          ) : null}

          {product.variations?.length ? (
            <div className="mt-6">
              <FormField label="Variação" id="variation" required>
                <select
                  id="variation"
                  className={inputClassName}
                  value={variation}
                  onChange={(e) => {
                    const next = e.target.value;
                    setVariation(next);
                    const v = findVariation(product, next);
                    if (v?.imageUrl) {
                      const idx = product.images.findIndex((img) => img === v.imageUrl);
                      if (idx >= 0) setActiveImage(idx);
                    }
                  }}
                  disabled={!product.isAvailable}
                >
                  <option value="">Selecione</option>
                  {product.variations
                    .filter((v) => v.isAvailable)
                    .map((v) => (
                      <option key={v.id} value={v.name}>
                        {v.name} — R${" "}
                        {v.price.toFixed(2).replace(".", ",")}
                      </option>
                    ))}
                </select>
              </FormField>
            </div>
          ) : null}

          <div className="mt-6 flex flex-wrap items-center gap-4">
            <QuantitySelector
              value={quantity}
              onChange={setQuantity}
              disabled={!product.isAvailable}
            />
            <Button
              type="button"
              disabled={
                !product.isAvailable ||
                (Boolean(product.variations?.some((v) => v.isAvailable)) &&
                  !variation)
              }
              onClick={() => {
                if (
                  product.variations?.some((v) => v.isAvailable) &&
                  !variation
                ) {
                  push("error", "Selecione uma variação.");
                  return;
                }
                addItem(product.id, quantity, variation || undefined);
                push("success", "Produto adicionado ao carrinho.");
              }}
            >
              Adicionar ao carrinho
            </Button>
          </div>

          <p className="mt-4 text-xs text-esotera-muted">
            {storeConfig.includedCardNotice}
          </p>

          <div className="mt-8 rounded-md border border-esotera-border bg-esotera-surface p-4 text-sm text-esotera-muted">
            <p className="font-medium text-esotera-secondary">
              Informações de entrega
            </p>
            <p className="mt-2">
              Enviamos para todo o Brasil. O prazo e o valor do frete são
              calculados no checkout de acordo com o CEP informado. Em regiões
              elegíveis de São Paulo, também pode haver opção de entrega no
              mesmo dia.
            </p>
          </div>
        </div>
      </div>

      {related.length ? (
        <section className="mt-12 sm:mt-16">
          <h2 className="font-serif text-2xl text-esotera-secondary">
            Produtos relacionados
          </h2>
          <div className="mt-5">
            <ProductGrid>
              {related.map((p) => (
                <ProductCard key={p.id} product={p} />
              ))}
            </ProductGrid>
          </div>
        </section>
      ) : null}
    </div>
  );
}
