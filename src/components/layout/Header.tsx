"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useState } from "react";
import { Menu, ShoppingBag, User, X } from "lucide-react";
import { useCartStore } from "@/stores/cartStore";
import { useAuthStore } from "@/stores/authStore";
import { MobileMenu } from "@/components/layout/MobileMenu";
import { BrandLogo } from "@/components/brand/BrandLogo";

const links = [
  { href: "/", label: "Início" },
  { href: "/produtos", label: "Produtos" },
  { href: "/minha-conta", label: "Minha conta" },
];

export function Header() {
  const pathname = usePathname();
  const items = useCartStore((s) => s.items);
  const itemCount = items.reduce((sum, i) => sum + i.quantity, 0);
  const user = useAuthStore((s) => s.user);
  const [open, setOpen] = useState(false);

  if (pathname.startsWith("/admin")) return null;

  return (
    <header className="sticky top-0 z-40 border-b border-esotera-border bg-esotera-surface/95 backdrop-blur-md">
      <div className="mx-auto flex h-16 max-w-6xl items-center justify-between gap-4 px-4 sm:px-6">
        <BrandLogo variant="dark" priority />

        <nav className="hidden items-center gap-6 md:flex" aria-label="Principal">
          {links.map((link) => (
            <Link
              key={link.href}
              href={link.href}
              className={`text-sm font-medium transition ${
                pathname === link.href
                  ? "text-esotera-primary"
                  : "text-esotera-secondary hover:text-esotera-primary"
              }`}
            >
              {link.label}
            </Link>
          ))}
          {user?.role === "admin" ? (
            <Link
              href="/admin"
              className="text-sm font-medium text-esotera-muted hover:text-esotera-primary"
            >
              Admin
            </Link>
          ) : null}
        </nav>

        <div className="flex items-center gap-1 sm:gap-2">
          <Link
            href={user ? "/minha-conta" : "/login"}
            className="hidden min-h-11 items-center justify-center gap-2 rounded-md px-2 text-esotera-secondary hover:text-esotera-primary sm:inline-flex"
            aria-label={user ? "Minha conta" : "Entrar"}
          >
            <User size={20} />
            {user ? (
              <span className="max-w-[9rem] truncate text-sm font-medium">
                {user.name.split(" ")[0]}
              </span>
            ) : (
              <span className="text-sm font-medium">Entrar</span>
            )}
          </Link>
          <Link
            href="/carrinho"
            className="relative inline-flex min-h-11 min-w-11 items-center justify-center rounded-md text-esotera-secondary hover:text-esotera-primary"
            aria-label={`Carrinho com ${itemCount} itens`}
          >
            <ShoppingBag size={20} />
            {itemCount > 0 ? (
              <span className="absolute right-1 top-1 flex h-5 min-w-5 items-center justify-center rounded-full bg-esotera-primary px-1 text-[10px] font-semibold text-white">
                {itemCount}
              </span>
            ) : null}
          </Link>
          <button
            type="button"
            className="inline-flex min-h-11 min-w-11 items-center justify-center rounded-md text-esotera-secondary hover:text-esotera-primary md:hidden"
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
