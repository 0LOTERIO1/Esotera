export const STORAGE_KEYS = {
  cart: "esotera-cart",
  auth: "esotera-auth",
  users: "esotera-users",
  orders: "esotera-orders",
  products: "esotera-products-v3",
  settings: "esotera-settings",
  couponUsage: "esotera-coupon-usage",
  /** Endereços mock (modo DATA_MODE=mock) — por userId */
  addresses: "esotera-addresses",
} as const;

export function safeParseJSON<T>(value: string | null, fallback: T): T {
  if (!value) return fallback;
  try {
    return JSON.parse(value) as T;
  } catch {
    return fallback;
  }
}
