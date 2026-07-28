"use client";

import Link from "next/link";
import { storeConfig, mailtoHref, whatsappHref } from "@/config/store";

export default function ExchangesPage() {
  const wa = whatsappHref(storeConfig.whatsappExchangeMessage);

  return (
    <div className="mx-auto max-w-3xl px-4 py-12 sm:px-6">
      <h1 className="font-serif text-4xl text-esotera-secondary">
        Trocas e devoluções
      </h1>
      <p className="mt-3 text-sm leading-relaxed text-esotera-muted">
        Se precisar trocar ou devolver um produto, entre em contato conosco.
        Cada solicitação é analisada individualmente pela equipe da Esotera,
        em conformidade com o Código de Defesa do Consumidor.
      </p>

      <h2 className="mt-10 font-serif text-2xl text-esotera-secondary">
        Como funciona
      </h2>
      <ol className="mt-4 list-decimal space-y-3 pl-5 text-sm text-esotera-muted">
        <li>
          O cliente entra em contato com a Esotera por WhatsApp, Instagram ou
          e-mail.
        </li>
        <li>A equipe analisa a solicitação.</li>
        <li>
          Quando a devolução for autorizada, a Esotera envia uma etiqueta de
          postagem.
        </li>
        <li>O cliente cola a etiqueta na embalagem.</li>
        <li>
          O cliente leva o produto ao ponto de coleta indicado ou mais próximo.
        </li>
        <li>
          Após o recebimento e a análise do produto, a equipe dá continuidade
          à troca ou ao reembolso, conforme o caso.
        </li>
      </ol>

      <p className="mt-6 rounded-md border border-esotera-border bg-esotera-surface-secondary p-4 text-sm text-esotera-muted">
        Conserve o produto, os acessórios e a embalagem adequadamente até
        receber as orientações da equipe. A etiqueta de postagem é gerada e
        enviada pela Esotera após o contato e a análise — não há geração
        automática pelo site.
      </p>

      <h2 className="mt-10 font-serif text-2xl text-esotera-secondary">
        Canais oficiais
      </h2>
      <ul className="mt-4 space-y-2 text-sm">
        <li>
          Instagram:{" "}
          <a
            href={storeConfig.social.instagram}
            className="text-esotera-primary hover:underline"
            target="_blank"
            rel="noopener noreferrer"
          >
            {storeConfig.social.instagramHandle}
          </a>
        </li>
        <li>
          WhatsApp:{" "}
          <a
            href={wa}
            className="text-esotera-primary hover:underline"
            target="_blank"
            rel="noopener noreferrer"
          >
            {storeConfig.whatsapp}
          </a>
        </li>
        <li>
          E-mail:{" "}
          <a href={mailtoHref()} className="text-esotera-primary hover:underline">
            {storeConfig.email}
          </a>
        </li>
      </ul>

      <p className="mt-10 text-sm text-esotera-muted">
        <Link href="/contato" className="text-esotera-primary hover:underline">
          Página de contato
        </Link>
      </p>
    </div>
  );
}
