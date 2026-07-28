/**
 * Chave de idempotência por tentativa de compra (Fase 2C).
 * Reutilizar no retry; gerar nova só após sucesso ou alteração material.
 */

export type OrderAttemptFingerprintInput = {
  addressId: string;
  items: { productId: string; quantity: number; variation?: string }[];
  shippingMethodId: string;
  paymentMethod: string;
  installments?: number;
  couponCode?: string;
};

export function createIdempotencyKey(): string {
  return crypto.randomUUID();
}

export function fingerprintOrderAttempt(
  input: OrderAttemptFingerprintInput,
): string {
  const items = [...input.items]
    .map((i) => ({
      productId: i.productId,
      quantity: i.quantity,
      variation: i.variation?.trim() || null,
    }))
    .sort((a, b) => {
      const byId = a.productId.localeCompare(b.productId);
      if (byId !== 0) return byId;
      return (a.variation ?? "").localeCompare(b.variation ?? "");
    });

  return JSON.stringify({
    addressId: input.addressId,
    items,
    shippingMethodId: input.shippingMethodId,
    paymentMethod: input.paymentMethod,
    installments: input.installments ?? null,
    couponCode: input.couponCode?.trim().toUpperCase() || null,
  });
}
