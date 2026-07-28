import { defaultCoupon } from "@/config/coupon";
import { mockCouponService } from "@/services/coupon/mockCouponService";
import { safeParseJSON } from "@/utils/storage";
import type {
  AdminCouponDto,
  CreateCouponPayload,
  UpdateCouponPayload,
} from "@/services/api/couponsApi";
import type {
  ICouponRepository,
  CouponValidationResult,
  AdminCouponListFilter,
} from "./ICouponRepository";

const MOCK_COUPONS_KEY = "esotera-mock-coupons";

function seedCoupons(): AdminCouponDto[] {
  const now = new Date().toISOString();
  return [
    {
      id: "00000000-0000-0000-0000-000000000001",
      code: defaultCoupon.code,
      discountAmount: defaultCoupon.discountAmount,
      minPurchase: defaultCoupon.minPurchase,
      appliesToShipping: false,
      oneUsePerCustomer: true,
      maxTotalUses: null,
      usageCount: 0,
      isActive: true,
      isArchived: false,
      archivedAtUtc: null,
      validFromUtc: null,
      validUntilUtc: null,
      createdAtUtc: now,
      updatedAtUtc: now,
    },
  ];
}

function readCoupons(): AdminCouponDto[] {
  if (typeof window === "undefined") return seedCoupons();
  const stored = safeParseJSON<AdminCouponDto[] | null>(
    localStorage.getItem(MOCK_COUPONS_KEY),
    null,
  );
  if (!stored || stored.length === 0) {
    const seed = seedCoupons();
    localStorage.setItem(MOCK_COUPONS_KEY, JSON.stringify(seed));
    return seed;
  }
  return stored;
}

function writeCoupons(list: AdminCouponDto[]) {
  localStorage.setItem(MOCK_COUPONS_KEY, JSON.stringify(list));
}

function normalizeCode(code: string) {
  return code.trim().toUpperCase();
}

export class MockCouponRepository implements ICouponRepository {
  async validate(params: {
    code: string;
    subtotal: number;
    userId?: string | null;
  }): Promise<CouponValidationResult> {
    const code = normalizeCode(params.code);
    const coupon = readCoupons().find(
      (c) => c.code === code && !c.isArchived && c.isActive,
    );
    if (!coupon) {
      return { ok: false, reason: "invalid", message: "Cupom inválido." };
    }
    return mockCouponService.validate({
      ...params,
      code,
      expectedCode: coupon.code,
      discountAmount: coupon.discountAmount,
      minPurchase: coupon.minPurchase,
    });
  }

  async listAdmin(filter?: AdminCouponListFilter): Promise<AdminCouponDto[]> {
    let list = readCoupons();
    const archived = filter?.archived ?? "active";
    if (archived === "active") list = list.filter((c) => !c.isArchived);
    else if (archived === "archived") list = list.filter((c) => c.isArchived);
    if (filter?.isActive !== undefined)
      list = list.filter((c) => c.isActive === filter.isActive);
    return list.sort((a, b) => b.updatedAtUtc.localeCompare(a.updatedAtUtc));
  }

  async getAdmin(id: string): Promise<AdminCouponDto> {
    const found = readCoupons().find((c) => c.id === id);
    if (!found) throw new Error("Cupom não encontrado.");
    return found;
  }

  async create(payload: CreateCouponPayload): Promise<AdminCouponDto> {
    const code = normalizeCode(payload.code);
    const list = readCoupons();
    if (list.some((c) => c.code === code)) {
      throw new Error(`Já existe um cupom com o código '${code}'.`);
    }
    const now = new Date().toISOString();
    const coupon: AdminCouponDto = {
      id: crypto.randomUUID(),
      code,
      discountAmount: payload.discountAmount,
      minPurchase: payload.minPurchase,
      appliesToShipping: false,
      oneUsePerCustomer: payload.oneUsePerCustomer ?? true,
      maxTotalUses: payload.maxTotalUses ?? null,
      usageCount: 0,
      isActive: payload.isActive ?? true,
      isArchived: false,
      archivedAtUtc: null,
      validFromUtc: payload.validFromUtc ?? null,
      validUntilUtc: payload.validUntilUtc ?? null,
      createdAtUtc: now,
      updatedAtUtc: now,
    };
    writeCoupons([coupon, ...list]);
    return coupon;
  }

  async update(id: string, payload: UpdateCouponPayload): Promise<AdminCouponDto> {
    const list = readCoupons();
    const idx = list.findIndex((c) => c.id === id);
    if (idx < 0) throw new Error("Cupom não encontrado.");
    const current = list[idx];
    let code = current.code;
    if (payload.code != null) {
      code = normalizeCode(payload.code);
      if (list.some((c) => c.code === code && c.id !== id)) {
        throw new Error(`Já existe um cupom com o código '${code}'.`);
      }
    }
    const updated: AdminCouponDto = {
      ...current,
      code,
      discountAmount: payload.discountAmount ?? current.discountAmount,
      minPurchase: payload.minPurchase ?? current.minPurchase,
      oneUsePerCustomer:
        payload.oneUsePerCustomer ?? current.oneUsePerCustomer,
      maxTotalUses:
        payload.clearMaxTotalUses === true
          ? null
          : (payload.maxTotalUses ?? current.maxTotalUses),
      isActive: payload.isActive ?? current.isActive,
      validFromUtc:
        payload.clearValidFrom === true
          ? null
          : (payload.validFromUtc ?? current.validFromUtc),
      validUntilUtc:
        payload.clearValidUntil === true
          ? null
          : (payload.validUntilUtc ?? current.validUntilUtc),
      appliesToShipping: false,
      updatedAtUtc: new Date().toISOString(),
    };
    list[idx] = updated;
    writeCoupons(list);
    return updated;
  }

  async activate(id: string): Promise<AdminCouponDto> {
    return this.update(id, { isActive: true });
  }

  async deactivate(id: string): Promise<AdminCouponDto> {
    return this.update(id, { isActive: false });
  }

  async archive(id: string): Promise<AdminCouponDto> {
    const list = readCoupons();
    const idx = list.findIndex((c) => c.id === id);
    if (idx < 0) throw new Error("Cupom não encontrado.");
    list[idx] = {
      ...list[idx],
      isArchived: true,
      isActive: false,
      archivedAtUtc: new Date().toISOString(),
      updatedAtUtc: new Date().toISOString(),
    };
    writeCoupons(list);
    return list[idx];
  }

  async restore(id: string): Promise<AdminCouponDto> {
    const list = readCoupons();
    const idx = list.findIndex((c) => c.id === id);
    if (idx < 0) throw new Error("Cupom não encontrado.");
    list[idx] = {
      ...list[idx],
      isArchived: false,
      archivedAtUtc: null,
      updatedAtUtc: new Date().toISOString(),
    };
    writeCoupons(list);
    return list[idx];
  }
}
