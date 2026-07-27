import { onlyDigits } from "@/utils/validation";
import {
  defaultStoreSettings,
  simulatedJ3CepRanges,
  shippingOrigin,
} from "@/config/shipping";
import type { ShippingOption, StoreSettings } from "@/types";

function isWeekday(date: Date): boolean {
  const day = date.getDay();
  return day >= 1 && day <= 5;
}

function nextBusinessDay(from: Date): Date {
  const d = new Date(from);
  do {
    d.setDate(d.getDate() + 1);
  } while (!isWeekday(d));
  return d;
}

function getSaoPauloNow(): Date {
  const parts = new Intl.DateTimeFormat("en-US", {
    timeZone: "America/Sao_Paulo",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
  }).formatToParts(new Date());

  const get = (type: string) =>
    Number(parts.find((p) => p.type === type)?.value ?? 0);

  return new Date(
    get("year"),
    get("month") - 1,
    get("day"),
    get("hour"),
    get("minute"),
  );
}

export function isJ3CepEligible(cep: string): boolean {
  const digits = onlyDigits(cep);
  if (digits.length !== 8) return false;
  // TODO: substituir simulatedJ3CepRanges pela cobertura oficial J3
  return simulatedJ3CepRanges.some(
    (range) => digits >= range.start && digits <= range.end,
  );
}

export function qualifiesForFreeShipping(
  productsTotalAfterDiscount: number,
  state: string,
  settings: StoreSettings = defaultStoreSettings,
): boolean {
  return (
    productsTotalAfterDiscount >= settings.freeShippingMin &&
    settings.freeShippingStates.includes(state.toUpperCase())
  );
}

function applySubsidy(
  price: number,
  settings: StoreSettings,
): { price: number; originalPrice: number } {
  if (!settings.shippingSubsidy.enabled || price === 0) {
    return { price, originalPrice: price };
  }
  return {
    originalPrice: price,
    price: Math.max(0, price - settings.shippingSubsidy.amount),
  };
}

function melhorEnvioPrices(state: string): {
  economico: { price: number; days: string };
  expresso: { price: number; days: string };
} {
  const uf = state.toUpperCase();
  const southEast = ["SP", "RJ", "MG", "ES"];
  const south = ["PR", "SC", "RS"];

  if (uf === "SP") {
    return {
      economico: { price: 18.9, days: "3 a 5 dias úteis" },
      expresso: { price: 28.9, days: "1 a 2 dias úteis" },
    };
  }
  if (southEast.includes(uf)) {
    return {
      economico: { price: 24.9, days: "4 a 7 dias úteis" },
      expresso: { price: 36.9, days: "2 a 3 dias úteis" },
    };
  }
  if (south.includes(uf)) {
    return {
      economico: { price: 29.9, days: "5 a 8 dias úteis" },
      expresso: { price: 42.9, days: "3 a 4 dias úteis" },
    };
  }
  return {
    economico: { price: 39.9, days: "8 a 12 dias úteis" },
    expresso: { price: 59.9, days: "4 a 6 dias úteis" },
  };
}

export type ShippingQuoteInput = {
  cep: string;
  state: string;
  productsTotalAfterDiscount: number;
  settings?: StoreSettings;
};

export function quoteShipping(input: ShippingQuoteInput): ShippingOption[] {
  const settings = input.settings ?? defaultStoreSettings;
  const free = qualifiesForFreeShipping(
    input.productsTotalAfterDiscount,
    input.state,
    settings,
  );
  const options: ShippingOption[] = [];
  const now = getSaoPauloNow();

  if (isJ3CepEligible(input.cep) && isWeekday(now)) {
    const beforeCutoff = now.getHours() < settings.j3CutoffHour;
    const deliveryLabel = beforeCutoff
      ? "Hoje (mesmo dia)"
      : `Próximo dia útil (${nextBusinessDay(now).toLocaleDateString("pt-BR")})`;

    const base = free ? 0 : settings.j3Price;
    const priced = applySubsidy(base, settings);

    options.push({
      id: "j3",
      provider: "J3",
      name: "Entrega J3 (simulada)",
      price: priced.price,
      originalPrice: free ? settings.j3Price : priced.originalPrice,
      estimatedDays: deliveryLabel,
      description:
        "Modalidade simulada para CEPs elegíveis de São Paulo. Cobertura oficial ainda não configurada.",
      isSameDay: beforeCutoff,
    });
  }

  const me = melhorEnvioPrices(input.state);

  const ecoBase = free ? 0 : me.economico.price;
  const eco = applySubsidy(ecoBase, settings);
  options.push({
    id: "melhor_economico",
    provider: "Melhor Envio",
    name: "Econômico",
    price: eco.price,
    originalPrice: free ? me.economico.price : eco.originalPrice,
    estimatedDays: me.economico.days,
    description: `Simulação Melhor Envio a partir de ${shippingOrigin.city}.`,
  });

  const expBase = free ? 0 : me.expresso.price;
  const exp = applySubsidy(expBase, settings);
  options.push({
    id: "melhor_expresso",
    provider: "Melhor Envio",
    name: "Expresso",
    price: exp.price,
    originalPrice: free ? me.expresso.price : exp.originalPrice,
    estimatedDays: me.expresso.days,
    description: `Simulação Melhor Envio expressa a partir de ${shippingOrigin.city}.`,
  });

  return options;
}

export const mockShippingService = {
  quoteShipping,
  isJ3CepEligible,
  qualifiesForFreeShipping,
  origin: shippingOrigin,
};
