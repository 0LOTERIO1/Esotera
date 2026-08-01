import type { StoreSettings } from "@/types";

export const shippingOrigin = {
  cep: "08061-420",
  region: "Ermelino Matarazzo",
  city: "São Paulo",
  state: "SP",
  package: {
    widthCm: 15,
    heightCm: 15,
    lengthCm: 5,
    weightGrams: 400,
  },
} as const;

export const FREE_SHIPPING_STATES = [
  "SP",
  "RJ",
  "MG",
  "ES",
  "PR",
  "SC",
  "RS",
] as const;

export const defaultStoreSettings: StoreSettings = {
  storeName: "Esotera",
  freeShippingMin: 99.9,
  freeShippingStates: [...FREE_SHIPPING_STATES],
  j3Price: 12,
  j3CutoffHour: 12,
  couponDiscount: 5,
  couponMinPurchase: 30,
  // TODO: regra de subsídio de R$ 10 ainda depende de confirmação do cliente
  shippingSubsidy: {
    enabled: false,
    amount: 10,
  },
};

/**
 * TODO: substituir por cobertura oficial J3 quando fornecida pelo cliente.
 * Faixas simuladas apenas para demonstração — NÃO são cobertura oficial.
 *
 * Para ativar Melhor Envio / J3 reais (futuro):
 * - Backend: MELHOR_ENVIO_ENABLED + CLIENT_ID/SECRET; J3_ENABLED + API_URL/TOKEN
 * - Cobertura oficial de CEPs J3, tabelas/serviços Melhor Envio, origem de envio
 * - Até lá, checkout e pedidos usam cotação simulada (valores fixos por UF)
 */
export const simulatedJ3CepRanges: Array<{ start: string; end: string }> = [
  { start: "01000000", end: "05999999" },
  { start: "08000000", end: "08499999" },
  { start: "04000000", end: "04999999" },
];
