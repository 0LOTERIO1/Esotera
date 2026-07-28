import { apiClient } from "./apiClient";
import {
  mapAdminCustomer,
  mapAdminDashboard,
  mapAdminOrderDetail,
  mapAdminOrdersPage,
  mapSoldProduct,
} from "./adminMapper";
import type {
  AdminCustomer,
  AdminDashboard,
  AdminOrderDetail,
  AdminOrdersPage,
  AdminSoldProduct,
} from "./adminTypes";
import type { OrderStatus } from "@/types";

export type AdminOrderListParams = {
  status?: string;
  search?: string;
  page?: number;
  pageSize?: number;
};

export const adminApi = {
  async getDashboard(): Promise<AdminDashboard> {
    const data = await apiClient.get<Parameters<typeof mapAdminDashboard>[0]>(
      "/api/admin/dashboard",
      { auth: true },
    );
    return mapAdminDashboard(data);
  },

  async listOrders(params: AdminOrderListParams = {}): Promise<AdminOrdersPage> {
    const query = new URLSearchParams();
    if (params.status) query.set("status", params.status);
    if (params.search) query.set("search", params.search);
    query.set("page", String(params.page ?? 1));
    query.set("pageSize", String(params.pageSize ?? 20));
    const data = await apiClient.get<{
      items: Parameters<typeof mapAdminOrdersPage>[0]["items"];
      totalCount: number;
      page: number;
      pageSize: number;
      totalPages?: number;
    }>(`/api/admin/orders?${query.toString()}`, { auth: true });
    return mapAdminOrdersPage(data);
  },

  async getOrder(id: string): Promise<AdminOrderDetail | null> {
    try {
      const data = await apiClient.get<Parameters<typeof mapAdminOrderDetail>[0]>(
        `/api/admin/orders/${id}`,
        { auth: true },
      );
      return mapAdminOrderDetail(data);
    } catch (error: unknown) {
      if (
        error &&
        typeof error === "object" &&
        "status" in error &&
        error.status === 404
      ) {
        return null;
      }
      throw error;
    }
  },

  async updateOrderStatus(
    id: string,
    status: OrderStatus,
    expectedVersion: number,
    note?: string,
  ): Promise<AdminOrderDetail> {
    await apiClient.patch(
      `/api/admin/orders/${id}/status`,
      {
        status,
        note: note ?? null,
        expectedVersion,
      },
      { auth: true },
    );
    const detail = await this.getOrder(id);
    if (!detail) {
      throw new Error("Pedido atualizado, mas não foi possível recarregar os detalhes.");
    }
    return detail;
  },

  async listCustomers(): Promise<AdminCustomer[]> {
    const data = await apiClient.get<Parameters<typeof mapAdminCustomer>[0][]>(
      "/api/admin/customers",
      { auth: true },
    );
    return data.map(mapAdminCustomer);
  },

  async listSoldProducts(): Promise<AdminSoldProduct[]> {
    const data = await apiClient.get<Parameters<typeof mapSoldProduct>[0][]>(
      "/api/admin/sales/products",
      { auth: true },
    );
    return data.map(mapSoldProduct);
  },

  async setProductAvailability(
    id: string,
    isAvailable: boolean,
  ): Promise<void> {
    await apiClient.patch(
      `/api/admin/products/${id}/availability`,
      { isAvailable },
      { auth: true },
    );
  },

  async listProducts(params: {
    search?: string;
    categoryId?: string;
    isAvailable?: boolean;
    isArchived?: boolean;
    archived?: "all";
  } = {}): Promise<
    {
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
    }[]
  > {
    const query = new URLSearchParams();
    if (params.search) query.set("search", params.search);
    if (params.categoryId) query.set("categoryId", params.categoryId);
    if (params.isAvailable !== undefined)
      query.set("isAvailable", String(params.isAvailable));
    if (params.isArchived !== undefined)
      query.set("isArchived", String(params.isArchived));
    if (params.archived) query.set("archived", params.archived);
    const qs = query.toString();
    return apiClient.get(`/api/admin/products${qs ? `?${qs}` : ""}`, {
      auth: true,
    });
  },

  async getProduct(id: string) {
    return apiClient.get<import("./productsApi").ApiProductDetail>(
      `/api/admin/products/${id}`,
      { auth: true },
    );
  },

  async createProduct(body: Record<string, unknown>) {
    return apiClient.post<import("./productsApi").ApiProductDetail>(
      "/api/admin/products",
      body,
      { auth: true },
    );
  },

  async updateProduct(id: string, body: Record<string, unknown>) {
    return apiClient.put<import("./productsApi").ApiProductDetail>(
      `/api/admin/products/${id}`,
      body,
      { auth: true },
    );
  },

  async archiveProduct(id: string) {
    return apiClient.patch<import("./productsApi").ApiProductDetail>(
      `/api/admin/products/${id}/archive`,
      undefined,
      { auth: true },
    );
  },

  async restoreProduct(id: string) {
    return apiClient.patch<import("./productsApi").ApiProductDetail>(
      `/api/admin/products/${id}/restore`,
      undefined,
      { auth: true },
    );
  },

  async setProductFeatured(id: string, isFeatured: boolean) {
    await apiClient.patch(
      `/api/admin/products/${id}/featured`,
      { isFeatured },
      { auth: true },
    );
  },

  async uploadProductImage(
    productId: string,
    file: File,
    options: { isPrimary?: boolean; altText?: string } = {},
  ) {
    const form = new FormData();
    form.append("file", file);
    if (options.altText) form.append("altText", options.altText);
    const q = options.isPrimary ? "?isPrimary=true" : "";
    return apiClient.postFormData<import("./productsApi").ApiProductImage>(
      `/api/admin/products/${productId}/images${q}`,
      form,
      { auth: true },
    );
  },

  async updateProductImage(
    productId: string,
    imageId: string,
    body: { altText?: string; isPrimary?: boolean },
  ) {
    return apiClient.patch<import("./productsApi").ApiProductImage>(
      `/api/admin/products/${productId}/images/${imageId}`,
      body,
      { auth: true },
    );
  },

  async deleteProductImage(productId: string, imageId: string) {
    await apiClient.delete(
      `/api/admin/products/${productId}/images/${imageId}`,
      { auth: true },
    );
  },

  async reorderProductImages(productId: string, imageIds: string[]) {
    return apiClient.put<import("./productsApi").ApiProductImage[]>(
      `/api/admin/products/${productId}/images/order`,
      { imageIds },
      { auth: true },
    );
  },
};
