"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { ExternalLink, Globe, Link2 } from "lucide-react";
import { storeConfig } from "@/config/store";
import { useSettingsStore } from "@/stores/settingsStore";

export function Footer() {
  const pathname = usePathname();
  const storeName = useSettingsStore((s) => s.settings.storeName);

  if (pathname.startsWith("/admin")) return null;

  return (
    <footer className="mt-auto border-t border-esotera-graphite bg-esotera-black/60">
      <div className="mx-auto grid max-w-6xl gap-8 px-4 py-12 sm:px-6 md:grid-cols-3">
        <div>
          <p className="font-serif text-xl text-esotera-gold">{storeName}</p>
          <p className="mt-2 text-sm text-esotera-muted">{storeConfig.tagline}</p>
        </div>
        <div>
          <p className="text-sm font-medium text-esotera-beige">Institucional</p>
          <ul className="mt-3 space-y-2 text-sm text-esotera-muted">
            <li>
              <Link href="/produtos" className="hover:text-esotera-gold">
                Produtos
              </Link>
            </li>
            <li>
              <Link href="/minha-conta" className="hover:text-esotera-gold">
                Minha conta
              </Link>
            </li>
            <li>
              <span className="cursor-default">Sobre nós (em breve)</span>
            </li>
            <li>
              <span className="cursor-default">Trocas e devoluções (visual)</span>
            </li>
            <li>
              <span className="cursor-default">Privacidade (visual)</span>
            </li>
          </ul>
        </div>
        <div>
          <p className="text-sm font-medium text-esotera-beige">Contato</p>
          <p className="mt-3 text-sm text-esotera-muted">{storeConfig.email}</p>
          <p className="text-sm text-esotera-muted">{storeConfig.phone}</p>
          <div className="mt-4 flex gap-3">
            <a
              href={storeConfig.social.instagram}
              aria-label="Instagram (fictício)"
              className="text-esotera-muted hover:text-esotera-gold"
            >
              <Globe size={18} />
            </a>
            <a
              href={storeConfig.social.facebook}
              aria-label="Facebook (fictício)"
              className="text-esotera-muted hover:text-esotera-gold"
            >
              <Link2 size={18} />
            </a>
            <a
              href={storeConfig.social.youtube}
              aria-label="YouTube (fictício)"
              className="text-esotera-muted hover:text-esotera-gold"
            >
              <ExternalLink size={18} />
            </a>
          </div>
        </div>
      </div>
      <div className="border-t border-esotera-graphite/60 py-4 text-center text-xs text-esotera-muted">
        © {new Date().getFullYear()} {storeName}. Protótipo de demonstração.
      </div>
    </footer>
  );
}
