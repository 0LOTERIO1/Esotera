"use client";

import Image from "next/image";
import Link from "next/link";
import { useState } from "react";
import type { Product } from "@/types";
import { Price } from "@/components/ui/Price";
import { Button } from "@/components/ui/Button";
import { useCartStore } from "@/stores/cartStore";
import { useToastStore } from "@/stores/toastStore";

type ProductCardProps = {
  product: Product;
};

export function ProductCard({ product }: ProductCardProps) {
  const addItem = useCartStore((s) => s.addItem);
  const push = useToastStore((s) => s.push);
  const [imgError, setImgError] = useState(false);
  const src = product.images[0];
  const isDataUrl = src?.startsWith("data:");

  return (
    <article className="group flex h-full flex-col overflow-hidden rounded-lg border border-esotera-border bg-esotera-surface shadow-sm transition hover:border-esotera-primary/35 hover:shadow">
      <Link
        href={`/produtos/${product.slug}`}
        className="relative aspect-square overflow-hidden bg-esotera-surface-secondary"
      >
        {!imgError && src ? (
          isDataUrl ? (
            // eslint-disable-next-line @next/next/no-img-element
            <img
              src={src}
              alt={product.name}
              className="absolute inset-0 h-full w-full object-cover transition duration-300 group-hover:scale-[1.02]"
              onError={() => setImgError(true)}
            />
          ) : (
            <Image
              src={src}
              alt={product.name}
              fill
              className="object-cover transition duration-300 group-hover:scale-[1.02]"
              sizes="(max-width: 640px) 50vw, (max-width: 1024px) 33vw, 20vw"
              onError={() => setImgError(true)}
            />
          )
        ) : (
          <div className="absolute inset-0 flex items-center justify-center px-2 text-center text-[11px] text-esotera-muted">
            Imagem indisponível
          </div>
        )}
        {!product.isAvailable ? (
          <span className="absolute left-1.5 top-1.5 rounded bg-esotera-secondary/90 px-1.5 py-0.5 text-[10px] text-white">
            Indisponível
          </span>
        ) : null}
      </Link>
      <div className="flex flex-1 flex-col gap-1 p-2 sm:p-2.5">
        <Link
          href={`/produtos/${product.slug}`}
          className="line-clamp-2 text-[13px] font-medium leading-snug text-esotera-text hover:text-esotera-primary sm:text-sm"
        >
          {product.name}
        </Link>
        <Price value={product.price} className="mt-0.5 text-[0.95rem] sm:text-base" />
        <Button
          type="button"
          disabled={!product.isAvailable}
          className="mt-1 w-full !min-h-9 px-2 py-1.5 text-xs sm:!min-h-10 sm:text-sm"
          onClick={() => {
            addItem(product.id, 1);
            push("success", "Produto adicionado ao carrinho.");
          }}
        >
          {product.isAvailable ? "Comprar" : "Indisponível"}
        </Button>
      </div>
    </article>
  );
}
