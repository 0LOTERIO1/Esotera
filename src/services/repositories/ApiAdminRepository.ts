import { adminApi } from "@/services/api/adminApi";
import { ApiError } from "@/services/api/apiClient";
import { mapProductListItem } from "@/services/api/productsApi";
import { normalizeProductImageUrl } from "@/utils/productImage";
import type { IAdminRepository } from "./IAdminRepository";
import type { AdminOrderListParams } from "@/services/api/adminApi";
import type {
  AdminCustomer,
  AdminDashboard,
  AdminOrderDetail,
  AdminOrdersPage,
  AdminSoldProduct,
} from "@/services/api/adminTypes";
import type { OrderStatus, Product } from "@/types";

function rethrow(error: unknown): never {
  if (error instanceof ApiError) throw error;
  if (error instanceof Error) throw error;
  throw new Error("Falha na operação administrativa.");
}

export class ApiAdminRepository implements IAdminRepository {
  async getDashboard(): Promise<AdminDashboard> {
    try {
      return await adminApi.getDashboard();
    } catch (error) {
      rethrow(error);
    }
  }

  async listOrders(params?: AdminOrderListParams): Promise<AdminOrdersPage> {
    try {
      return await adminApi.listOrders(params);
    } catch (error) {
      rethrow(error);
    }
  }

  async getOrder(id: string): Promise<AdminOrderDetail | null> {
    try {
      return await adminApi.getOrder(id);
    } catch (error) {
      rethrow(error);
    }
  }

  async updateOrderStatus(
    id: string,
    status: OrderStatus,
    expectedVersion: number,
    note?: string,
  ): Promise<AdminOrderDetail> {
    try {
      return await adminApi.updateOrderStatus(id, status, expectedVersion, note);
    } catch (error) {
      rethrow(error);
    }
  }

  async listCustomers(): Promise<AdminCustomer[]> {
    try {
      return await adminApi.listCustomers();
    } catch (error) {
      rethrow(error);
    }
  }

  async listSoldProducts(): Promise<AdminSoldProduct[]> {
    try {
      return await adminApi.listSoldProducts();
    } catch (error) {
      rethrow(error);
    }
  }

  async listProducts(): Promise<Product[]> {
    try {
      const items = await adminApi.listProducts();
      return items.map((item) =>
        mapProductListItem({
          ...item,
          primaryImage: item.primaryImage
            ? normalizeProductImageUrl(item.primaryImage)
            : item.primaryImage,
        }),
      );
    } catch (error) {
      rethrow(error);
    }
  }

  async setProductAvailability(id: string, isAvailable: boolean): Promise<void> {
    try {
      await adminApi.setProductAvailability(id, isAvailable);
    } catch (error) {
      rethrow(error);
    }
  }
}
