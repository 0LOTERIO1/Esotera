import { isApiMode } from "@/config/dataMode";
import { isRealPaymentEnabled as mpEnabled } from "@/config/mercadoPago";

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

/** Pagamento real via Mercado Pago Brick (requer Public Key + modo API). */
export function isRealPaymentEnabled(): boolean {
  return mpEnabled();
}

/**
 * Em production, sem MP, o cliente não deve concluir pedido como se estivesse pago.
 * Em testing / mock, o fluxo de pedido permanece disponível para homologação.
 * Em API + testing sem Public Key: pedido awaiting_payment sem cobrança real.
 */
export function canCompleteCheckoutWithoutRealPayment(): boolean {
  if (isRealPaymentEnabled()) return true;
  if (!isApiMode()) return isTestingStore();
  // API sem Public Key: ainda permite criar pedido (awaiting), mas sem Brick.
  return isTestingStore();
}
