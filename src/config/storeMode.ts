/**
 * Modo da loja na experiência pública.
 * - testing: permite fluxo de pedido com pagamento ainda não integrado (uso interno/homologação)
 * - production: bloqueia finalização até Mercado Pago estar integrado
 *
 * Defina NEXT_PUBLIC_STORE_MODE=production na Vercel quando a loja estiver aberta ao público.
 */
export type StoreMode = "testing" | "production";

export function getStoreMode(): StoreMode {
  const mode = process.env.NEXT_PUBLIC_STORE_MODE?.toLowerCase();
  return mode === "production" ? "production" : "testing";
}

export function isProductionStore(): boolean {
  return getStoreMode() === "production";
}

export function isTestingStore(): boolean {
  return getStoreMode() === "testing";
}

/** Pagamento real (Mercado Pago) ainda não integrado nesta fase. */
export function isRealPaymentEnabled(): boolean {
  return false;
}

/**
 * Em production, sem MP, o cliente não deve concluir pedido como se estivesse pago.
 * Em testing, o fluxo atual de pedido permanece disponível para homologação.
 */
export function canCompleteCheckoutWithoutRealPayment(): boolean {
  return isTestingStore();
}
