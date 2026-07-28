"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { Suspense, useCallback, useState } from "react";
import { FormField, inputClassName } from "@/components/ui/FormField";
import { Button } from "@/components/ui/Button";
import { brazilianStates } from "@/data/brazilianStates";
import { maskCep, maskCpf, maskPhone } from "@/utils/masks";
import { validateCep, validateCpf, validateEmail } from "@/utils/validation";
import { useAuthStore } from "@/stores/authStore";
import { useToastStore } from "@/stores/toastStore";
import { useCepAutofill } from "@/hooks/useCepAutofill";
import type { CepLookupResult } from "@/services/cep/viacepService";

type FormState = {
  name: string;
  email: string;
  cpf: string;
  phone: string;
  password: string;
  confirmPassword: string;
  cep: string;
  street: string;
  number: string;
  complement: string;
  neighborhood: string;
  city: string;
  state: string;
  terms: boolean;
  privacy: boolean;
};

type Errors = Partial<Record<keyof FormState, string>>;

const initial: FormState = {
  name: "",
  email: "",
  cpf: "",
  phone: "",
  password: "",
  confirmPassword: "",
  cep: "",
  street: "",
  number: "",
  complement: "",
  neighborhood: "",
  city: "",
  state: "SP",
  terms: false,
  privacy: false,
};

function RegisterForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const returnUrl = searchParams.get("returnUrl") || "/minha-conta";
  const register = useAuthStore((s) => s.register);
  const push = useToastStore((s) => s.push);
  const [form, setForm] = useState<FormState>(initial);
  const [errors, setErrors] = useState<Errors>({});
  const [cepTouched, setCepTouched] = useState(false);

  function set<K extends keyof FormState>(key: K, value: FormState[K]) {
    setForm((prev) => ({ ...prev, [key]: value }));
  }

  const applyCepResult = useCallback((result: CepLookupResult) => {
    setForm((prev) => ({
      ...prev,
      street: result.street,
      neighborhood: result.neighborhood,
      city: result.city,
      state: result.state || prev.state,
    }));
    setErrors((prev) => {
      const next = { ...prev };
      delete next.cep;
      delete next.street;
      delete next.city;
      delete next.state;
      if (!result.neighborhood) {
        next.neighborhood = "Informe o bairro.";
      } else {
        delete next.neighborhood;
      }
      return next;
    });
  }, []);

  const {
    status: cepStatus,
    message: cepMessage,
    lookingUp,
    assertCepReadyForSubmit,
  } = useCepAutofill({
    cep: form.cep,
    enabled: cepTouched,
    onResolved: applyCepResult,
  });

  function validate(): boolean {
    const next: Errors = {};
    if (!form.name.trim()) next.name = "Informe o nome completo.";
    if (!validateEmail(form.email)) next.email = "E-mail inválido.";
    if (!validateCpf(form.cpf)) next.cpf = "CPF inválido.";
    if (form.phone.replace(/\D/g, "").length < 10)
      next.phone = "Telefone inválido.";
    if (form.password.length < 6)
      next.password = "A senha deve ter ao menos 6 caracteres.";
    if (form.password !== form.confirmPassword)
      next.confirmPassword = "As senhas não coincidem.";
    if (!validateCep(form.cep)) next.cep = "CEP inválido.";
    const cepBlock = assertCepReadyForSubmit();
    if (cepBlock) next.cep = cepBlock;
    if (!form.street.trim()) next.street = "Informe o endereço.";
    if (!form.number.trim()) next.number = "Informe o número.";
    if (!form.neighborhood.trim()) next.neighborhood = "Informe o bairro.";
    if (!form.city.trim()) next.city = "Informe a cidade.";
    if (!form.state) next.state = "Selecione o estado.";
    if (!form.terms) next.terms = "Aceite os termos para continuar.";
    if (!form.privacy) next.privacy = "Aceite a política de privacidade.";
    setErrors(next);
    return Object.keys(next).length === 0;
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (lookingUp) return;
    if (!validate()) return;
    try {
      await register({
        name: form.name,
        email: form.email,
        cpf: form.cpf,
        phone: form.phone,
        password: form.password,
        address: {
          cep: form.cep,
          street: form.street,
          number: form.number,
          complement: form.complement || undefined,
          neighborhood: form.neighborhood,
          city: form.city,
          state: form.state,
        },
      });
      push("success", "Conta criada com sucesso.");
      router.push(returnUrl || "/minha-conta");
    } catch (err) {
      setErrors({
        email: err instanceof Error ? err.message : "Erro no cadastro.",
      });
    }
  }

  const loginHref =
    returnUrl && returnUrl !== "/minha-conta"
      ? `/login?returnUrl=${encodeURIComponent(returnUrl)}`
      : "/login";

  return (
    <div className="mx-auto max-w-2xl px-4 py-12 sm:px-6">
      <h1 className="font-serif text-4xl text-esotera-secondary">Cadastro</h1>
      <p className="mt-2 text-sm text-esotera-muted">
        Crie sua conta para acompanhar pedidos e finalizar compras com
        segurança.
      </p>

      <form onSubmit={handleSubmit} className="mt-8 grid gap-4 sm:grid-cols-2" noValidate>
        <div className="sm:col-span-2">
          <FormField label="Nome completo" id="name" required error={errors.name}>
            <input
              id="name"
              className={inputClassName}
              value={form.name}
              onChange={(e) => set("name", e.target.value)}
            />
          </FormField>
        </div>
        <FormField label="E-mail" id="email" required error={errors.email}>
          <input
            id="email"
            type="email"
            className={inputClassName}
            value={form.email}
            onChange={(e) => set("email", e.target.value)}
          />
        </FormField>
        <FormField label="CPF" id="cpf" required error={errors.cpf}>
          <input
            id="cpf"
            className={inputClassName}
            value={form.cpf}
            onChange={(e) => set("cpf", maskCpf(e.target.value))}
            inputMode="numeric"
          />
        </FormField>
        <FormField label="Telefone" id="phone" required error={errors.phone}>
          <input
            id="phone"
            className={inputClassName}
            value={form.phone}
            onChange={(e) => set("phone", maskPhone(e.target.value))}
            inputMode="tel"
          />
        </FormField>
        <FormField label="Senha" id="password" required error={errors.password}>
          <input
            id="password"
            type="password"
            className={inputClassName}
            value={form.password}
            onChange={(e) => set("password", e.target.value)}
          />
        </FormField>
        <FormField
          label="Confirmação de senha"
          id="confirmPassword"
          required
          error={errors.confirmPassword}
        >
          <input
            id="confirmPassword"
            type="password"
            className={inputClassName}
            value={form.confirmPassword}
            onChange={(e) => set("confirmPassword", e.target.value)}
          />
        </FormField>
        <FormField
          label="CEP"
          id="cep"
          required
          error={
            errors.cep ||
            (cepStatus === "not_found" || cepStatus === "error"
              ? (cepMessage ?? undefined)
              : undefined)
          }
        >
          <input
            id="cep"
            className={inputClassName}
            value={form.cep}
            onChange={(e) => {
              setCepTouched(true);
              set("cep", maskCep(e.target.value));
            }}
            inputMode="numeric"
            aria-busy={lookingUp}
          />
        </FormField>
        {lookingUp ? (
          <p className="sm:col-span-2 text-xs text-esotera-muted" role="status">
            Buscando CEP...
          </p>
        ) : null}
        {cepStatus === "ok" && cepMessage ? (
          <p className="sm:col-span-2 text-xs text-esotera-muted" role="status">
            {cepMessage}
          </p>
        ) : null}
        <FormField label="Endereço" id="street" required error={errors.street}>
          <input
            id="street"
            className={inputClassName}
            value={form.street}
            onChange={(e) => set("street", e.target.value)}
            disabled={lookingUp}
          />
        </FormField>
        <FormField label="Número" id="number" required error={errors.number}>
          <input
            id="number"
            className={inputClassName}
            value={form.number}
            onChange={(e) => set("number", e.target.value)}
          />
        </FormField>
        <FormField label="Complemento (opcional)" id="complement">
          <input
            id="complement"
            className={inputClassName}
            value={form.complement}
            onChange={(e) => set("complement", e.target.value)}
          />
        </FormField>
        <FormField
          label="Bairro"
          id="neighborhood"
          required
          error={errors.neighborhood}
        >
          <input
            id="neighborhood"
            className={inputClassName}
            value={form.neighborhood}
            onChange={(e) => set("neighborhood", e.target.value)}
            disabled={lookingUp}
          />
        </FormField>
        <FormField label="Cidade" id="city" required error={errors.city}>
          <input
            id="city"
            className={inputClassName}
            value={form.city}
            onChange={(e) => set("city", e.target.value)}
            disabled={lookingUp}
          />
        </FormField>
        <FormField label="Estado" id="state" required error={errors.state}>
          <select
            id="state"
            className={inputClassName}
            value={form.state}
            onChange={(e) => set("state", e.target.value)}
            disabled={lookingUp}
          >
            {brazilianStates.map((s) => (
              <option key={s.uf} value={s.uf}>
                {s.uf} — {s.name}
              </option>
            ))}
          </select>
        </FormField>

        <div className="sm:col-span-2 space-y-3">
          <label className="flex items-start gap-2 text-sm text-esotera-muted">
            <input
              type="checkbox"
              checked={form.terms}
              onChange={(e) => set("terms", e.target.checked)}
              className="mt-1"
            />
            <span>
              Aceito os termos de uso
              {errors.terms ? (
                <span className="mt-1 block text-xs text-esotera-error" role="alert">
                  {errors.terms}
                </span>
              ) : null}
            </span>
          </label>
          <label className="flex items-start gap-2 text-sm text-esotera-muted">
            <input
              type="checkbox"
              checked={form.privacy}
              onChange={(e) => set("privacy", e.target.checked)}
              className="mt-1"
            />
            <span>
              Aceito a política de privacidade
              {errors.privacy ? (
                <span className="mt-1 block text-xs text-esotera-error" role="alert">
                  {errors.privacy}
                </span>
              ) : null}
            </span>
          </label>
        </div>

        <div className="sm:col-span-2">
          <Button type="submit" className="w-full sm:w-auto" disabled={lookingUp}>
            {lookingUp ? "Buscando CEP..." : "Criar conta"}
          </Button>
        </div>
      </form>

      <p className="mt-6 text-sm text-esotera-muted">
        Já tem conta?{" "}
        <Link href={loginHref} className="text-esotera-primary hover:underline">
          Entrar
        </Link>
      </p>
    </div>
  );
}

export default function RegisterPage() {
  return (
    <Suspense
      fallback={
        <div className="px-4 py-12 text-center text-esotera-muted">Carregando…</div>
      }
    >
      <RegisterForm />
    </Suspense>
  );
}
