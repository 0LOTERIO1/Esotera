"use client";

import { Button } from "@/components/ui/Button";
import { FormField, inputClassName } from "@/components/ui/FormField";
import { useToastStore } from "@/stores/toastStore";

export function NewsletterSection() {
  const push = useToastStore((s) => s.push);

  return (
    <section className="mx-auto max-w-6xl px-4 py-10 sm:px-6">
      <div className="rounded-xl border border-esotera-border bg-esotera-surface px-5 py-8 shadow-sm sm:px-8">
        <h2 className="font-serif text-2xl text-esotera-secondary sm:text-3xl">
          Newsletter
        </h2>
        <p className="mt-2 max-w-xl text-sm text-esotera-muted">
          Cadastro visual apenas — nenhum e-mail será enviado neste protótipo.
        </p>
        <form
          className="mt-5 flex max-w-lg flex-col gap-3 sm:flex-row sm:items-end"
          onSubmit={(e) => {
            e.preventDefault();
            push(
              "info",
              "Inscrição visual registrada. Sem envio real de e-mails.",
            );
            (e.target as HTMLFormElement).reset();
          }}
        >
          <div className="flex-1">
            <FormField label="Seu e-mail" id="newsletter-email">
              <input
                id="newsletter-email"
                type="email"
                required
                className={inputClassName}
                placeholder="voce@email.com"
              />
            </FormField>
          </div>
          <Button type="submit">Inscrever-se</Button>
        </form>
      </div>
    </section>
  );
}
