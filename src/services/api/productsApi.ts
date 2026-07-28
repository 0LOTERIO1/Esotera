import { apiClient, ApiError } from "./apiClient";
import type { Product, ProductImageMeta, ProductVariation } from "@/types";
import {
  normalizeProductImageUrl,
  PRODUCT_IMAGE_PLACEHOLDER,
} from "@/utils/productImage";
import { withCloudinaryTransform } from "@/utils/cloudinaryImage";

/** Resposta de GET /api/products */
export type ApiProductListItem = {
  id: string;
  slug: string;
  name: string;
  shortDescription?: string | null;
  price: number;
  category: string;
  categoryId?: string;
  primaryImage?: string | null;
  isFeatured: boolean;
  isAvailable: boolean;
  isArchived?: boolean;
  updatedAtUtc?: string;
};

export type ApiProductImage = {
  id: string;
  secureUrl: string;
  publicId?: string | null;
  altText?: string | null;
  sortOrder: number;
  isPrimary: boolean;
  createdAtUtc?: string;
};

/** Resposta de GET /api/products/{slug} e admin detail */
export type ApiProductDetail = {
  id: string;
  slug: string;
  name: string;
  shortDescription?: string | null;
  description?: string | null;
  price: number;
  category: string;
  categoryId?: string;
  images: Array<string | ApiProductImage>;
  features?: string[] | null;
  packageContents?: string[] | null;
  variations?: Array<
    | { type: string; options: string[] }
    | {
        id: string;
        name: string;
        price: number;
        isAvailable?: boolean;
        sku?: string | null;
        imageUrl?: string | null;
      }
  > | null;
  isFeatured: boolean;
  isAvailable: boolean;
  isArchived?: boolean;
  archivedAtUtc?: string | null;
  isDemo?: boolean;
  rowVersion?: number;
  createdAtUtc?: string;
  updatedAtUtc?: string;
};

export type ApiCategory = {
  id: string;
  name: string;
  slug: string;
};

function mapVariations(
  variations: ApiProductDetail["variations"],
  fallbackPrice: number,
): ProductVariation[] | undefined {
  if (!variations?.length) return undefined;
  const mapped: ProductVariation[] = [];
  for (const v of variations) {
    if ("name" in v && typeof v.name === "string") {
      mapped.push({
        id: v.id || v.name,
        name: v.name,
        price: typeof v.price === "number" && v.price > 0 ? v.price : fallbackPrice,
        isAvailable: v.isAvailable !== false,
        sku: v.sku,
        imageUrl: v.imageUrl,
      });
      continue;
    }
    if ("options" in v && Array.isArray(v.options)) {
      for (const opt of v.options) {
        mapped.push({
          id: opt,
          name: opt,
          price: fallbackPrice,
          isAvailable: true,
        });
      }
    }
  }
  return mapped.length ? mapped : undefined;
}

function mapImageMeta(img: ApiProductImage): ProductImageMeta {
  return {
    id: img.id,
    secureUrl: normalizeProductImageUrl(img.secureUrl),
    publicId: img.publicId,
    altText: img.altText,
    sortOrder: img.sortOrder,
    isPrimary: img.isPrimary,
    createdAt: img.createdAtUtc,
  };
}

function extractImageUrls(
  images: Array<string | ApiProductImage> | null | undefined,
): { urls: string[]; metas?: ProductImageMeta[] } {
  if (!images?.length) {
    return { urls: [PRODUCT_IMAGE_PLACEHOLDER] };
  }

  if (typeof images[0] === "string") {
    const urls = (images as string[])
      .map((url) => normalizeProductImageUrl(url))
      .filter(Boolean);
    return { urls: urls.length ? urls : [PRODUCT_IMAGE_PLACEHOLDER] };
  }

  const metas = (images as ApiProductImage[])
    .slice()
    .sort((a, b) => Number(b.isPrimary) - Number(a.isPrimary) || a.sortOrder - b.sortOrder)
    .map(mapImageMeta);
  const urls = metas.map((m) =>
    withCloudinaryTransform(m.secureUrl, m.isPrimary ? "detail" : "card"),
  );
  return {
    urls: urls.length ? urls : [PRODUCT_IMAGE_PLACEHOLDER],
    metas,
  };
}

export function mapProductListItem(item: ApiProductListItem): Product {
  const primary = item.primaryImage
    ? withCloudinaryTransform(normalizeProductImageUrl(item.primaryImage), "card")
    : PRODUCT_IMAGE_PLACEHOLDER;
  return {
    id: item.id,
    slug: item.slug,
    name: item.name,
    shortDescription: item.shortDescription ?? "",
    description: item.shortDescription ?? "",
    price: item.price,
    category: item.category,
    categoryId: item.categoryId,
    images: [primary],
    features: [],
    isFeatured: item.isFeatured,
    isAvailable: item.isAvailable,
    isArchived: item.isArchived ?? false,
    updatedAt: item.updatedAtUtc,
  };
}

export function mapProductDetail(api: ApiProductDetail): Product {
  const { urls, metas } = extractImageUrls(api.images);
  return {
    id: api.id,
    slug: api.slug,
    name: api.name,
    shortDescription: api.shortDescription ?? "",
    description: api.description ?? "",
    price: api.price,
    category: api.category,
    categoryId: api.categoryId,
    images: urls,
    productImages: metas,
    features: api.features ?? [],
    packageContents: api.packageContents ?? undefined,
    variations: mapVariations(api.variations, api.price),
    isFeatured: api.isFeatured,
    isAvailable: api.isAvailable,
    isArchived: api.isArchived ?? false,
    archivedAt: api.archivedAtUtc,
    isDemo: api.isDemo,
    rowVersion: api.rowVersion,
    createdAt: api.createdAtUtc,
    updatedAt: api.updatedAtUtc,
  };
}

export const productsApi = {
  async list(): Promise<Product[]> {
    const response = await apiClient.get<ApiProductListItem[]>("/api/products", {
      auth: false,
    });
    return response.map(mapProductListItem);
  },

  async getBySlug(slug: string): Promise<Product | null> {
    try {
      const response = await apiClient.get<ApiProductDetail>(
        `/api/products/${encodeURIComponent(slug)}`,
        { auth: false },
      );
      return mapProductDetail(response);
    } catch (error: unknown) {
      if (error instanceof ApiError && error.status === 404) return null;
      throw error;
    }
  },

  async getById(id: string): Promise<Product | null> {
    try {
      const response = await apiClient.get<ApiProductDetail>(
        `/api/products/id/${id}`,
        { auth: false },
      );
      return mapProductDetail(response);
    } catch (error: unknown) {
      if (error instanceof ApiError && error.status === 404) return null;
      throw error;
    }
  },

  async listCategories(): Promise<ApiCategory[]> {
    return apiClient.get<ApiCategory[]>("/api/categories", { auth: false });
  },
};

export function toProductUserMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 409) {
      return "Conflito: o produto foi alterado por outra operação ou o slug já existe. Atualize e tente novamente.";
    }
    return error.userMessage;
  }
  if (error instanceof Error) return error.message;
  return "Não foi possível carregar os produtos.";
}
