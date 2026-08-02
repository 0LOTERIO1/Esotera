/**
 * Fachada de frete do frontend.
 * Em modo API: sempre POST /api/shipping/quote (nunca Melhor Envio no browser).
 * Em modo mock: cotação simulada local.
 */
import { isApiMode } from "@/config/dataMode";
import {
  mapQuoteOption,
  quoteErrorMessage,
  shippingQuoteApi,
} from "@/services/api/shippingQuoteApi";
import {
  isJ3CepEligible,
  qualifiesForFreeShipping,
  quoteSimulatedShipping,
  simulatedShippingProvider,
  type ShippingQuoteInput,
} from "./simulatedShippingProvider";
import type {
  IShippingQuoteService,
  ShippingQuoteResult,
} from "./types";

const GENERIC_ERROR = "Não foi possível calcular o frete. Tente novamente.";

async function quoteFromApi(
  input: ShippingQuoteInput,
): Promise<ShippingQuoteResult> {
  try {
    const dto = await shippingQuoteApi.quote({
      destinationCep: input.cep,
      state: input.state,
      productsSubtotal: input.productsTotalAfterDiscount,
    });

    if (!dto.ok || !dto.options?.length) {
      return {
        ok: false,
        options: [],
        source: "carrier",
        errorMessage:
          dto.message ||
          (dto.errorCode === "invalid_cep"
            ? "CEP inválido. Verifique o endereço."
            : "Nenhuma modalidade disponível para este endereço. Tente outro CEP ou tente novamente."),
      };
    }

    return {
      ok: true,
      options: dto.options.map(mapQuoteOption),
      source: "carrier",
    };
  } catch (err) {
    return {
      ok: false,
      options: [],
      source: "carrier",
      errorMessage: quoteErrorMessage(err, GENERIC_ERROR),
    };
  }
}

export async function quoteShippingSafe(
  input: ShippingQuoteInput,
): Promise<ShippingQuoteResult> {
  if (isApiMode()) {
    return quoteFromApi(input);
  }

  try {
    const options = quoteSimulatedShipping(input);
    if (!options.length) {
      return {
        ok: false,
        options: [],
        source: "simulated",
        errorMessage:
          "Nenhuma modalidade disponível para este endereço. Tente outro CEP ou tente novamente.",
      };
    }
    return { ok: true, options, source: "simulated" };
  } catch {
    return {
      ok: false,
      options: [],
      source: "simulated",
      errorMessage: GENERIC_ERROR,
    };
  }
}

export const shippingService: IShippingQuoteService = {
  quote: quoteShippingSafe,
};

/** @deprecated Preferir shippingService / quoteShippingSafe */
export const mockShippingService = {
  quoteShipping: quoteSimulatedShipping,
  isJ3CepEligible,
  qualifiesForFreeShipping,
  origin: simulatedShippingProvider.origin,
};

export {
  quoteSimulatedShipping as quoteShipping,
  isJ3CepEligible,
  qualifiesForFreeShipping,
};
export type { ShippingQuoteInput };
