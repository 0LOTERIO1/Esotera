import type {
  Order,
  OrderStatus,
  PaymentMethod,
  ShippingMethodId,
  Address,
} from "@/types";
import type { OrderListItem } from "@/services/api/ordersApi.types";
import { normalizeProductImageUrl } from "@/utils/productImage";

/** Contratos crus da API de pedidos (fonte de verdade no modo API). */
export type ApiOrderItemDto = {
  id: string;
  productId?: string | null;
  productName: string;
  unitPrice: number;
  quantity: number;
  variation?: string | null;
  imageUrl?: string | null;
  lineTotal: number;
};

export type ApiOrderDto = {
  id: string;
  orderNumber: string;
  status: OrderStatus;
  subtotal: number;
  discount: number;
  shippingPrice: number;
  total: number;
  couponCode?: string | null;
  shipping: {
    methodId: string;
    methodName: string;
    provider: string;
    estimatedDays: number;
  };
  payment: {
    method: string;
    installments?: number | null;
    status: string;
  };
  customer: {
    name: string;
    email: string;
    phone?: string | null;
    cpf?: string | null;
  };
  address: Address;
  items: ApiOrderItemDto[];
  createdAt: string;
  updatedAt: string;
};

export type ApiOrderListItemDto = {
  id: string;
  orderNumber: string;
  status: OrderStatus;
  total: number;
  itemCount: number;
  customerName: string;
  createdAt: string;
};

function formatEstimatedDays(days: number): string {
  if (days <= 0) return "Hoje (mesmo dia) — simulado";
  if (days === 1) return "1 dia útil — simulado";
  return `${days} dias úteis — simulado`;
}

/**
 * Mapper único OrderDto (API) → tipos do frontend.
 * Use este módulo em vez de converter nos componentes.
 */
export function mapApiOrderToOrder(
  apiOrder: ApiOrderDto,
  userId = "",
): Order {
  return {
    id: apiOrder.id,
    orderNumber: apiOrder.orderNumber,
    userId,
    items: apiOrder.items.map((item) => ({
      productId: item.productId ?? "",
      name: item.productName,
      price: item.unitPrice,
      quantity: item.quantity,
      variation: item.variation ?? undefined,
      image: normalizeProductImageUrl(item.imageUrl),
    })),
    subtotal: apiOrder.subtotal,
    discount: apiOrder.discount,
    shippingPrice: apiOrder.shippingPrice,
    total: apiOrder.total,
    couponCode: apiOrder.couponCode ?? undefined,
    shipping: {
      methodId: apiOrder.shipping.methodId as ShippingMethodId,
      methodName: apiOrder.shipping.methodName,
      provider: apiOrder.shipping.provider,
      estimatedDays: formatEstimatedDays(apiOrder.shipping.estimatedDays),
      address: {
        cep: apiOrder.address.cep,
        street: apiOrder.address.street,
        number: apiOrder.address.number,
        complement: apiOrder.address.complement,
        neighborhood: apiOrder.address.neighborhood,
        city: apiOrder.address.city,
        state: apiOrder.address.state,
      },
    },
    payment: {
      method: apiOrder.payment.method as PaymentMethod,
      installments: apiOrder.payment.installments ?? undefined,
      status: apiOrder.payment.status,
    },
    status: apiOrder.status,
    createdAt: apiOrder.createdAt,
    updatedAt: apiOrder.updatedAt,
    upSellerExport: {
      customerName: apiOrder.customer.name,
      customerEmail: apiOrder.customer.email,
      customerPhone: apiOrder.customer.phone ?? "",
      customerCpf: apiOrder.customer.cpf ?? "",
    },
  };
}

export function mapApiOrderListItem(
  item: ApiOrderListItemDto,
): OrderListItem {
  return {
    id: item.id,
    orderNumber: item.orderNumber,
    status: item.status,
    total: item.total,
    itemCount: item.itemCount,
    customerName: item.customerName,
    createdAt: item.createdAt,
  };
}
