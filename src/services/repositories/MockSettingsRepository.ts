import { defaultStoreSettings } from "@/config/shipping";
import type { UpdateStoreSettingsPayload } from "@/services/api/settingsApi";
import type { StoreSettings } from "@/types";
import type { ISettingsRepository } from "./ISettingsRepository";

/**
 * Mock isolado — não chama API. Usa defaults em memória por sessão do módulo
 * (o settingsStore/localStorage continua sendo a UI de edição no modo mock).
 */
let mockSettings: StoreSettings = { ...defaultStoreSettings, shippingSubsidy: { ...defaultStoreSettings.shippingSubsidy }, freeShippingStates: [...defaultStoreSettings.freeShippingStates] };

export class MockSettingsRepository implements ISettingsRepository {
  async getPublic(): Promise<StoreSettings> {
    return structuredClone(mockSettings);
  }

  async getAdmin(): Promise<StoreSettings> {
    return structuredClone(mockSettings);
  }

  async update(payload: UpdateStoreSettingsPayload): Promise<StoreSettings> {
    const states = payload.freeShippingStates
      .map((s) => s.trim().toUpperCase())
      .filter(Boolean);
    const unique = [...new Set(states)];
    mockSettings = {
      ...mockSettings,
      storeName: payload.storeName.trim() || "Esotera",
      freeShippingMin: payload.freeShippingMin,
      freeShippingStates: unique,
      j3Price: payload.j3Price,
      j3CutoffHour: payload.j3CutoffHour,
      shippingSubsidy: {
        enabled: payload.shippingSubsidyEnabled,
        amount: payload.shippingSubsidyAmount,
      },
      shippingOriginCep: payload.shippingOriginCep,
      packageLengthCm: payload.packageLengthCm,
      packageWidthCm: payload.packageWidthCm,
      packageHeightCm: payload.packageHeightCm,
      packageWeightGrams: payload.packageWeightGrams,
      melhorEnvioQuoteEnabled: payload.melhorEnvioQuoteEnabled,
    };
    return structuredClone(mockSettings);
  }

  /** Usado pelo settingsStore mock para sincronizar edições locais */
  static syncFromStore(settings: StoreSettings) {
    mockSettings = structuredClone(settings);
  }
}
