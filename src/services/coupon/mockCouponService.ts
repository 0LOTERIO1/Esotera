import { defaultCoupon } from "@/config/coupon";
import { STORAGE_KEYS, safeParseJSON } from "@/utils/storage";

export type CouponValidationResult =
  | { ok: true; discountAmount: number; code: string }
  | { ok: false; reason: "invalid" | "min" | "used"; message: string };

function readUsage(): Record<string, string[]> {
  if (typeof window === "undefined") return {};
  return safeParseJSON(localStorage.getItem(STORAGE_KEYS.couponUsage), {});
}

function writeUsage(usage: Record<string, string[]>) {
  localStorage.setItem(STORAGE_KEYS.couponUsage, JSON.stringify(usage));
}

export const mockCouponService = {
  getConfig(discountAmount?: number, minPurchase?: number) {
    return {
      ...defaultCoupon,
      discountAmount: discountAmount ?? defaultCoupon.discountAmount,
      minPurchase: minPurchase ?? defaultCoupon.minPurchase,
    };
  },

  hasBeenUsed(userId: string, code: string): boolean {
    const usage = readUsage();
    return (usage[userId] ?? []).includes(code.toUpperCase());
  },

  validate(params: {
    code: string;
    subtotal: number;
    userId?: string | null;
    discountAmount?: number;
    minPurchase?: number;
    /** Quando informado, valida contra este código (admin mock); senão usa defaultCoupon */
    expectedCode?: string;
  }): CouponValidationResult {
    const config = this.getConfig(params.discountAmount, params.minPurchase);
    const code = params.code.trim().toUpperCase();
    const expected = (params.expectedCode ?? config.code).trim().toUpperCase();

    if (code !== expected) {
      return {
        ok: false,
        reason: "invalid",
        message: "Cupom inválido.",
      };
    }

    if (params.subtotal < config.minPurchase) {
      return {
        ok: false,
        reason: "min",
        message: `Compra mínima de R$ ${config.minPurchase.toFixed(2).replace(".", ",")} para este cupom.`,
      };
    }

    if (params.userId && this.hasBeenUsed(params.userId, code)) {
      return {
        ok: false,
        reason: "used",
        message: "Cupom já utilizado por este cliente.",
      };
    }

    return {
      ok: true,
      code,
      discountAmount: Math.min(config.discountAmount, params.subtotal),
    };
  },

  markUsed(userId: string, code: string) {
    const usage = readUsage();
    const list = usage[userId] ?? [];
    const upper = code.toUpperCase();
    if (!list.includes(upper)) {
      usage[userId] = [...list, upper];
      writeUsage(usage);
    }
  },
};
