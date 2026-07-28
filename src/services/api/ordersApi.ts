import { apiClient } from "./apiClient";
import {
  mapApiOrderToOrder,
  mapApiOrderListItem,
  type ApiOrderDto,
  type ApiOrderListItemDto,
} from "./orderMapper";
import type { Order, PaymentMethod } from "@/types";
import type { OrderListItem } from "./ordersApi.types";

export type { OrderListItem } from "./ordersApi.types";

/** Contrato real POST /api/orders (sem preços/totais/userId/endereço inline) */
export type CreateOrderApiRequest = {
  items: {
    productId: string;
    quantity: number;
    variation?: string;
  }[];
  addressId: string;
  shippingMethodId: string;
  paymentMethod: PaymentMethod;
  installments?: number;
  couponCode?: string;
};

export const ordersApi = {
  async create(
    input: CreateOrderApiRequest,
    idempotencyKey: string,
  ): Promise<Order> {
    // Contrato enxuto: somente addressId (sem Address inline)
    const body = {
      items: input.items.map((i) => ({
        productId: i.productId,
        quantity: i.quantity,
        variation: i.variation ?? null,
      })),
      addressId: input.addressId,
      shippingMethodId: input.shippingMethodId,
      paymentMethod: input.paymentMethod,
      installments: input.installments ?? null,
      couponCode: input.couponCode ?? null,
    };

    const response = await apiClient.post<ApiOrderDto>("/api/orders", body, {
      auth: true,
      headers: {
        "Idempotency-Key": idempotencyKey,
      },
    });
    return mapApiOrderToOrder(response);
  },

  async listMine(): Promise<OrderListItem[]> {
    const response = await apiClient.get<ApiOrderListItemDto[]>("/api/orders", {
      auth: true,
    });
    return response.map(mapApiOrderListItem);
  },

  async getMine(id: string): Promise<Order | null> {
    try {
      const response = await apiClient.get<ApiOrderDto>(`/api/orders/${id}`, {
        auth: true,
      });
      return mapApiOrderToOrder(response);
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
};
