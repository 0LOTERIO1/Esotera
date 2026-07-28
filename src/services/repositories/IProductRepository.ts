import type { Product, ProductImageMeta, ProductVariation } from "@/types";

export type ProductInput = {
  id?: string;
  slug?: string;
  name: string;
  shortDescription: string;
  description: string;
  price: number;
  category: string;
  categoryId?: string;
  images: string[];
  features: string[];
  packageContents?: string[];
  variations?: ProductVariation[];
  isFeatured: boolean;
  isAvailable: boolean;
  isDemo?: boolean;
  rowVersion?: number;
  createdAt?: string;
  updatedAt?: string;
};

export type ProductListFilters = {
  search?: string;
  categoryId?: string;
  isAvailable?: boolean | "all";
  archived?: "active" | "archived" | "all";
};

export interface IProductRepository {
  getCatalog(): Promise<Product[]>;
  getBySlug(slug: string): Promise<Product | undefined>;
  getById(id: string): Promise<Product | undefined>;
  /** Lista administrativa (inclui indisponíveis; arquivados conforme filtro). */
  listAdmin?(filters?: ProductListFilters): Promise<Product[]>;
  getAdminDetail?(id: string): Promise<Product | undefined>;
  upsert(product: ProductInput, imageFile?: File): Promise<Product>;
  setAvailability(id: string, isAvailable: boolean): Promise<Product | undefined>;
  setFeatured?(id: string, isFeatured: boolean): Promise<void>;
  archive?(id: string): Promise<Product>;
  restore?(id: string): Promise<Product>;
  uploadImage?(
    productId: string,
    file: File,
    options?: { isPrimary?: boolean; altText?: string },
  ): Promise<ProductImageMeta>;
  updateImage?(
    productId: string,
    imageId: string,
    body: { altText?: string; isPrimary?: boolean },
  ): Promise<ProductImageMeta>;
  deleteImage?(productId: string, imageId: string): Promise<void>;
  reorderImages?(productId: string, imageIds: string[]): Promise<ProductImageMeta[]>;
}
