import type { ShippingOption, StoreSettings } from "@/types";
import type { ShippingQuoteInput } from "./simulatedShippingProvider";

/**
 * Contrato estável para cotação de frete.
 * Implementações futuras (Melhor Envio / J3) devem cumprir esta interface
 * e falhar de forma segura — nunca inventar preços em erro de transportadora.
 */
export interface IShippingQuoteService {
  /**
   * Retorna opções disponíveis. Lista vazia = nenhuma modalidade (não é preço zero).
   * Deve lançar apenas erros inesperados; preferir resultado com `ok: false`.
   */
  quote(input: ShippingQuoteInput): Promise<ShippingQuoteResult>;
}

export type ShippingQuoteSource = "simulated" | "carrier";

export type ShippingQuoteResult = {
  ok: boolean;
  options: ShippingOption[];
  /** Origem usada na cotação (hoje sempre simulated até credenciais oficiais). */
  source: ShippingQuoteSource;
  /** Mensagem segura para UI quando não há opções / falha. */
  errorMessage?: string;
  settingsUsed?: StoreSettings;
};

export type { ShippingQuoteInput };
