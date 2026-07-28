import type { Order, OrderStatus, OrderItem, Address, PaymentMethod, ShippingOption } from "@/types";
import type { OrderListItem } from "@/services/api/ordersApi.types";

export type CreateOrderInput = {
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
  /** Snapshot local (modo mock) */
  address: Address;
  /** ID do endereço salvo — obrigatório em modo API */
  addressId?: string;
  paymentMethod: PaymentMethod;
  installments?: number;
  /** Cabeçalho Idempotency-Key — obrigatório em modo API */
  idempotencyKey?: string;
};

export interface IOrderRepository {
  create(input: CreateOrderInput): Promise<Order>;
  getById(id: string): Promise<Order | undefined>;
  getByUser(userId: string): Promise<Order[]>;
  /** Resumo do histórico (API); no mock deriva dos pedidos completos */
  listMineSummaries(userId: string): Promise<OrderListItem[]>;
  listAll(): Promise<Order[]>;
  updateStatus(id: string, status: OrderStatus): Promise<void>;
}
