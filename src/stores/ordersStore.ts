"use client";

import { create } from "zustand";
import { persist } from "zustand/middleware";
import { STORAGE_KEYS } from "@/utils/storage";
import { generateId } from "@/utils/format";
import { mockCouponService } from "@/services/coupon/mockCouponService";
import { mockPaymentService } from "@/services/payment/mockPaymentService";
import type {
  Address,
  Order,
  OrderItem,
  OrderStatus,
  PaymentMethod,
  ShippingMethodId,
  ShippingOption,
} from "@/types";

type CreateOrderInput = {
  userId: string;
  customerName: string;
  customerEmail: string;
  customerPhone: string;
  customerCpf: string;
  items: OrderItem[];
  subtotal: number;
  discount: number;
  couponCode?: string;
  shippingOption: ShippingOption;
  address: Address;
  paymentMethod: PaymentMethod;
  installments?: number;
};

type OrdersState = {
  orders: Order[];
  hydrated: boolean;
  setHydrated: (value: boolean) => void;
  createOrder: (input: CreateOrderInput) => Order;
  getById: (id: string) => Order | undefined;
  getByUser: (userId: string) => Order[];
  updateStatus: (id: string, status: OrderStatus) => void;
};

export const useOrdersStore = create<OrdersState>()(
  persist(
    (set, get) => ({
      orders: [],
      hydrated: false,
      setHydrated: (value) => set({ hydrated: value }),
      createOrder: (input) => {
        const payment = mockPaymentService.process({
          method: input.paymentMethod,
          installments: input.installments,
          total: input.subtotal - input.discount + input.shippingOption.price,
        });

        const now = new Date().toISOString();
        const order: Order = {
          id: generateId("ped"),
          userId: input.userId,
          items: input.items,
          subtotal: input.subtotal,
          discount: input.discount,
          shippingPrice: input.shippingOption.price,
          total:
            input.subtotal - input.discount + input.shippingOption.price,
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

        set((state) => ({ orders: [order, ...state.orders] }));
        return order;
      },
      getById: (id) => get().orders.find((o) => o.id === id),
      getByUser: (userId) =>
        get().orders.filter((o) => o.userId === userId),
      updateStatus: (id, status) => {
        set((state) => ({
          orders: state.orders.map((o) =>
            o.id === id
              ? { ...o, status, updatedAt: new Date().toISOString() }
              : o,
          ),
        }));
      },
    }),
    {
      name: STORAGE_KEYS.orders,
      onRehydrateStorage: () => (state) => {
        state?.setHydrated(true);
      },
      partialize: (state) => ({ orders: state.orders }),
    },
  ),
);
