import type { StoreSettings } from "@/types";
import type {
  AdminStoreSettingsDto,
  PublicStoreSettingsDto,
  UpdateStoreSettingsPayload,
} from "@/services/api/settingsApi";

export function mapPublicSettingsToStore(
  dto: PublicStoreSettingsDto,
  legacy?: Pick<StoreSettings, "couponDiscount" | "couponMinPurchase">,
): StoreSettings {
  return {
    storeName: dto.storeName,
    freeShippingMin: dto.freeShippingMin,
    freeShippingStates: [...dto.freeShippingStates],
    j3Price: dto.j3Price,
    j3CutoffHour: dto.j3CutoffHour,
    couponDiscount: legacy?.couponDiscount ?? 5,
    couponMinPurchase: legacy?.couponMinPurchase ?? 30,
    shippingSubsidy: {
      enabled: dto.shippingSubsidyEnabled,
      amount: dto.shippingSubsidyAmount,
    },
  };
}

export function mapAdminSettingsToStore(
  dto: AdminStoreSettingsDto,
  legacy?: Pick<StoreSettings, "couponDiscount" | "couponMinPurchase">,
): StoreSettings {
  return mapPublicSettingsToStore(dto, legacy);
}

export function toUpdateSettingsPayload(
  settings: StoreSettings,
): UpdateStoreSettingsPayload {
  return {
    storeName: settings.storeName,
    freeShippingMin: settings.freeShippingMin,
    freeShippingStates: settings.freeShippingStates,
    j3Price: settings.j3Price,
    j3CutoffHour: settings.j3CutoffHour,
    shippingSubsidyEnabled: settings.shippingSubsidy.enabled,
    shippingSubsidyAmount: settings.shippingSubsidy.amount,
  };
}
