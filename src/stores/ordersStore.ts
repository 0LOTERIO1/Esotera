"use client";

import { create } from "zustand";
import { persist } from "zustand/middleware";
import { STORAGE_KEYS } from "@/utils/storage";
import { isApiMode } from "@/config/dataMode";
import { MockOrderRepository } from "@/services/repositories/MockOrderRepository";
import { getOrderRepository } from "@/services/repositories";
import type { CreateOrderInput } from "@/services/repositories/IOrderRepository";
import type { OrderListItem } from "@/services/api/ordersApi.types";
import type { Order, OrderStatus } from "@/types";

/**
 * Pedidos do cliente:
 * - mock: localStorage via MockOrderRepository
 * - api: backend real (sem fallback mock)
 *
 * Painel admin permanece demonstrativo no localStorage (não mistura pedidos da API).
 * TODO Fase admin: conectar painel administrativo à API de pedidos.
 */
const demoOrders = new MockOrderRepository();

type OrdersState = {
  /** Pedidos mock / demo admin — não é fonte de verdade em modo API */
  orders: Order[];
  hydrated: boolean;
  setHydrated: (value: boolean) => void;
  createOrder: (input: CreateOrderInput) => Promise<Order>;
  getById: (id: string) => Order | undefined;
  getByUser: (userId: string) => Order[];
  fetchById: (id: string) => Promise<Order | undefined>;
  fetchMineSummaries: (userId: string) => Promise<OrderListItem[]>;
  getAllOrders: () => Promise<Order[]>;
  updateStatus: (id: string, status: OrderStatus) => Promise<void>;
};

export const useOrdersStore = create<OrdersState>()(
  persist(
    (set, get) => ({
      orders: [],
      hydrated: false,
      setHydrated: (value) => set({ hydrated: value }),

      createOrder: async (input) => {
        if (isApiMode()) {
          return getOrderRepository().create(input);
        }
        demoOrders.setOrders(get().orders);
        const order = await demoOrders.create(input);
        set({ orders: demoOrders.getOrders() });
        return order;
      },

      getById: (id) => get().orders.find((o) => o.id === id),

      getByUser: (userId) => get().orders.filter((o) => o.userId === userId),

      fetchById: async (id) => {
        if (isApiMode()) {
          return getOrderRepository().getById(id);
        }
        return get().orders.find((o) => o.id === id);
      },

      fetchMineSummaries: async (userId) => {
        if (isApiMode()) {
          return getOrderRepository().listMineSummaries(userId);
        }
        demoOrders.setOrders(get().orders);
        return demoOrders.listMineSummaries(userId);
      },

      getAllOrders: async () => {
        // Admin demo — sempre local, nunca API nesta fase
        demoOrders.setOrders(get().orders);
        const orders = await demoOrders.listAll();
        set({ orders });
        return orders;
      },

      updateStatus: async (id, status) => {
        // Admin demo — sempre local
        demoOrders.setOrders(get().orders);
        await demoOrders.updateStatus(id, status);
        set({ orders: demoOrders.getOrders() });
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
