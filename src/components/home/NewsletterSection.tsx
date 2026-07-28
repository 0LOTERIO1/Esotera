"use client";

import { useState } from "react";
import { FormField, inputClassName } from "@/components/ui/FormField";
import { Button } from "@/components/ui/Button";
import { newsletterApi } from "@/services/api/newsletterApi";
import { ApiError } from "@/services/api/apiClient";
import { validateEmail } from "@/utils/validation";

export function NewsletterSection() {
  const [email, setEmail] = useState("");
  const [consent, setConsent] = useState(false);
  const [loading, setLoading] = useState(false);
  const [success, setSuccess] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setSuccess(null);
    setError(null);

    if (!validateEmail(email)) {
      setError("Informe um e-mail válido.");
      return;
    }
    if (!consent) {
      setError("Confirme o consentimento para receber comunicações.");
      return;
    }

    setLoading(true);
    try {
      const res = await newsletterApi.subscribe(email.trim(), true);
      setSuccess(res.message);
      setEmail("");
      setConsent(false);
    } catch (err) {
      setError(
        err instanceof ApiError
          ? err.userMessage
          : err instanceof Error
            ? err.message
            : "Não foi possível concluir a inscrição.",
      );
    } finally {
      setLoading(false);
    }
  }

  return (
    <section className="mx-auto max-w-6xl px-4 py-10 sm:px-6">
      <div className="rounded-xl border border-esotera-border bg-esotera-surface px-5 py-8 shadow-sm sm:px-8">
        <h2 className="font-serif text-2xl text-esotera-secondary sm:text-3xl">
          Newsletter
        </h2>
        <p className="mt-2 max-w-xl text-sm text-esotera-muted">
          Receba novidades, lançamentos e conteúdos da Esotera.
        </p>
        <form
          className="mt-5 flex max-w-lg flex-col gap-3"
          onSubmit={(e) => void handleSubmit(e)}
          noValidate
        >
          <FormField label="Seu e-mail" id="newsletter-email" required>
            <input
              id="newsletter-email"
              type="email"
              className={inputClassName}
              placeholder="voce@email.com"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              disabled={loading}
              autoComplete="email"
            />
          </FormField>
          <label className="flex items-start gap-2 text-sm text-esotera-muted">
            <input
              type="checkbox"
              className="mt-1"
              checked={consent}
              onChange={(e) => setConsent(e.target.checked)}
              disabled={loading}
            />
            <span>
              Concordo em receber comunicações da Esotera por e-mail. Posso
              cancelar a inscrição a qualquer momento.
            </span>
          </label>
          <Button type="submit" disabled={loading} className="w-full sm:w-auto">
            {loading ? "Enviando…" : "Quero receber"}
          </Button>
          {success ? (
            <p role="status" className="text-sm text-esotera-success">
              {success}
            </p>
          ) : null}
          {error ? (
            <p role="alert" className="text-sm text-esotera-error">
              {error}
            </p>
          ) : null}
        </form>
      </div>
    </section>
  );
}
