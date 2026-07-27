export const STORAGE_KEYS = {
  cart: "esotera-cart",
  auth: "esotera-auth",
  users: "esotera-users",
  orders: "esotera-orders",
  products: "esotera-products-v2",
  settings: "esotera-settings",
  couponUsage: "esotera-coupon-usage",
} as const;

export function safeParseJSON<T>(value: string | null, fallback: T): T {
  if (!value) return fallback;
  try {
    return JSON.parse(value) as T;
  } catch {
    return fallback;
  }
}
