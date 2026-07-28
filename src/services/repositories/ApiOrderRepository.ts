import { ordersApi } from "@/services/api/ordersApi";
import { ApiError } from "@/services/api/apiClient";
import type { IOrderRepository, CreateOrderInput } from "./IOrderRepository";
import type { Order, OrderStatus } from "@/types";
import type { OrderListItem } from "@/services/api/ordersApi.types";

function toUserMessage(error: unknown): string {
  if (error instanceof ApiError) return error.userMessage;
  if (error instanceof Error) return error.message;
  return "Não foi possível processar o pedido.";
}

/**
 * Pedidos do cliente autenticado via API .NET.
 * Sem fallback para mock. Admin permanece mock (não usar listAll/updateStatus).
 */
export class ApiOrderRepository implements IOrderRepository {
  private rethrow(error: unknown): never {
    if (error instanceof ApiError) throw error;
    throw new Error(toUserMessage(error));
  }

  async create(input: CreateOrderInput): Promise<Order> {
    if (!input.addressId) {
      throw new Error("Selecione um endereço de entrega válido.");
    }
    if (!input.idempotencyKey) {
      throw new Error("Chave de idempotência ausente. Recarregue e tente novamente.");
    }

    try {
      // Envia apenas o contrato enxuto — preços/endereço completo/userId ficam no backend
      return await ordersApi.create(
        {
          items: input.items.map((i) => ({
            productId: i.productId,
            quantity: i.quantity,
            variation: i.variation,
          })),
          addressId: input.addressId,
          shippingMethodId: input.shippingOption.id,
          paymentMethod: input.paymentMethod,
          installments: input.installments,
          couponCode: input.couponCode,
        },
        input.idempotencyKey,
      );
    } catch (error) {
      this.rethrow(error);
    }
  }

  async getById(id: string): Promise<Order | undefined> {
    try {
      const order = await ordersApi.getMine(id);
      return order ?? undefined;
    } catch (error) {
      this.rethrow(error);
    }
  }

  async getByUser(userId: string): Promise<Order[]> {
    const summaries = await this.listMineSummaries(userId);
    const details: Order[] = [];
    for (const s of summaries) {
      const full = await this.getById(s.id);
      if (full) {
        details.push({ ...full, userId });
      }
    }
    return details;
  }

  async listMineSummaries(userId: string): Promise<OrderListItem[]> {
    void userId;
    try {
      return await ordersApi.listMine();
    } catch (error) {
      this.rethrow(error);
    }
  }

  /** TODO Fase admin: conectar painel administrativo à API de pedidos */
  async listAll(): Promise<Order[]> {
    return [];
  }

  /** TODO Fase admin: alteração de status via API */
  async updateStatus(id: string, status: OrderStatus): Promise<void> {
    void id;
    void status;
    throw new Error(
      "Atualização administrativa de status via API não está disponível nesta fase.",
    );
  }
}
