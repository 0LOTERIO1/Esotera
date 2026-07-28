import type { StoreSettings } from "@/types";
import type { UpdateStoreSettingsPayload } from "@/services/api/settingsApi";

export interface ISettingsRepository {
  getPublic(): Promise<StoreSettings>;
  getAdmin(): Promise<StoreSettings>;
  update(payload: UpdateStoreSettingsPayload): Promise<StoreSettings>;
}
