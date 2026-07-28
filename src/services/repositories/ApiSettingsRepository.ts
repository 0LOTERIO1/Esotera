import { settingsApi } from "@/services/api/settingsApi";
import {
  mapAdminSettingsToStore,
  mapPublicSettingsToStore,
} from "@/services/api/settingsMapper";
import type { UpdateStoreSettingsPayload } from "@/services/api/settingsApi";
import type { StoreSettings } from "@/types";
import type { ISettingsRepository } from "./ISettingsRepository";

export class ApiSettingsRepository implements ISettingsRepository {
  async getPublic(): Promise<StoreSettings> {
    const dto = await settingsApi.getPublic();
    return mapPublicSettingsToStore(dto);
  }

  async getAdmin(): Promise<StoreSettings> {
    const dto = await settingsApi.getAdmin();
    return mapAdminSettingsToStore(dto);
  }

  async update(payload: UpdateStoreSettingsPayload): Promise<StoreSettings> {
    const dto = await settingsApi.update(payload);
    return mapAdminSettingsToStore(dto);
  }
}
