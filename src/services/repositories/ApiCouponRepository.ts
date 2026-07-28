import { ApiError } from "@/services/api/apiClient";
import { couponsApi } from "@/services/api/couponsApi";
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

export class ApiCouponRepository implements ICouponRepository {
  async validate(params: {
    code: string;
    subtotal: number;
    userId?: string | null;
  }): Promise<CouponValidationResult> {
    try {
      const response = await couponsApi.validate(params.code, params.subtotal);

      if (response.isValid) {
        return {
          ok: true,
          code: (response.code ?? params.code).trim().toUpperCase(),
          discountAmount: response.discountAmount ?? 0,
        };
      }

      const message = response.errorMessage ?? "Cupom inválido.";
      const reason =
        /mínima/i.test(message) ? "min" : /já utiliz/i.test(message) ? "used" : "invalid";

      return { ok: false, reason, message };
    } catch (err) {
      if (err instanceof ApiError) {
        return {
          ok: false,
          reason: err.status === 409 ? "used" : "invalid",
          message: err.userMessage,
        };
      }
      throw err;
    }
  }

  listAdmin(filter?: AdminCouponListFilter): Promise<AdminCouponDto[]> {
    return couponsApi.listAdmin({
      archived:
        filter?.archived === "archived"
          ? "only"
          : filter?.archived === "all"
            ? "all"
            : undefined,
      isActive: filter?.isActive,
    });
  }

  getAdmin(id: string): Promise<AdminCouponDto> {
    return couponsApi.getAdmin(id);
  }

  create(payload: CreateCouponPayload): Promise<AdminCouponDto> {
    return couponsApi.create(payload);
  }

  update(id: string, payload: UpdateCouponPayload): Promise<AdminCouponDto> {
    return couponsApi.update(id, payload);
  }

  activate(id: string): Promise<AdminCouponDto> {
    return couponsApi.activate(id);
  }

  deactivate(id: string): Promise<AdminCouponDto> {
    return couponsApi.deactivate(id);
  }

  archive(id: string): Promise<AdminCouponDto> {
    return couponsApi.archive(id);
  }

  restore(id: string): Promise<AdminCouponDto> {
    return couponsApi.restore(id);
  }
}
