"use client";

import Link from "next/link";
import { storeConfig, mailtoHref, whatsappHref } from "@/config/store";

export default function ContactPage() {
  return (
    <div className="mx-auto max-w-2xl px-4 py-12 sm:px-6">
      <h1 className="font-serif text-4xl text-esotera-secondary">Contato</h1>
      <p className="mt-3 text-sm text-esotera-muted">
        Fale com a Esotera pelos canais oficiais abaixo.
      </p>
      <ul className="mt-8 space-y-4 text-sm text-esotera-text">
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
            href={whatsappHref()}
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
      <p className="mt-8 text-sm text-esotera-muted">
        <Link href="/trocas-e-devolucoes" className="text-esotera-primary hover:underline">
          Trocas e devoluções
        </Link>
      </p>
    </div>
  );
}
