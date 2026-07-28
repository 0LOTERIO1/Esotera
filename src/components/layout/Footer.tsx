"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { storeConfig, mailtoHref, whatsappHref } from "@/config/store";
import { useSettingsStore } from "@/stores/settingsStore";
import { BrandLogo } from "@/components/brand/BrandLogo";

export function Footer() {
  const pathname = usePathname();
  const storeName = useSettingsStore((s) => s.settings.storeName);

  if (pathname.startsWith("/admin")) return null;

  return (
    <footer className="mt-auto border-t border-esotera-border bg-esotera-surface-secondary">
      <div className="mx-auto grid max-w-6xl gap-8 px-4 py-10 sm:px-6 md:grid-cols-3">
        <div>
          <BrandLogo variant="dark" href={null} />
          <p className="mt-3 text-sm text-esotera-muted">{storeConfig.tagline}</p>
        </div>
        <div>
          <p className="text-sm font-semibold text-esotera-secondary">Navegação</p>
          <ul className="mt-3 space-y-2 text-sm text-esotera-muted">
            <li>
              <Link href="/produtos" className="hover:text-esotera-primary">
                Produtos
              </Link>
            </li>
            <li>
              <Link href="/minha-conta" className="hover:text-esotera-primary">
                Minha conta
              </Link>
            </li>
            <li>
              <Link href="/carrinho" className="hover:text-esotera-primary">
                Carrinho
              </Link>
            </li>
            <li>
              <Link href="/contato" className="hover:text-esotera-primary">
                Contato
              </Link>
            </li>
            <li>
              <Link
                href="/trocas-e-devolucoes"
                className="hover:text-esotera-primary"
              >
                Trocas e devoluções
              </Link>
            </li>
            <li>
              <Link href="/termos" className="hover:text-esotera-primary">
                Termos de uso
              </Link>
            </li>
            <li>
              <Link href="/privacidade" className="hover:text-esotera-primary">
                Privacidade
              </Link>
            </li>
          </ul>
        </div>
        <div>
          <p className="text-sm font-semibold text-esotera-secondary">Contato</p>
          <ul className="mt-3 space-y-2 text-sm text-esotera-muted">
            <li>
              <a
                href={storeConfig.social.instagram}
                className="hover:text-esotera-primary"
                target="_blank"
                rel="noopener noreferrer"
              >
                Instagram {storeConfig.social.instagramHandle}
              </a>
            </li>
            <li>
              <a
                href={whatsappHref()}
                className="hover:text-esotera-primary"
                target="_blank"
                rel="noopener noreferrer"
              >
                WhatsApp {storeConfig.whatsapp}
              </a>
            </li>
            <li>
              <a href={mailtoHref()} className="hover:text-esotera-primary">
                {storeConfig.email}
              </a>
            </li>
          </ul>
        </div>
      </div>
      <div className="border-t border-esotera-border py-4 text-center text-xs text-esotera-muted">
        © {new Date().getFullYear()} {storeName}. Todos os direitos reservados.
      </div>
    </footer>
  );
}
