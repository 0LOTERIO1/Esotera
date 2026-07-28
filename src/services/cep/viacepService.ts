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
  erro?: boolean | string;
};

/**
 * Campos de endereço preenchíveis pelo ViaCEP.
 * - logradouro → street
 * - bairro → neighborhood
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
    public code: "invalid" | "not_found" | "network" | "unavailable" | "timeout",
  ) {
    super(message);
    this.name = "CepLookupError";
  }
}

const DEFAULT_TIMEOUT_MS = 8_000;

/**
 * Consulta ViaCEP: GET https://viacep.com.br/ws/{cep}/json/
 * Serviço único — formulários devem usar este helper via useCepAutofill.
 */
export async function lookupCep(
  cep: string,
  signal?: AbortSignal,
  timeoutMs: number = DEFAULT_TIMEOUT_MS,
): Promise<CepLookupResult> {
  const digits = onlyDigits(cep);
  if (!validateCep(digits)) {
    throw new CepLookupError(
      "CEP deve conter exatamente 8 dígitos.",
      "invalid",
    );
  }

  const timeoutController = new AbortController();
  const timeoutId = setTimeout(() => timeoutController.abort(), timeoutMs);

  const onOuterAbort = () => timeoutController.abort();
  if (signal) {
    if (signal.aborted) {
      clearTimeout(timeoutId);
      throw new DOMException("Aborted", "AbortError");
    }
    signal.addEventListener("abort", onOuterAbort, { once: true });
  }

  let response: Response;
  try {
    response = await fetch(`https://viacep.com.br/ws/${digits}/json/`, {
      method: "GET",
      signal: timeoutController.signal,
      headers: { Accept: "application/json" },
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === "AbortError") {
      if (signal?.aborted) throw error;
      throw new CepLookupError(
        "A consulta do CEP demorou demais. Tente novamente.",
        "timeout",
      );
    }
    throw new CepLookupError(
      "Não foi possível consultar o CEP. Verifique sua conexão e tente novamente.",
      "network",
    );
  } finally {
    clearTimeout(timeoutId);
    signal?.removeEventListener("abort", onOuterAbort);
  }

  if (!response.ok) {
    throw new CepLookupError(
      "Serviço de CEP indisponível no momento. Tente novamente em instantes.",
      "unavailable",
    );
  }

  let data: ViaCepResponse;
  try {
    data = (await response.json()) as ViaCepResponse;
  } catch {
    throw new CepLookupError(
      "Não foi possível interpretar a resposta do CEP. Tente novamente.",
      "unavailable",
    );
  }

  if (data.erro === true || data.erro === "true") {
    throw new CepLookupError(
      "CEP não encontrado. Verifique o número informado.",
      "not_found",
    );
  }

  const street = (data.logradouro ?? "").trim();
  const neighborhood = (data.bairro ?? "").trim();
  const city = (data.localidade ?? "").trim();
  const state = (data.uf ?? "").trim().toUpperCase();

  if (!city || state.length !== 2) {
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
