import type {
  AdminCouponDto,
  CreateCouponPayload,
  UpdateCouponPayload,
} from "@/services/api/couponsApi";

export type CouponValidationResult =
  | { ok: true; discountAmount: number; code: string }
  | { ok: false; reason: "invalid" | "min" | "used"; message: string };

export type AdminCouponListFilter = {
  archived?: "active" | "archived" | "all";
  isActive?: boolean;
};

export interface ICouponRepository {
  validate(params: {
    code: string;
    subtotal: number;
    userId?: string | null;
  }): Promise<CouponValidationResult>;

  listAdmin?(filter?: AdminCouponListFilter): Promise<AdminCouponDto[]>;
  getAdmin?(id: string): Promise<AdminCouponDto>;
  create?(payload: CreateCouponPayload): Promise<AdminCouponDto>;
  update?(id: string, payload: UpdateCouponPayload): Promise<AdminCouponDto>;
  activate?(id: string): Promise<AdminCouponDto>;
  deactivate?(id: string): Promise<AdminCouponDto>;
  archive?(id: string): Promise<AdminCouponDto>;
  restore?(id: string): Promise<AdminCouponDto>;
}
