import { formatEstimatedDays } from "@/utils/format";
import { normalizeProductImageUrl } from "@/utils/productImage";
import type {
  AdminCustomer,
  AdminDashboard,
  AdminOrderDetail,
  AdminOrderSummary,
  AdminOrdersPage,
  AdminRecentOrder,
  AdminSoldProduct,
} from "./adminTypes";
import type { OrderStatus, PaymentMethod } from "@/types";

export function mapAdminDashboard(api: {
  totalOrders: number;
  totalSales: number;
  awaitingPayment: number;
  paymentApproved: number;
  preparing: number;
  shipped: number;
  delivered: number;
  cancelled: number;
  availableProducts: number;
  customersWithOrders: number;
  recentOrders: {
    id: string;
    orderNumber: string;
    status: string;
    total: number;
    customerName: string;
    createdAt: string;
  }[];
  topProducts: {
    productId?: string | null;
    productName: string;
    imageUrl?: string | null;
    quantitySold: number;
    totalRevenue: number;
    orderCount: number;
  }[];
}): AdminDashboard {
  return {
    totalOrders: api.totalOrders,
    totalSales: api.totalSales,
    awaitingPayment: api.awaitingPayment,
    paymentApproved: api.paymentApproved,
    preparing: api.preparing,
    shipped: api.shipped,
    delivered: api.delivered,
    cancelled: api.cancelled,
    availableProducts: api.availableProducts,
    customersWithOrders: api.customersWithOrders,
    recentOrders: api.recentOrders.map(mapRecentOrder),
    topProducts: api.topProducts.map(mapSoldProduct),
  };
}

export function mapRecentOrder(api: {
  id: string;
  orderNumber: string;
  status: string;
  total: number;
  customerName: string;
  createdAt: string;
}): AdminRecentOrder {
  return {
    id: api.id,
    orderNumber: api.orderNumber,
    status: api.status as OrderStatus,
    total: api.total,
    customerName: api.customerName,
    createdAt: api.createdAt,
  };
}

export function mapSoldProduct(api: {
  productId?: string | null;
  productName: string;
  imageUrl?: string | null;
  quantitySold: number;
  totalRevenue: number;
  orderCount: number;
}): AdminSoldProduct {
  return {
    productId: api.productId,
    productName: api.productName,
    imageUrl: api.imageUrl,
    image: normalizeProductImageUrl(api.imageUrl),
    quantitySold: api.quantitySold,
    totalRevenue: api.totalRevenue,
    orderCount: api.orderCount,
  };
}

export function mapAdminCustomer(api: {
  id: string;
  name: string;
  email: string;
  phone?: string | null;
  orderCount: number;
  totalSpent: number;
  lastOrderAt?: string | null;
}): AdminCustomer {
  return {
    id: api.id,
    name: api.name,
    email: api.email,
    phone: api.phone,
    orderCount: api.orderCount,
    totalSpent: api.totalSpent,
    lastOrderAt: api.lastOrderAt,
  };
}

export function mapAdminOrderSummary(api: {
  id: string;
  orderNumber: string;
  status: string;
  total: number;
  itemCount: number;
  customerName: string;
  paymentMethod: string;
  shippingMethodName: string;
  createdAt: string;
  rowVersion: number;
}): AdminOrderSummary {
  return {
    id: api.id,
    orderNumber: api.orderNumber,
    status: api.status as OrderStatus,
    total: api.total,
    itemCount: api.itemCount,
    customerName: api.customerName,
    paymentMethod: api.paymentMethod as PaymentMethod,
    shippingMethodName: api.shippingMethodName,
    createdAt: api.createdAt,
    rowVersion: api.rowVersion,
  };
}

export function mapAdminOrdersPage(api: {
  items: Parameters<typeof mapAdminOrderSummary>[0][];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages?: number;
}): AdminOrdersPage {
  return {
    items: api.items.map(mapAdminOrderSummary),
    totalCount: api.totalCount,
    page: api.page,
    pageSize: api.pageSize,
    totalPages:
      api.totalPages ??
      Math.max(1, Math.ceil(api.totalCount / Math.max(api.pageSize, 1))),
  };
}

export function mapAdminOrderDetail(api: {
  id: string;
  orderNumber: string;
  status: string;
  subtotal: number;
  discount: number;
  shippingPrice: number;
  total: number;
  couponCode?: string | null;
  shipping: {
    methodId: string;
    methodName: string;
    provider: string;
    estimatedDays: number | null;
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
  };
  address: {
    cep: string;
    street: string;
    number: string;
    complement?: string | null;
    neighborhood: string;
    city: string;
    state: string;
  };
  items: {
    id: string;
    productId?: string | null;
    productName: string;
    unitPrice: number;
    quantity: number;
    variation?: string | null;
    sku?: string | null;
    imageUrl?: string | null;
    lineTotal: number;
  }[];
  statusHistory: {
    fromStatus?: string | null;
    toStatus: string;
    note?: string | null;
    createdAt: string;
  }[];
  fiscal: {
    fiscalStatus: string;
    maskedChNFe?: string | null;
    invoiceNumber?: string | null;
    invoiceSeries?: string | null;
    authorizedAtUtc?: string | null;
  };
  createdAt: string;
  updatedAt: string;
  rowVersion: number;
}): AdminOrderDetail {
  return {
    id: api.id,
    orderNumber: api.orderNumber,
    status: api.status as OrderStatus,
    subtotal: api.subtotal,
    discount: api.discount,
    shippingPrice: api.shippingPrice,
    total: api.total,
    couponCode: api.couponCode ?? undefined,
    shipping: {
      methodId: api.shipping.methodId,
      methodName: api.shipping.methodName,
      provider: api.shipping.provider,
      estimatedDays: formatEstimatedDays(api.shipping.estimatedDays),
    },
    payment: {
      method: api.payment.method as PaymentMethod,
      installments: api.payment.installments ?? undefined,
      status: api.payment.status,
    },
    customer: {
      name: api.customer.name,
      email: api.customer.email,
      phone: api.customer.phone ?? undefined,
    },
    address: {
      cep: api.address.cep,
      street: api.address.street,
      number: api.address.number,
      complement: api.address.complement ?? undefined,
      neighborhood: api.address.neighborhood,
      city: api.address.city,
      state: api.address.state,
    },
    items: api.items.map((item) => ({
      id: item.id,
      productId: item.productId ?? undefined,
      name: item.productName,
      price: item.unitPrice,
      quantity: item.quantity,
      variation: item.variation ?? undefined,
      sku: item.sku ?? undefined,
      image: normalizeProductImageUrl(item.imageUrl),
      lineTotal: item.lineTotal,
    })),
    statusHistory: api.statusHistory.map((h) => ({
      fromStatus: h.fromStatus ?? undefined,
      toStatus: h.toStatus,
      note: h.note ?? undefined,
      createdAt: h.createdAt,
    })),
    fiscal: {
      fiscalStatus: api.fiscal?.fiscalStatus ?? "awaiting_xml",
      maskedChNFe: api.fiscal?.maskedChNFe ?? undefined,
      invoiceNumber: api.fiscal?.invoiceNumber ?? undefined,
      invoiceSeries: api.fiscal?.invoiceSeries ?? undefined,
      authorizedAtUtc: api.fiscal?.authorizedAtUtc ?? undefined,
    },
    createdAt: api.createdAt,
    updatedAt: api.updatedAt,
    rowVersion: api.rowVersion,
  };
}
