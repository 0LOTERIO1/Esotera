/**
 * Fachada de frete do frontend.
 *
 * Hoje: sempre usa cotação simulada (valores fixos por UF + J3 por faixas CEP).
 * Futuro: quando Melhor Envio / J3 estiverem configurados e validados,
 * tentar transportadora primeiro; em falha, NÃO inventar preço —
 * retornar erro seguro e opções vazias (checkout não quebra).
 *
 * Credenciais no .env.example (MELHOR_ENVIO_*, J3_*) ainda NÃO estão integradas.
 */
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

const CARRIER_ERROR =
  "Não foi possível calcular o frete com a transportadora. Tente novamente ou escolha outro endereço.";

function carriersConfigured(): boolean {
  // Preparação apenas — sem inventar integração.
  // Ativar somente após o cliente fornecer credenciais + regras oficiais.
  const melhor =
    Boolean(process.env.NEXT_PUBLIC_MELHOR_ENVIO_ENABLED === "true") &&
    Boolean(process.env.NEXT_PUBLIC_MELHOR_ENVIO_READY === "true");
  const j3 =
    Boolean(process.env.NEXT_PUBLIC_J3_ENABLED === "true") &&
    Boolean(process.env.NEXT_PUBLIC_J3_READY === "true");
  return melhor || j3;
}

async function tryCarrierQuote(): Promise<ShippingQuoteResult | null> {
  if (!carriersConfigured()) return null;
  // Placeholder: integração real ainda não implementada (faltam credenciais/API).
  // Retornar null força o fallback simulado documentado.
  return null;
}

export async function quoteShippingSafe(
  input: ShippingQuoteInput,
): Promise<ShippingQuoteResult> {
  try {
    const carrier = await tryCarrierQuote();
    if (carrier) {
      if (!carrier.ok || carrier.options.length === 0) {
        // Falha de transportadora: não inventar valores; checkout continua com erro claro.
        return {
          ok: false,
          options: [],
          source: "carrier",
          errorMessage: carrier.errorMessage ?? CARRIER_ERROR,
        };
      }
      return carrier;
    }

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
      errorMessage: "Não foi possível calcular o frete. Tente novamente.",
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
