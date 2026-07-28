"use client";

import { useEffect, useRef, useState } from "react";
import {
  CepLookupError,
  lookupCep,
  type CepLookupResult,
} from "@/services/cep/viacepService";
import { onlyDigits, validateCep } from "@/utils/validation";

export type CepAutofillStatus =
  | "idle"
  | "loading"
  | "ok"
  | "not_found"
  | "error";

type UseCepAutofillOptions = {
  cep: string;
  /** Quando false, não consulta (ex.: formulário fechado ou CEP ainda não alterado na edição) */
  enabled?: boolean;
  /**
   * Chamado somente com resposta válida do ViaCEP.
   * Não limpa o formulário em caso de erro — o endereço atual permanece até sucesso.
   */
  onResolved: (result: CepLookupResult) => void;
  /** Chamado quando o CEP não existe (sem apagar campos automaticamente) */
  onNotFound?: () => void;
};

/**
 * Dispara consulta automática ao ViaCEP quando o CEP completa 8 dígitos.
 * Reutilizado por AddressForm, cadastro e checkout.
 */
export function useCepAutofill({
  cep,
  enabled = true,
  onResolved,
  onNotFound,
}: UseCepAutofillOptions) {
  const [status, setStatus] = useState<CepAutofillStatus>("idle");
  const [message, setMessage] = useState<string | null>(null);
  const onResolvedRef = useRef(onResolved);
  const onNotFoundRef = useRef(onNotFound);
  const lastSuccessRef = useRef<string | null>(null);
  const lastNotFoundRef = useRef<string | null>(null);

  useEffect(() => {
    onResolvedRef.current = onResolved;
    onNotFoundRef.current = onNotFound;
  }, [onResolved, onNotFound]);

  useEffect(() => {
    if (!enabled) return;

    const digits = onlyDigits(cep);

    if (digits.length < 8) {
      lastSuccessRef.current = null;
      lastNotFoundRef.current = null;
      const idleTimer = window.setTimeout(() => {
        setStatus("idle");
        setMessage(null);
      }, 0);
      return () => window.clearTimeout(idleTimer);
    }

    if (!validateCep(digits)) return;

    // Evita consultas repetidas para o mesmo CEP já resolvido ou inexistente
    if (lastSuccessRef.current === digits || lastNotFoundRef.current === digits) {
      return;
    }

    const controller = new AbortController();
    let cancelled = false;

    const timer = window.setTimeout(() => {
      void (async () => {
        if (cancelled) return;
        setStatus("loading");
        setMessage("Buscando CEP...");

        try {
          const result = await lookupCep(digits, controller.signal);
          if (cancelled) return;
          lastSuccessRef.current = digits;
          lastNotFoundRef.current = null;
          setStatus("ok");
          setMessage(
            result.neighborhood
              ? null
              : "Bairro não retornado para este CEP. Preencha manualmente.",
          );
          onResolvedRef.current(result);
        } catch (error) {
          if (cancelled) return;
          if (error instanceof DOMException && error.name === "AbortError") {
            return;
          }

          if (error instanceof CepLookupError && error.code === "not_found") {
            lastNotFoundRef.current = digits;
            setStatus("not_found");
            setMessage(error.message);
            onNotFoundRef.current?.();
            return;
          }

          // Rede/timeout/indisponível: não marca como consultado — permite nova tentativa
          setStatus("error");
          setMessage(
            error instanceof Error
              ? error.message
              : "Não foi possível consultar o CEP. Tente novamente.",
          );
        }
      })();
    }, 0);

    return () => {
      cancelled = true;
      controller.abort();
      window.clearTimeout(timer);
    };
  }, [cep, enabled]);

  /**
   * Impede salvar CEP inexistente.
   * Falha de rede não bloqueia se o usuário preencheu os campos manualmente.
   */
  function assertCepReadyForSubmit(): string | null {
    const digits = onlyDigits(cep);
    if (!validateCep(digits)) {
      return "CEP deve conter exatamente 8 dígitos.";
    }
    if (status === "loading") {
      return "Aguarde a consulta do CEP.";
    }
    if (status === "not_found") {
      return "CEP não encontrado. Verifique o número informado.";
    }
    return null;
  }

  function resetLookup() {
    lastSuccessRef.current = null;
    lastNotFoundRef.current = null;
    setStatus("idle");
    setMessage(null);
  }

  return {
    status,
    message,
    lookingUp: status === "loading",
    assertCepReadyForSubmit,
    resetLookup,
  };
}
