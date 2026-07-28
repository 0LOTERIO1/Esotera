import { useOrdersStore } from "@/stores/ordersStore";
import { useProductsStore } from "@/stores/productsStore";
import { mockAuthService } from "@/services/auth/mockAuthService";
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
import type { Order, OrderStatus, Product } from "@/types";

/**
 * Painel admin demonstrativo (localStorage). Não mistura com pedidos da API.
 */
export class MockAdminRepository implements IAdminRepository {
  async getDashboard(): Promise<AdminDashboard> {
    const orders = useOrdersStore.getState().orders.filter(
      (o) => o.status !== "cancelled",
    );
    const all = useOrdersStore.getState().orders;
    const products = useProductsStore.getState().products;
    const count = (status: OrderStatus) =>
      all.filter((o) => o.status === status).length;

    return {
      totalOrders: all.length,
      totalSales: orders.reduce((s, o) => s + o.total, 0),
      awaitingPayment: count("awaiting_payment"),
      paymentApproved: count("payment_approved"),
      preparing: count("preparing"),
      shipped: count("shipped"),
      delivered: count("delivered"),
      cancelled: count("cancelled"),
      availableProducts: products.filter((p) => p.isAvailable).length,
      customersWithOrders: new Set(all.map((o) => o.userId)).size,
      recentOrders: [...all]
        .sort(
          (a, b) =>
            new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime(),
        )
        .slice(0, 8)
        .map((o) => ({
          id: o.id,
          orderNumber: o.orderNumber ?? o.id,
          status: o.status,
          total: o.total,
          customerName: o.upSellerExport?.customerName ?? "Cliente",
          createdAt: o.createdAt,
        })),
      topProducts: await this.listSoldProducts(),
    };
  }

  async listOrders(params: AdminOrderListParams = {}): Promise<AdminOrdersPage> {
    let orders = [...useOrdersStore.getState().orders];
    if (params.status) {
      orders = orders.filter((o) => o.status === params.status);
    }
    if (params.search?.trim()) {
      const q = params.search.trim().toLowerCase();
      orders = orders.filter(
        (o) =>
          o.id.toLowerCase().includes(q) ||
          (o.orderNumber ?? "").toLowerCase().includes(q) ||
          (o.upSellerExport?.customerName ?? "").toLowerCase().includes(q),
      );
    }
    orders.sort(
      (a, b) =>
        new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime(),
    );
    const page = params.page ?? 1;
    const pageSize = params.pageSize ?? 20;
    const start = (page - 1) * pageSize;
    const slice = orders.slice(start, start + pageSize);
    return {
      items: slice.map((o) => ({
        id: o.id,
        orderNumber: o.orderNumber ?? o.id,
        status: o.status,
        total: o.total,
        itemCount: o.items.reduce((s, i) => s + i.quantity, 0),
        customerName: o.upSellerExport?.customerName ?? "Cliente",
        paymentMethod: o.payment.method,
        shippingMethodName: o.shipping.methodName,
        createdAt: o.createdAt,
        rowVersion: 0,
      })),
      totalCount: orders.length,
      page,
      pageSize,
      totalPages: Math.max(1, Math.ceil(orders.length / pageSize)),
    };
  }

  async getOrder(id: string): Promise<AdminOrderDetail | null> {
    const order = useOrdersStore.getState().orders.find((o) => o.id === id);
    if (!order) return null;
    return mapOrderToAdminDetail(order);
  }

  async updateOrderStatus(
    id: string,
    status: OrderStatus,
    expectedVersion: number,
    note?: string,
  ): Promise<AdminOrderDetail> {
    void expectedVersion;
    void note;
    await useOrdersStore.getState().updateStatus(id, status);
    const detail = await this.getOrder(id);
    if (!detail) throw new Error("Pedido não encontrado.");
    return detail;
  }

  async listCustomers(): Promise<AdminCustomer[]> {
    const orders = useOrdersStore.getState().orders.filter(
      (o) => o.status !== "cancelled",
    );
    const users = mockAuthService.listUsers().filter((u) => u.role === "customer");
    return users.map((user) => {
      const userOrders = orders.filter((o) => o.userId === user.id);
      return {
        id: user.id,
        name: user.name,
        email: user.email,
        phone: user.phone,
        orderCount: userOrders.length,
        totalSpent: userOrders.reduce((s, o) => s + o.total, 0),
        lastOrderAt: userOrders[0]?.createdAt ?? null,
      };
    });
  }

  async listSoldProducts(): Promise<AdminSoldProduct[]> {
    const orders = useOrdersStore
      .getState()
      .orders.filter((o) => o.status !== "cancelled");
    const map = new Map<string, AdminSoldProduct>();
    for (const order of orders) {
      for (const item of order.items) {
        const key = `${item.productId}|${item.name}|${item.image}`;
        const current = map.get(key);
        if (current) {
          current.quantitySold += item.quantity;
          current.totalRevenue += item.price * item.quantity;
          current.orderCount += 1;
        } else {
          map.set(key, {
            productId: item.productId,
            productName: item.name,
            imageUrl: item.image,
            image: normalizeProductImageUrl(item.image),
            quantitySold: item.quantity,
            totalRevenue: item.price * item.quantity,
            orderCount: 1,
          });
        }
      }
    }
    return [...map.values()].sort((a, b) => b.quantitySold - a.quantitySold);
  }

  async listProducts(): Promise<Product[]> {
    return useProductsStore.getState().products;
  }

  async setProductAvailability(id: string, isAvailable: boolean): Promise<void> {
    await useProductsStore.getState().setAvailability(id, isAvailable);
  }
}

function mapOrderToAdminDetail(order: Order): AdminOrderDetail {
  return {
    id: order.id,
    orderNumber: order.orderNumber ?? order.id,
    status: order.status,
    subtotal: order.subtotal,
    discount: order.discount,
    shippingPrice: order.shippingPrice,
    total: order.total,
    couponCode: order.couponCode,
    shipping: {
      methodId: order.shipping.methodId,
      methodName: order.shipping.methodName,
      provider: order.shipping.provider,
      estimatedDays: order.shipping.estimatedDays,
    },
    payment: {
      method: order.payment.method,
      installments: order.payment.installments,
      status: order.payment.status,
    },
    customer: {
      name: order.upSellerExport?.customerName ?? "",
      email: order.upSellerExport?.customerEmail ?? "",
      phone: order.upSellerExport?.customerPhone,
    },
    address: order.shipping.address,
    items: order.items.map((item, index) => ({
      id: `${order.id}-${index}`,
      productId: item.productId,
      name: item.name,
      price: item.price,
      quantity: item.quantity,
      variation: item.variation,
      image: normalizeProductImageUrl(item.image),
      lineTotal: item.price * item.quantity,
    })),
    statusHistory: [
      {
        toStatus: order.status,
        createdAt: order.updatedAt,
      },
    ],
    createdAt: order.createdAt,
    updatedAt: order.updatedAt,
    rowVersion: 0,
  };
}
