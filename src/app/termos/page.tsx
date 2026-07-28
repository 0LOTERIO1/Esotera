import Link from "next/link";

export default function TermsPage() {
  return (
    <div className="mx-auto max-w-3xl px-4 py-12 sm:px-6">
      <h1 className="font-serif text-4xl text-esotera-secondary">
        Termos de uso
      </h1>
      <p className="mt-2 text-xs text-esotera-muted">Versão 2026-07-01</p>
      <div className="mt-8 space-y-4 text-sm leading-relaxed text-esotera-muted">
        <p>
          Ao utilizar o site da Esotera, você concorda com estes termos. A
          loja oferece produtos esotéricos para compra online, com cadastro de
          conta, carrinho e acompanhamento de pedidos.
        </p>
        <p>
          Você é responsável por manter a confidencialidade da sua senha e por
          fornecer dados verdadeiros no cadastro e no checkout.
        </p>
        <p>
          Preços, disponibilidade e opções de frete podem variar e são
          confirmados no momento da compra. Pedidos estão sujeitos à análise
          de pagamento e à disponibilidade de estoque.
        </p>
        <p>
          Estes termos não excluem direitos previstos no Código de Defesa do
          Consumidor e na legislação brasileira aplicável.
        </p>
        <p>
          Em caso de dúvidas, utilize os canais oficiais em{" "}
          <Link href="/contato" className="text-esotera-primary hover:underline">
            Contato
          </Link>
          .
        </p>
      </div>
    </div>
  );
}
