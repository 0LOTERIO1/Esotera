import { generateId } from "@/utils/format";
import { mockCouponService } from "@/services/coupon/mockCouponService";
import { mockPaymentService } from "@/services/payment/mockPaymentService";
import type { IOrderRepository, CreateOrderInput } from "./IOrderRepository";
import type { OrderListItem } from "@/services/api/ordersApi.types";
import type { Order, OrderStatus, ShippingMethodId } from "@/types";

export class MockOrderRepository implements IOrderRepository {
  private orders: Order[] = [];

  constructor(initialOrders: Order[] = []) {
    this.orders = initialOrders;
  }

  setOrders(orders: Order[]) {
    this.orders = orders;
  }

  getOrders(): Order[] {
    return this.orders;
  }

  async create(input: CreateOrderInput): Promise<Order> {
    const payment = mockPaymentService.process({
      method: input.paymentMethod,
      installments: input.installments,
      total: input.subtotal - input.discount + input.shippingOption.price,
    });

    const now = new Date().toISOString();
    const order: Order = {
      id: generateId("ped"),
      orderNumber: undefined,
      userId: input.userId,
      items: input.items,
      subtotal: input.subtotal,
      discount: input.discount,
      shippingPrice: input.shippingOption.price,
      total: input.subtotal - input.discount + input.shippingOption.price,
      couponCode: input.couponCode,
      shipping: {
        methodId: input.shippingOption.id as ShippingMethodId,
        methodName: `${input.shippingOption.provider} — ${input.shippingOption.name}`,
        provider: input.shippingOption.provider,
        estimatedDays: input.shippingOption.estimatedDays,
        address: input.address,
      },
      payment: {
        method: input.paymentMethod,
        installments: input.installments,
        status: payment.message,
      },
      status: mockPaymentService.initialStatus(input.paymentMethod),
      createdAt: now,
      updatedAt: now,
      upSellerExport: {
        customerName: input.customerName,
        customerEmail: input.customerEmail,
        customerPhone: input.customerPhone,
        customerCpf: input.customerCpf,
      },
    };

    if (input.couponCode) {
      mockCouponService.markUsed(input.userId, input.couponCode);
    }

    this.orders = [order, ...this.orders];
    return order;
  }

  async getById(id: string): Promise<Order | undefined> {
    return this.orders.find((o) => o.id === id);
  }

  async getByUser(userId: string): Promise<Order[]> {
    return this.orders.filter((o) => o.userId === userId);
  }

  async listMineSummaries(userId: string): Promise<OrderListItem[]> {
    return this.orders
      .filter((o) => o.userId === userId)
      .map((o) => ({
        id: o.id,
        orderNumber: o.orderNumber ?? o.id,
        status: o.status,
        total: o.total,
        itemCount: o.items.reduce((sum, i) => sum + i.quantity, 0),
        customerName: o.upSellerExport?.customerName ?? "",
        createdAt: o.createdAt,
      }));
  }

  async listAll(): Promise<Order[]> {
    return this.orders;
  }

  async updateStatus(id: string, status: OrderStatus): Promise<void> {
    this.orders = this.orders.map((o) =>
      o.id === id ? { ...o, status, updatedAt: new Date().toISOString() } : o,
    );
  }
}
