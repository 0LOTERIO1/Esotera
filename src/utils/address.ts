import { brazilianStates } from "@/data/brazilianStates";
import { onlyDigits, validateCep } from "@/utils/validation";
import type { AddressInput } from "@/types";

export type AddressFormErrors = Partial<
  Record<
    "cep" | "street" | "number" | "complement" | "neighborhood" | "city" | "state",
    string
  >
>;

/** CEP exibido na UI: 00000-000 */
export function formatCepDisplay(cep: string): string {
  const d = onlyDigits(cep).slice(0, 8);
  if (d.length <= 5) return d;
  return `${d.slice(0, 5)}-${d.slice(5)}`;
}

/** Linhas de exibição padronizadas do endereço */
export function formatAddressLines(address: {
  street: string;
  number: string;
  complement?: string;
  neighborhood: string;
  city: string;
  state: string;
  cep: string;
}): { line1: string; line2: string; line3: string } {
  const complement = address.complement?.trim();
  return {
    line1: `${address.street}, ${address.number}${
      complement ? ` — ${complement}` : ""
    }`,
    line2: `${address.neighborhood} · ${address.city}/${address.state}`,
    line3: `CEP ${formatCepDisplay(address.cep)}`,
  };
}

export function validateBrazilianState(state: string): boolean {
  const uf = state.trim().toUpperCase();
  return brazilianStates.some((s) => s.uf === uf);
}

export function validateAddressInput(input: {
  cep: string;
  street: string;
  number: string;
  neighborhood: string;
  city: string;
  state: string;
}): AddressFormErrors {
  const errors: AddressFormErrors = {};
  if (!validateCep(input.cep)) {
    errors.cep = "CEP deve conter exatamente 8 dígitos.";
  }
  if (!input.street.trim()) errors.street = "Informe a rua.";
  if (!input.number.trim()) errors.number = "Informe o número.";
  if (!input.neighborhood.trim()) errors.neighborhood = "Informe o bairro.";
  if (!input.city.trim()) errors.city = "Informe a cidade.";
  if (!validateBrazilianState(input.state)) {
    errors.state = "Selecione um estado válido (UF com 2 letras).";
  }
  return errors;
}

/** Normaliza payload para a API: CEP só dígitos, UF maiúscula, sem userId */
export function normalizeAddressPayload(input: AddressInput): AddressInput {
  const cep = onlyDigits(input.cep);
  const complement = input.complement?.trim();
  return {
    cep,
    street: input.street.trim(),
    number: input.number.trim(),
    complement: complement ? complement : undefined,
    neighborhood: input.neighborhood.trim(),
    city: input.city.trim(),
    state: input.state.trim().toUpperCase(),
    isPrimary: Boolean(input.isPrimary),
  };
}
