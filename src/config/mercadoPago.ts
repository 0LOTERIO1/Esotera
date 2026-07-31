import { isApiMode } from "@/config/dataMode";

/**
 * Pagamento real (Mercado Pago) só quando:
 * - modo API
 * - Public Key presente (NEXT_PUBLIC_MERCADO_PAGO_PUBLIC_KEY)
 *
 * Sem a Public Key na Vercel, a loja pública NÃO ativa cobrança real.
 * Access Token permanece somente no backend (MERCADO_PAGO_ACCESS_TOKEN).
 *
 * Ambiente Test/Production é definido no backend (`MercadoPago__Environment`).
 * Opcionalmente o front pode espelhar com NEXT_PUBLIC_MERCADO_PAGO_ENVIRONMENT.
 */
export function isRealPaymentEnabled(): boolean {
  if (!isApiMode()) return false;
  const key = process.env.NEXT_PUBLIC_MERCADO_PAGO_PUBLIC_KEY?.trim();
  return Boolean(key);
}

export function getMercadoPagoPublicKey(): string | null {
  const key = process.env.NEXT_PUBLIC_MERCADO_PAGO_PUBLIC_KEY?.trim();
  return key || null;
}

/** Hint estático do ambiente (não substitui GET /api/payments/config). */
export function getMercadoPagoEnvironmentHint(): "Test" | "Production" | null {
  const raw = process.env.NEXT_PUBLIC_MERCADO_PAGO_ENVIRONMENT?.trim();
  if (!raw) return null;
  if (/^prod(uction)?$/i.test(raw) || /^live$/i.test(raw)) return "Production";
  return "Test";
}
