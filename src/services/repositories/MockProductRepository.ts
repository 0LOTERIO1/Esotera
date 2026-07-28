import { productRepository } from "@/services/products/productRepository";
import type { IProductRepository, ProductInput } from "./IProductRepository";
import type { Product } from "@/types";

export class MockProductRepository implements IProductRepository {
  async getCatalog(): Promise<Product[]> {
    return productRepository.getCatalog();
  }

  async getBySlug(slug: string): Promise<Product | undefined> {
    return productRepository.getBySlug(slug);
  }

  async getById(id: string): Promise<Product | undefined> {
    return productRepository.getById(id);
  }

  async listAdmin(): Promise<Product[]> {
    return productRepository.getCatalog();
  }

  async getAdminDetail(id: string): Promise<Product | undefined> {
    return productRepository.getById(id);
  }

  async upsert(product: ProductInput, imageFile?: File): Promise<Product> {
    void imageFile;
    return productRepository.upsert(product);
  }

  async setAvailability(id: string, isAvailable: boolean): Promise<Product | undefined> {
    return productRepository.setAvailability(id, isAvailable);
  }

  async archive(id: string): Promise<Product> {
    const updated = await productRepository.setAvailability(id, false);
    if (!updated) throw new Error("Produto não encontrado.");
    return { ...updated, isArchived: true, isAvailable: false };
  }

  async restore(id: string): Promise<Product> {
    const product = await productRepository.getById(id);
    if (!product) throw new Error("Produto não encontrado.");
    return { ...product, isArchived: false, isAvailable: false };
  }
}
