"use client";

import Link from "next/link";
import { useState } from "react";
import { FormField, inputClassName } from "@/components/ui/FormField";
import { Button } from "@/components/ui/Button";
import { authApi, toAuthUserMessage } from "@/services/api/authApi";
import { validateEmail } from "@/utils/validation";

export default function ForgotPasswordPage() {
  const [email, setEmail] = useState("");
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setMessage(null);
    if (!validateEmail(email)) {
      setError("Informe um e-mail válido.");
      return;
    }
    setLoading(true);
    try {
      const res = await authApi.forgotPassword(email.trim());
      setMessage(res.message);
    } catch (err) {
      setError(toAuthUserMessage(err));
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="mx-auto max-w-md px-4 py-12 sm:px-6">
      <h1 className="font-serif text-4xl text-esotera-secondary">
        Recuperar senha
      </h1>
      <p className="mt-2 text-sm text-esotera-muted">
        Informe seu e-mail. Se houver uma conta, enviaremos instruções para
        redefinir a senha.
      </p>
      <form onSubmit={(e) => void handleSubmit(e)} className="mt-8 space-y-4">
        <FormField label="E-mail" id="email" required>
          <input
            id="email"
            type="email"
            className={inputClassName}
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            autoComplete="email"
            disabled={loading}
          />
        </FormField>
        <Button type="submit" className="w-full" disabled={loading}>
          {loading ? "Enviando…" : "Enviar instruções"}
        </Button>
        {message ? (
          <p role="status" className="text-sm text-esotera-success">
            {message}
          </p>
        ) : null}
        {error ? (
          <p role="alert" className="text-sm text-esotera-error">
            {error}
          </p>
        ) : null}
      </form>
      <p className="mt-6 text-center text-sm text-esotera-muted">
        <Link href="/login" className="text-esotera-primary hover:underline">
          Voltar ao login
        </Link>
      </p>
    </div>
  );
}
