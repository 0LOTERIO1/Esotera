import type {
  AdminCustomer,
  AdminDashboard,
  AdminOrderDetail,
  AdminOrdersPage,
  AdminSoldProduct,
} from "@/services/api/adminTypes";
import type { AdminOrderListParams } from "@/services/api/adminApi";
import type { OrderStatus, Product } from "@/types";

export interface IAdminRepository {
  getDashboard(): Promise<AdminDashboard>;
  listOrders(params?: AdminOrderListParams): Promise<AdminOrdersPage>;
  getOrder(id: string): Promise<AdminOrderDetail | null>;
  updateOrderStatus(
    id: string,
    status: OrderStatus,
    expectedVersion: number,
    note?: string,
  ): Promise<AdminOrderDetail>;
  listCustomers(): Promise<AdminCustomer[]>;
  listSoldProducts(): Promise<AdminSoldProduct[]>;
  listProducts(): Promise<Product[]>;
  setProductAvailability(
    id: string,
    isAvailable: boolean,
  ): Promise<void>;
}
