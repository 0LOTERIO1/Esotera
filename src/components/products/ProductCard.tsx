"use client";

import Link from "next/link";
import type { Product } from "@/types";
import { Price } from "@/components/ui/Price";
import { Button } from "@/components/ui/Button";
import { ProductImage } from "@/components/ui/ProductImage";
import { useCartStore } from "@/stores/cartStore";
import { useToastStore } from "@/stores/toastStore";

type ProductCardProps = {
  product: Product;
};

export function ProductCard({ product }: ProductCardProps) {
  const addItem = useCartStore((s) => s.addItem);
  const push = useToastStore((s) => s.push);

  return (
    <article className="group flex flex-col overflow-hidden rounded-lg border border-esotera-graphite/80 bg-esotera-black/30 transition hover:border-esotera-gold/40">
      <Link
        href={`/produtos/${product.slug}`}
        className="relative aspect-[4/5] overflow-hidden bg-esotera-navy"
      >
        <ProductImage
          src={product.images[0]}
          alt={product.name}
          fill
          className="object-cover transition duration-500 group-hover:scale-[1.03]"
          sizes="(max-width: 768px) 50vw, 25vw"
        />
        {!product.isAvailable ? (
          <span className="absolute left-3 top-3 rounded bg-esotera-black/80 px-2 py-1 text-xs text-esotera-beige">
            Indisponível
          </span>
        ) : null}
        {product.isDemo ? (
          <span className="absolute right-3 top-3 rounded border border-esotera-gold/40 bg-esotera-black/80 px-2 py-1 text-[10px] text-esotera-gold">
            Demo
          </span>
        ) : null}
      </Link>
      <div className="flex flex-1 flex-col gap-2 p-4">
        <p className="text-xs uppercase tracking-[0.16em] text-esotera-muted">
          {product.category}
        </p>
        <Link
          href={`/produtos/${product.slug}`}
          className="line-clamp-2 font-serif text-xl leading-snug text-esotera-beige hover:text-esotera-gold"
        >
          {product.name}
        </Link>
        <Price value={product.price} className="mt-auto text-base" />
        <Button
          type="button"
          disabled={!product.isAvailable}
          className="mt-2 w-full"
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
