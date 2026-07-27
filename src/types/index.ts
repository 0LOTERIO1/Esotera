export type Product = {
  id: string;
  slug: string;
  name: string;
  shortDescription: string;
  description: string;
  price: number;
  category: string;
  images: string[];
  features: string[];
  packageContents?: string[];
  variations?: string[];
  isFeatured: boolean;
  isAvailable: boolean;
  isDemo?: boolean;
};

export type Address = {
  cep: string;
  street: string;
  number: string;
  complement?: string;
  neighborhood: string;
  city: string;
  state: string;
};

export type UserRole = "customer" | "admin";

export type User = {
  id: string;
  name: string;
  email: string;
  cpf: string;
  phone: string;
  address: Address;
  role: UserRole;
  createdAt: string;
};

export type CartItem = {
  productId: string;
  quantity: number;
  variation?: string;
};

export type AppliedCoupon = {
  code: string;
  discountAmount: number;
};

export type OrderStatus =
  | "awaiting_payment"
  | "payment_approved"
  | "preparing"
  | "shipped"
  | "delivered"
  | "cancelled";

export type PaymentMethod = "pix" | "card" | "boleto";

export type ShippingMethodId = "j3" | "melhor_economico" | "melhor_expresso";

export type ShippingOption = {
  id: ShippingMethodId;
  provider: "J3" | "Melhor Envio";
  name: string;
  price: number;
  originalPrice: number;
  estimatedDays: string;
  description: string;
  isSameDay?: boolean;
};

export type OrderItem = {
  productId: string;
  name: string;
  price: number;
  quantity: number;
  variation?: string;
  image: string;
};

export type Order = {
  id: string;
  userId: string;
  items: OrderItem[];
  subtotal: number;
  discount: number;
  shippingPrice: number;
  total: number;
  couponCode?: string;
  shipping: {
    methodId: ShippingMethodId;
    methodName: string;
    provider: string;
    estimatedDays: string;
    address: Address;
  };
  payment: {
    method: PaymentMethod;
    installments?: number;
    status: string;
  };
  status: OrderStatus;
  createdAt: string;
  updatedAt: string;
  /** Campos preparados para futura exportação UpSeller */
  upSellerExport?: {
    customerName: string;
    customerEmail: string;
    customerPhone: string;
    customerCpf: string;
  };
};

export type StoreSettings = {
  storeName: string;
  freeShippingMin: number;
  freeShippingStates: string[];
  j3Price: number;
  j3CutoffHour: number;
  couponDiscount: number;
  couponMinPurchase: number;
  shippingSubsidy: {
    enabled: boolean;
    amount: number;
  };
};

export type ToastMessage = {
  id: string;
  type: "success" | "error" | "info";
  message: string;
};
