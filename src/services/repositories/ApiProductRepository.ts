import { productsApi, mapProductDetail, toProductUserMessage } from "@/services/api/productsApi";
import { adminApi } from "@/services/api/adminApi";
import type {
  IProductRepository,
  ProductInput,
  ProductListFilters,
} from "./IProductRepository";
import type { Product, ProductImageMeta } from "@/types";

function slugify(name: string): string {
  return name
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .slice(0, 180);
}

function toVariationDtos(variations?: string[]) {
  if (!variations?.length) return null;
  return [{ type: "Opções", options: variations }];
}

function mapImage(api: {
  id: string;
  secureUrl: string;
  publicId?: string | null;
  altText?: string | null;
  sortOrder: number;
  isPrimary: boolean;
  createdAtUtc?: string;
}): ProductImageMeta {
  return {
    id: api.id,
    secureUrl: api.secureUrl,
    publicId: api.publicId,
    altText: api.altText,
    sortOrder: api.sortOrder,
    isPrimary: api.isPrimary,
    createdAt: api.createdAtUtc,
  };
}

/**
 * Catálogo e admin de produtos via API real (sem localStorage).
 */
export class ApiProductRepository implements IProductRepository {
  async getCatalog(): Promise<Product[]> {
    try {
      return await productsApi.list();
    } catch (error) {
      throw new Error(toProductUserMessage(error));
    }
  }

  async getBySlug(slug: string): Promise<Product | undefined> {
    try {
      const product = await productsApi.getBySlug(slug);
      return product ?? undefined;
    } catch (error) {
      throw new Error(toProductUserMessage(error));
    }
  }

  async getById(id: string): Promise<Product | undefined> {
    try {
      const product = await productsApi.getById(id);
      return product ?? undefined;
    } catch (error) {
      throw new Error(toProductUserMessage(error));
    }
  }

  async listAdmin(filters: ProductListFilters = {}): Promise<Product[]> {
    try {
      const items = await adminApi.listProducts({
        search: filters.search,
        categoryId: filters.categoryId,
        isAvailable:
          filters.isAvailable === "all" || filters.isAvailable === undefined
            ? undefined
            : filters.isAvailable,
        isArchived:
          filters.archived === "archived"
            ? true
            : filters.archived === "active"
              ? false
              : undefined,
        archived: filters.archived === "all" ? "all" : undefined,
      });
      const { mapProductListItem } = await import("@/services/api/productsApi");
      return items.map(mapProductListItem);
    } catch (error) {
      throw new Error(toProductUserMessage(error));
    }
  }

  async getAdminDetail(id: string): Promise<Product | undefined> {
    try {
      const raw = await adminApi.getProduct(id);
      return mapProductDetail(raw);
    } catch (error) {
      throw new Error(toProductUserMessage(error));
    }
  }

  async upsert(product: ProductInput, imageFile?: File): Promise<Product> {
    try {
      const categoryId = product.categoryId;
      if (!categoryId) {
        throw new Error("Selecione uma categoria válida.");
      }

      const slug = product.slug?.trim() || slugify(product.name);
      let saved: Product;

      if (product.id) {
        const raw = await adminApi.updateProduct(product.id, {
          name: product.name,
          slug,
          shortDescription: product.shortDescription,
          description: product.description,
          price: product.price,
          categoryId,
          features: product.features,
          packageContents: product.packageContents ?? [],
          variations: toVariationDtos(product.variations),
          isFeatured: product.isFeatured,
          isAvailable: product.isAvailable,
          expectedVersion: product.rowVersion,
        });
        saved = mapProductDetail(raw);
      } else {
        const raw = await adminApi.createProduct({
          name: product.name,
          slug,
          shortDescription: product.shortDescription,
          description: product.description,
          price: product.price,
          categoryId,
          features: product.features,
          packageContents: product.packageContents ?? [],
          variations: toVariationDtos(product.variations),
          isFeatured: product.isFeatured,
          isAvailable: product.isAvailable,
        });
        saved = mapProductDetail(raw);
      }

      if (imageFile) {
        await adminApi.uploadProductImage(saved.id, imageFile, {
          isPrimary: true,
        });
        const refreshed = await this.getAdminDetail(saved.id);
        if (refreshed) return refreshed;
      }

      return saved;
    } catch (error) {
      throw new Error(toProductUserMessage(error));
    }
  }

  async setAvailability(
    id: string,
    isAvailable: boolean,
  ): Promise<Product | undefined> {
    try {
      await adminApi.setProductAvailability(id, isAvailable);
      return (await this.getAdminDetail(id)) ?? undefined;
    } catch (error) {
      throw new Error(toProductUserMessage(error));
    }
  }

  async setFeatured(id: string, isFeatured: boolean): Promise<void> {
    try {
      await adminApi.setProductFeatured(id, isFeatured);
    } catch (error) {
      throw new Error(toProductUserMessage(error));
    }
  }

  async archive(id: string): Promise<Product> {
    try {
      return mapProductDetail(await adminApi.archiveProduct(id));
    } catch (error) {
      throw new Error(toProductUserMessage(error));
    }
  }

  async restore(id: string): Promise<Product> {
    try {
      return mapProductDetail(await adminApi.restoreProduct(id));
    } catch (error) {
      throw new Error(toProductUserMessage(error));
    }
  }

  async uploadImage(
    productId: string,
    file: File,
    options?: { isPrimary?: boolean; altText?: string },
  ): Promise<ProductImageMeta> {
    try {
      return mapImage(
        await adminApi.uploadProductImage(productId, file, options),
      );
    } catch (error) {
      throw new Error(toProductUserMessage(error));
    }
  }

  async updateImage(
    productId: string,
    imageId: string,
    body: { altText?: string; isPrimary?: boolean },
  ): Promise<ProductImageMeta> {
    try {
      return mapImage(await adminApi.updateProductImage(productId, imageId, body));
    } catch (error) {
      throw new Error(toProductUserMessage(error));
    }
  }

  async deleteImage(productId: string, imageId: string): Promise<void> {
    try {
      await adminApi.deleteProductImage(productId, imageId);
    } catch (error) {
      throw new Error(toProductUserMessage(error));
    }
  }

  async reorderImages(
    productId: string,
    imageIds: string[],
  ): Promise<ProductImageMeta[]> {
    try {
      const list = await adminApi.reorderProductImages(productId, imageIds);
      return list.map(mapImage);
    } catch (error) {
      throw new Error(toProductUserMessage(error));
    }
  }
}
