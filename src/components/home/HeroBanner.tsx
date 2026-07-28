import { ButtonLink } from "@/components/ui/Button";

export function HeroBanner() {
  return (
    <section className="border-b border-esotera-border bg-gradient-to-br from-esotera-surface-secondary via-esotera-background to-esotera-surface">
      <div className="mx-auto flex max-w-6xl flex-col justify-center px-4 py-10 sm:px-6 sm:py-14 md:py-16">
        <p className="text-xs font-semibold uppercase tracking-[0.2em] text-esotera-primary sm:text-sm">
          Tarôs e produtos esotéricos
        </p>
        <h1 className="mt-3 max-w-2xl font-serif text-3xl leading-tight text-esotera-secondary sm:text-4xl md:text-5xl">
          Esotera — escolha seu tarô com praticidade
        </h1>
        <p className="mt-3 max-w-xl text-sm leading-relaxed text-esotera-muted sm:text-base">
          Autoconhecimento e espiritualidade com produtos selecionados, entrega
          para todo o Brasil e compra simples.
        </p>
        <div className="mt-6">
          <ButtonLink href="/produtos">Conheça os tarôs</ButtonLink>
        </div>
      </div>
    </section>
  );
}
