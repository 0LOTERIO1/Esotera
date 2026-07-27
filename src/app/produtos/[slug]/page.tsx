"use client";

import { ProductImage } from "@/components/ui/ProductImage";
import { use, useMemo, useState } from "react";
import { notFound } from "next/navigation";
import { useProductsStore } from "@/stores/productsStore";
import { useCartStore } from "@/stores/cartStore";
import { useToastStore } from "@/stores/toastStore";
import { Price } from "@/components/ui/Price";
import { QuantitySelector } from "@/components/ui/QuantitySelector";
import { Button } from "@/components/ui/Button";
import { ProductCard } from "@/components/products/ProductCard";
import { shippingOrigin } from "@/config/shipping";
import { FormField, inputClassName } from "@/components/ui/FormField";

export default function ProductDetailPage({
  params,
}: {
  params: Promise<{ slug: string }>;
}) {
  const { slug } = use(params);
  const product = useProductsStore((s) => s.getBySlug(slug));
  const products = useProductsStore((s) => s.products);
  const addItem = useCartStore((s) => s.addItem);
  const push = useToastStore((s) => s.push);
  const [quantity, setQuantity] = useState(1);
  const [variation, setVariation] = useState("");
  const [activeImage, setActiveImage] = useState(0);

  const related = useMemo(() => {
    if (!product) return [];
    return products
      .filter((p) => p.category === product.category && p.id !== product.id)
      .slice(0, 3);
  }, [product, products]);

  if (!product) {
    notFound();
  }

  return (
    <div className="mx-auto max-w-6xl px-4 py-10 sm:px-6">
      <div className="grid gap-10 lg:grid-cols-2">
        <div>
          <div className="relative aspect-[4/5] overflow-hidden rounded-lg border border-esotera-graphite bg-esotera-black/40">
            <ProductImage
              src={product.images[activeImage] ?? product.images[0]}
              alt={product.name}
              fill
              className="object-cover"
              sizes="(max-width: 1024px) 100vw, 50vw"
              priority
            />
          </div>
          {product.images.length > 1 ? (
            <div className="mt-3 flex gap-2">
              {product.images.map((src, index) => (
                <button
                  key={src}
                  type="button"
                  onClick={() => setActiveImage(index)}
                  className={`relative h-20 w-16 overflow-hidden rounded border ${
                    activeImage === index
                      ? "border-esotera-gold"
                      : "border-esotera-graphite"
                  }`}
                  aria-label={`Ver imagem ${index + 1}`}
                >
                  <ProductImage src={src} alt="" fill className="object-cover" sizes="64px" />
                </button>
              ))}
            </div>
          ) : null}
        </div>

        <div>
          <p className="text-xs uppercase tracking-wide text-esotera-muted">
            {product.category}
          </p>
          <h1 className="mt-2 font-serif text-3xl text-esotera-white sm:text-4xl">
            {product.name}
          </h1>
          <p className="mt-4">
            <Price value={product.price} className="text-2xl" />
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
              <h2 className="font-serif text-xl text-esotera-beige">
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
              <h2 className="font-serif text-xl text-esotera-beige">
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
              <FormField label="Variação" id="variation">
                <select
                  id="variation"
                  className={inputClassName}
                  value={variation}
                  onChange={(e) => setVariation(e.target.value)}
                  disabled={!product.isAvailable}
                >
                  <option value="">Selecione</option>
                  {product.variations.map((v) => (
                    <option key={v} value={v}>
                      {v}
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
              disabled={!product.isAvailable}
              onClick={() => {
                addItem(product.id, quantity, variation || undefined);
                push("success", "Produto adicionado ao carrinho.");
              }}
            >
              Adicionar ao carrinho
            </Button>
          </div>

          <div className="mt-8 rounded-md border border-esotera-graphite p-4 text-sm text-esotera-muted">
            <p className="font-medium text-esotera-beige">Informações de entrega</p>
            <p className="mt-2">
              Envio a partir de {shippingOrigin.city} ({shippingOrigin.cep}).
              Frete calculado no checkout. Modalidade J3 simulada para CEPs
              elegíveis em São Paulo.
            </p>
          </div>
        </div>
      </div>

      {related.length ? (
        <section className="mt-16">
          <h2 className="font-serif text-2xl text-esotera-white">
            Produtos relacionados
          </h2>
          <div className="mt-6 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
            {related.map((p) => (
              <ProductCard key={p.id} product={p} />
            ))}
          </div>
        </section>
      ) : null}
    </div>
  );
}
