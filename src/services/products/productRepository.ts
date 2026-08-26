import { initialProducts } from "@/data/products";
import { STORAGE_KEYS, safeParseJSON } from "@/utils/storage";
import { generateId } from "@/utils/format";
import { isQuotaExceededError } from "@/utils/imageStorage";
import type { Product } from "@/types";

export type ProductInput = Omit<Product, "id" | "slug" | "createdAt"> & {
  id?: string;
  slug?: string;
  createdAt?: string;
};

type ProductStorage = {
  /** Produtos criados pelo admin */
  custom: Product[];
  /** Substituições de produtos iniciais (por id) */
  overrides: Record<string, Product>;
};

const EMPTY: ProductStorage = { custom: [], overrides: {} };

function slugify(name: string): string {
  return name
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/(^-|-$)/g, "")
    .slice(0, 80);
}

function readStorage(): ProductStorage {
  if (typeof window === "undefined") return EMPTY;
  return safeParseJSON<ProductStorage>(
    localStorage.getItem(STORAGE_KEYS.products),
    EMPTY,
  );
}

function writeStorage(data: ProductStorage) {
  try {
    localStorage.setItem(STORAGE_KEYS.products, JSON.stringify(data));
  } catch (error) {
    if (isQuotaExceededError(error)) {
      throw new Error(
        "Espaço de armazenamento do navegador insuficiente. Remova produtos antigos ou use uma imagem menor.",
      );
    }
    throw error;
  }
}

/**
 * Repositório de produtos do protótipo.
 * Mistura seed estático + cadastros/overrides locais.
 * TODO: substituir por API .NET + PostgreSQL / storage em nuvem.
 */
export const productRepository = {
  getCatalog(): Product[] {
    const { custom, overrides } = readStorage();
    const seeded = initialProducts.map((p) => overrides[p.id] ?? p);
    const seedIds = new Set(initialProducts.map((p) => p.id));
    const extras = custom.filter((p) => !seedIds.has(p.id));
    return [...seeded, ...extras];
  },

  getBySlug(slug: string): Product | undefined {
    return this.getCatalog().find((p) => p.slug === slug);
  },

  getById(id: string): Product | undefined {
    return this.getCatalog().find((p) => p.id === id);
  },

  upsert(input: ProductInput): Product {
    const storage = readStorage();
    const now = new Date().toISOString();
    const id = input.id ?? generateId("prod");
    const slug =
      input.slug?.trim() ||
      `${slugify(input.name)}-${id.slice(-4)}`;

    const product: Product = {
      id,
      slug,
      name: input.name.trim(),
      sku: input.sku?.trim() || null,
      shortDescription: input.shortDescription.trim(),
      description: input.description.trim(),
      price: input.price,
      category: input.category.trim(),
      images: input.images,
      features: input.features ?? [],
      packageContents: input.packageContents,
      variations: input.variations,
      isFeatured: input.isFeatured,
      isAvailable: input.isAvailable,
      isDemo: input.isDemo,
      createdAt: input.createdAt ?? now,
      updatedAt: now,
    };

    const isSeed = initialProducts.some((p) => p.id === id);
    if (isSeed) {
      storage.overrides[id] = product;
    } else {
      const idx = storage.custom.findIndex((p) => p.id === id);
      if (idx >= 0) storage.custom[idx] = product;
      else storage.custom.push(product);
    }

    writeStorage(storage);
    return product;
  },

  setAvailability(id: string, isAvailable: boolean): Product | undefined {
    const current = this.getById(id);
    if (!current) return undefined;
    return this.upsert({ ...current, isAvailable });
  },
};
