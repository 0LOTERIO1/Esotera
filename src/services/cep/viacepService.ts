import { onlyDigits, validateCep } from "@/utils/validation";

/** Resposta bruta do ViaCEP */
type ViaCepResponse = {
  cep?: string;
  logradouro?: string;
  complemento?: string;
  bairro?: string;
  localidade?: string;
  uf?: string;
  ibge?: string;
  erro?: boolean;
};

/**
 * Campos de endereço preenchíveis pelo ViaCEP.
 * Mapeamento exato:
 * - logradouro → street
 * - bairro → neighborhood (nunca localidade/IBGE)
 * - localidade → city
 * - uf → state
 */
export type CepLookupResult = {
  street: string;
  neighborhood: string;
  city: string;
  state: string;
};

export class CepLookupError extends Error {
  constructor(
    message: string,
    public code: "invalid" | "not_found" | "network" | "unavailable",
  ) {
    super(message);
    this.name = "CepLookupError";
  }
}

/**
 * Consulta ViaCEP: GET https://viacep.com.br/ws/{cep}/json/
 * Serviço reutilizável — não duplicar a lógica nos formulários.
 */
export async function lookupCep(
  cep: string,
  signal?: AbortSignal,
): Promise<CepLookupResult> {
  const digits = onlyDigits(cep);
  if (!validateCep(digits)) {
    throw new CepLookupError(
      "CEP deve conter exatamente 8 dígitos.",
      "invalid",
    );
  }

  let response: Response;
  try {
    response = await fetch(`https://viacep.com.br/ws/${digits}/json/`, {
      method: "GET",
      signal,
      headers: { Accept: "application/json" },
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === "AbortError") {
      throw error;
    }
    throw new CepLookupError(
      "Não foi possível consultar o CEP. Tente novamente.",
      "network",
    );
  }

  if (!response.ok) {
    throw new CepLookupError(
      "Serviço de CEP indisponível no momento.",
      "unavailable",
    );
  }

  const data = (await response.json()) as ViaCepResponse;

  if (data.erro === true) {
    throw new CepLookupError(
      "CEP não encontrado. Verifique o número informado.",
      "not_found",
    );
  }

  const street = (data.logradouro ?? "").trim();
  // Obrigatório: bairro vem SOMENTE de data.bairro — nunca de localidade/ibge
  const neighborhood = (data.bairro ?? "").trim();
  const city = (data.localidade ?? "").trim();
  const state = (data.uf ?? "").trim().toUpperCase();

  if (!city || !state) {
    throw new CepLookupError(
      "CEP não encontrado. Verifique o número informado.",
      "not_found",
    );
  }

  return {
    street,
    neighborhood,
    city,
    state,
  };
}
