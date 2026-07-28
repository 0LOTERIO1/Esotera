import Link from "next/link";

export default function PrivacyPage() {
  return (
    <div className="mx-auto max-w-3xl px-4 py-12 sm:px-6">
      <h1 className="font-serif text-4xl text-esotera-secondary">
        Política de privacidade
      </h1>
      <p className="mt-2 text-xs text-esotera-muted">Versão 2026-07-01</p>
      <div className="mt-8 space-y-4 text-sm leading-relaxed text-esotera-muted">
        <p>
          A Esotera trata dados pessoais necessários para cadastro, compra,
          entrega, atendimento e, quando autorizado, envio de comunicações
          (newsletter).
        </p>
        <p>
          Dados como nome, e-mail, telefone, CPF e endereço são utilizados para
          processar pedidos e cumprir obrigações legais. Não vendemos seus
          dados a terceiros.
        </p>
        <p>
          Você pode solicitar informações sobre seus dados ou o cancelamento da
          newsletter pelos canais oficiais. O descadastramento da newsletter
          também pode ser feito pelo link enviado nos e-mails.
        </p>
        <p>
          Esta política observa princípios da LGPD e não limita direitos
          previstos na legislação brasileira.
        </p>
        <p>
          Contato:{" "}
          <Link href="/contato" className="text-esotera-primary hover:underline">
            página de contato
          </Link>
          .
        </p>
      </div>
    </div>
  );
}
