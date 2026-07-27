import { ButtonLink } from "@/components/ui/Button";
import { storeConfig } from "@/config/store";

export function HeroBanner() {
  return (
    <section className="relative overflow-hidden border-b border-esotera-graphite">
      <div
        className="absolute inset-0 bg-[radial-gradient(ellipse_at_30%_20%,rgba(196,163,90,0.12),transparent_50%),radial-gradient(ellipse_at_80%_60%,rgba(45,31,61,0.45),transparent_55%)]"
        aria-hidden
      />
      <div className="relative mx-auto flex min-h-[70vh] max-w-6xl flex-col justify-center px-4 py-20 sm:px-6">
        <p className="animate-fade-in text-sm uppercase tracking-[0.25em] text-esotera-gold">
          {storeConfig.name}
        </p>
        <h1 className="animate-rise mt-5 max-w-2xl font-serif text-5xl leading-[1.15] text-esotera-white sm:text-6xl md:text-7xl">
          {storeConfig.name}
        </h1>
        <p className="animate-rise mt-6 max-w-xl text-base leading-relaxed text-esotera-beige/90 sm:text-lg" style={{ animationDelay: "0.1s" }}>
          Um espaço para o autoconhecimento e a espiritualidade, com tarôs e
          produtos esotéricos selecionados com sofisticação.
        </p>
        <div className="animate-rise mt-8" style={{ animationDelay: "0.2s" }}>
          <ButtonLink href="/produtos">Conheça os tarôs</ButtonLink>
        </div>
      </div>
    </section>
  );
}
