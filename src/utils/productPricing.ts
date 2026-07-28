import type { Product, ProductVariation } from "@/types";

/** Resolve preço unitário considerando variação (nome ou id). */
export function resolveUnitPrice(
  product: Product,
  variation?: string | null,
): number {
  if (!variation || !product.variations?.length) return product.price;
  const found = product.variations.find(
    (v) =>
      v.id === variation ||
      v.name.toLowerCase() === variation.toLowerCase(),
  );
  return found && found.price > 0 ? found.price : product.price;
}

export function findVariation(
  product: Product,
  variation?: string | null,
): ProductVariation | undefined {
  if (!variation || !product.variations?.length) return undefined;
  return product.variations.find(
    (v) =>
      v.id === variation ||
      v.name.toLowerCase() === variation.toLowerCase(),
  );
}
