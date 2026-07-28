"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { Suspense, useState } from "react";
import { FormField, inputClassName } from "@/components/ui/FormField";
import { Button } from "@/components/ui/Button";
import { authApi, toAuthUserMessage } from "@/services/api/authApi";

function ResetPasswordForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const token = searchParams.get("token") || "";
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    if (!token) {
      setError("Link inválido. Solicite uma nova recuperação de senha.");
      return;
    }
    if (password.length < 6) {
      setError("A senha deve ter ao menos 6 caracteres.");
      return;
    }
    if (password !== confirm) {
      setError("As senhas não coincidem.");
      return;
    }
    setLoading(true);
    try {
      const res = await authApi.resetPassword({
        token,
        newPassword: password,
        confirmPassword: confirm,
      });
      setSuccess(res.message);
      setTimeout(() => router.push("/login"), 2000);
    } catch (err) {
      setError(toAuthUserMessage(err));
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="mx-auto max-w-md px-4 py-12 sm:px-6">
      <h1 className="font-serif text-4xl text-esotera-secondary">
        Nova senha
      </h1>
      <p className="mt-2 text-sm text-esotera-muted">
        Defina uma nova senha para sua conta.
      </p>
      <form onSubmit={(e) => void handleSubmit(e)} className="mt-8 space-y-4">
        <FormField label="Nova senha" id="password" required>
          <input
            id="password"
            type="password"
            className={inputClassName}
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete="new-password"
            disabled={loading}
          />
        </FormField>
        <FormField label="Confirmar senha" id="confirm" required>
          <input
            id="confirm"
            type="password"
            className={inputClassName}
            value={confirm}
            onChange={(e) => setConfirm(e.target.value)}
            autoComplete="new-password"
            disabled={loading}
          />
        </FormField>
        <Button type="submit" className="w-full" disabled={loading || !token}>
          {loading ? "Salvando…" : "Redefinir senha"}
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
      <p className="mt-6 text-center text-sm text-esotera-muted">
        <Link href="/login" className="text-esotera-primary hover:underline">
          Ir para o login
        </Link>
      </p>
    </div>
  );
}

export default function ResetPasswordPage() {
  return (
    <Suspense
      fallback={
        <div className="px-4 py-12 text-center text-esotera-muted">Carregando…</div>
      }
    >
      <ResetPasswordForm />
    </Suspense>
  );
}
