import { isApiMode } from "@/config/dataMode";

/**
 * Pagamento real (Mercado Pago) só quando:
 * - modo API
 * - Public Key presente (NEXT_PUBLIC_MERCADO_PAGO_PUBLIC_KEY)
 *
 * Sem a Public Key na Vercel, a loja pública NÃO ativa cobrança real.
 * Access Token permanece somente no backend (MERCADO_PAGO_ACCESS_TOKEN).
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
