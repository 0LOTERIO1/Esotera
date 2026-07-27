import Link from "next/link";
import { ButtonLink } from "@/components/ui/Button";

export default function ProductNotFound() {
  return (
    <div className="mx-auto max-w-lg px-4 py-20 text-center">
      <h1 className="font-serif text-3xl text-esotera-white">
        Produto não encontrado
      </h1>
      <p className="mt-3 text-sm text-esotera-muted">
        O item solicitado não existe neste catálogo de demonstração.
      </p>
      <div className="mt-8">
        <ButtonLink href="/produtos">Voltar ao catálogo</ButtonLink>
      </div>
      <p className="mt-4 text-sm">
        <Link href="/" className="text-esotera-gold hover:underline">
          Ir para o início
        </Link>
      </p>
    </div>
  );
}
