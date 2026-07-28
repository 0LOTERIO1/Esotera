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
   * Chamado com o resultado do ViaCEP.
   * street/neighborhood podem vir vazios — o usuário completa manualmente.
   */
  onResolved: (result: CepLookupResult) => void;
  /** Chamado quando o CEP não existe (ou falha definitiva de lookup) */
  onNotFound?: () => void;
};

/**
 * Dispara consulta automática ao ViaCEP quando o CEP completa 8 dígitos.
 * Centraliza o fluxo para AddressSection, cadastro e futuros formulários.
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
  const lastQueriedRef = useRef<string | null>(null);

  useEffect(() => {
    onResolvedRef.current = onResolved;
    onNotFoundRef.current = onNotFound;
  }, [onResolved, onNotFound]);

  useEffect(() => {
    if (!enabled) return;

    const digits = onlyDigits(cep);

    if (digits.length < 8) {
      lastQueriedRef.current = null;
      void Promise.resolve().then(() => {
        setStatus("idle");
        setMessage(null);
      });
      return;
    }

    if (!validateCep(digits)) return;
    if (lastQueriedRef.current === digits) return;

    const controller = new AbortController();
    let cancelled = false;

    void Promise.resolve().then(async () => {
      if (cancelled) return;
      setStatus("loading");
      setMessage(null);

      try {
        const result = await lookupCep(digits, controller.signal);
        if (cancelled) return;
        lastQueriedRef.current = digits;
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
        lastQueriedRef.current = digits;
        if (error instanceof CepLookupError && error.code === "not_found") {
          setStatus("not_found");
          setMessage(error.message);
          onNotFoundRef.current?.();
          return;
        }
        setStatus("error");
        setMessage(
          error instanceof Error
            ? error.message
            : "Não foi possível consultar o CEP.",
        );
      }
    });

    return () => {
      cancelled = true;
      controller.abort();
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
    lastQueriedRef.current = null;
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
