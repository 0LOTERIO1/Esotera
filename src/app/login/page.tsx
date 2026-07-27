"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { Suspense, useState } from "react";
import { FormField, inputClassName } from "@/components/ui/FormField";
import { Button } from "@/components/ui/Button";
import { useAuthStore } from "@/stores/authStore";
import { useToastStore } from "@/stores/toastStore";
import { DEMO_PASSWORD_HINT } from "@/config/demoUsers";

function LoginForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const returnUrl = searchParams.get("returnUrl") || "/minha-conta";
  const login = useAuthStore((s) => s.login);
  const loginDemoCustomer = useAuthStore((s) => s.loginDemoCustomer);
  const loginDemoAdmin = useAuthStore((s) => s.loginDemoAdmin);
  const push = useToastStore((s) => s.push);

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [remember, setRemember] = useState(true);
  const [error, setError] = useState<string | null>(null);

  function redirectAfterLogin(role: string) {
    if (returnUrl) {
      router.push(returnUrl);
      return;
    }
    router.push(role === "admin" ? "/admin" : "/minha-conta");
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      const user = login(email, password, remember);
      push("success", `Olá, ${user.name.split(" ")[0]}!`);
      redirectAfterLogin(user.role);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Falha no login.");
    }
  }

  return (
    <div className="mx-auto max-w-md px-4 py-12 sm:px-6">
      <h1 className="font-serif text-4xl text-esotera-white">Entrar</h1>
      <p className="mt-2 text-sm text-esotera-muted">
        Login simulado para o protótipo. Senha demo: {DEMO_PASSWORD_HINT}
      </p>

      <form onSubmit={handleSubmit} className="mt-8 space-y-4">
        <FormField label="E-mail" id="email" required error={error ?? undefined}>
          <input
            id="email"
            type="email"
            required
            className={inputClassName}
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            autoComplete="email"
          />
        </FormField>
        <FormField label="Senha" id="password" required>
          <input
            id="password"
            type="password"
            required
            className={inputClassName}
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete="current-password"
          />
        </FormField>
        <label className="flex items-center gap-2 text-sm text-esotera-muted">
          <input
            type="checkbox"
            checked={remember}
            onChange={(e) => setRemember(e.target.checked)}
          />
          Lembrar de mim
        </label>
        <Button type="submit" className="w-full">
          Entrar
        </Button>
      </form>

      <p className="mt-4 text-center text-sm text-esotera-muted">
        <span className="cursor-default underline decoration-dotted">
          Recuperar senha (visual)
        </span>
      </p>

      <div className="mt-6 space-y-2">
        <Button
          type="button"
          variant="secondary"
          className="w-full"
          onClick={() => {
            const user = loginDemoCustomer();
            push("success", "Entrou como cliente de demonstração.");
            redirectAfterLogin(user.role);
          }}
        >
          Entrar como usuário de demonstração
        </Button>
        <Button
          type="button"
          variant="secondary"
          className="w-full"
          onClick={() => {
            loginDemoAdmin();
            push("success", "Entrou como administrador de demonstração.");
            router.push("/admin");
          }}
        >
          Entrar como administrador de demonstração
        </Button>
      </div>

      <p className="mt-6 text-center text-sm text-esotera-muted">
        Não tem conta?{" "}
        <Link href="/cadastro" className="text-esotera-gold hover:underline">
          Cadastre-se
        </Link>
      </p>
    </div>
  );
}

export default function LoginPage() {
  return (
    <Suspense
      fallback={
        <div className="px-4 py-12 text-center text-esotera-muted">Carregando…</div>
      }
    >
      <LoginForm />
    </Suspense>
  );
}
