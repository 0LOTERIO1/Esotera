/**
 * Compat: reexporta a fachada e o provedor simulado.
 * Novos imports devem preferir `@/services/shipping/shippingService`.
 */
export {
  mockShippingService,
  quoteShipping,
  quoteShippingSafe,
  shippingService,
  isJ3CepEligible,
  qualifiesForFreeShipping,
  type ShippingQuoteInput,
} from "./shippingService";
