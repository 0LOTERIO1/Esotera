"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useState } from "react";
import { Menu, ShoppingBag, User, X } from "lucide-react";
import { useSettingsStore } from "@/stores/settingsStore";
import { useCartStore } from "@/stores/cartStore";
import { useAuthStore } from "@/stores/authStore";
import { MobileMenu } from "@/components/layout/MobileMenu";

const links = [
  { href: "/", label: "Início" },
  { href: "/produtos", label: "Produtos" },
  { href: "/minha-conta", label: "Minha conta" },
];

export function Header() {
  const pathname = usePathname();
  const storeName = useSettingsStore((s) => s.settings.storeName);
  const items = useCartStore((s) => s.items);
  const itemCount = items.reduce((sum, i) => sum + i.quantity, 0);
  const user = useAuthStore((s) => s.user);
  const [open, setOpen] = useState(false);

  if (pathname.startsWith("/admin")) return null;

  return (
    <header className="sticky top-0 z-40 border-b border-esotera-graphite/80 bg-esotera-navy/90 backdrop-blur-md">
      <div className="mx-auto flex h-16 max-w-6xl items-center justify-between gap-4 px-4 sm:px-6">
        <Link
          href="/"
          className="font-serif text-[1.75rem] tracking-[0.04em] text-esotera-gold transition hover:text-esotera-gold-soft"
        >
          {storeName}
        </Link>

        <nav className="hidden items-center gap-6 md:flex" aria-label="Principal">
          {links.map((link) => (
            <Link
              key={link.href}
              href={link.href}
              className={`text-sm transition ${
                pathname === link.href
                  ? "text-esotera-gold"
                  : "text-esotera-beige hover:text-esotera-gold"
              }`}
            >
              {link.label}
            </Link>
          ))}
          {user?.role === "admin" ? (
            <Link
              href="/admin"
              className="text-sm text-esotera-muted hover:text-esotera-gold"
            >
              Admin
            </Link>
          ) : null}
        </nav>

        <div className="flex items-center gap-2">
          <Link
            href={user ? "/minha-conta" : "/login"}
            className="hidden rounded-md p-2 text-esotera-beige hover:text-esotera-gold sm:inline-flex"
            aria-label={user ? "Minha conta" : "Entrar"}
          >
            <User size={20} />
          </Link>
          <Link
            href="/carrinho"
            className="relative inline-flex rounded-md p-2 text-esotera-beige hover:text-esotera-gold"
            aria-label={`Carrinho com ${itemCount} itens`}
          >
            <ShoppingBag size={20} />
            {itemCount > 0 ? (
              <span className="absolute -right-0.5 -top-0.5 flex h-5 min-w-5 items-center justify-center rounded-full bg-esotera-gold px-1 text-[10px] font-semibold text-esotera-black">
                {itemCount}
              </span>
            ) : null}
          </Link>
          <button
            type="button"
            className="inline-flex rounded-md p-2 text-esotera-beige hover:text-esotera-gold md:hidden"
            aria-label={open ? "Fechar menu" : "Abrir menu"}
            aria-expanded={open}
            onClick={() => setOpen((v) => !v)}
          >
            {open ? <X size={22} /> : <Menu size={22} />}
          </button>
        </div>
      </div>
      <MobileMenu open={open} onClose={() => setOpen(false)} links={links} />
    </header>
  );
}
