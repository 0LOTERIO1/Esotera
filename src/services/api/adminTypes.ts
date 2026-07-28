import type { OrderStatus, PaymentMethod, Address } from "@/types";

export type AdminDashboard = {
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
  recentOrders: AdminRecentOrder[];
  topProducts: AdminSoldProduct[];
};

export type AdminRecentOrder = {
  id: string;
  orderNumber: string;
  status: OrderStatus;
  total: number;
  customerName: string;
  createdAt: string;
};

export type AdminSoldProduct = {
  productId?: string | null;
  productName: string;
  imageUrl?: string | null;
  image: string;
  quantitySold: number;
  totalRevenue: number;
  orderCount: number;
};

export type AdminCustomer = {
  id: string;
  name: string;
  email: string;
  phone?: string | null;
  orderCount: number;
  totalSpent: number;
  lastOrderAt?: string | null;
};

export type AdminOrderSummary = {
  id: string;
  orderNumber: string;
  status: OrderStatus;
  total: number;
  itemCount: number;
  customerName: string;
  paymentMethod: PaymentMethod | string;
  shippingMethodName: string;
  createdAt: string;
  rowVersion: number;
};

export type AdminOrderDetail = {
  id: string;
  orderNumber: string;
  status: OrderStatus;
  subtotal: number;
  discount: number;
  shippingPrice: number;
  total: number;
  couponCode?: string;
  shipping: {
    methodId: string;
    methodName: string;
    provider: string;
    estimatedDays: string;
  };
  payment: {
    method: PaymentMethod | string;
    installments?: number;
    status: string;
  };
  customer: {
    name: string;
    email: string;
    phone?: string;
  };
  address: Address;
  items: {
    id: string;
    productId?: string;
    name: string;
    price: number;
    quantity: number;
    variation?: string;
    image: string;
    lineTotal: number;
  }[];
  statusHistory: {
    fromStatus?: string;
    toStatus: string;
    note?: string;
    createdAt: string;
  }[];
  createdAt: string;
  updatedAt: string;
  rowVersion: number;
};

export type AdminOrdersPage = {
  items: AdminOrderSummary[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
};
