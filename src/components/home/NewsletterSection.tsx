"use client";

import { FormField, inputClassName } from "@/components/ui/FormField";
import { Button } from "@/components/ui/Button";

/**
 * Newsletter pública — backend de e-mail ainda não integrado.
 * Formulário permanece visível, mas o envio fica desabilitado.
 */
export function NewsletterSection() {
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
          className="mt-5 flex max-w-lg flex-col gap-3 sm:flex-row sm:items-end"
          onSubmit={(e) => e.preventDefault()}
        >
          <div className="flex-1">
            <FormField label="Seu e-mail" id="newsletter-email">
              <input
                id="newsletter-email"
                type="email"
                className={inputClassName}
                placeholder="voce@email.com"
                disabled
                aria-disabled="true"
              />
            </FormField>
          </div>
          <Button type="button" disabled>
            Em breve
          </Button>
        </form>
        <p className="mt-3 text-xs text-esotera-muted">
          Em breve você poderá se inscrever por aqui.
        </p>
      </div>
    </section>
  );
}
