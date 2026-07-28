"use client";

import Link from "next/link";
import { useAuthStore } from "@/stores/authStore";

type MobileMenuProps = {
  open: boolean;
  onClose: () => void;
  links: Array<{ href: string; label: string }>;
};

export function MobileMenu({ open, onClose, links }: MobileMenuProps) {
  const user = useAuthStore((s) => s.user);

  if (!open) return null;

  return (
    <div className="border-t border-esotera-border bg-esotera-surface md:hidden">
      <nav
        className="mx-auto flex max-w-6xl flex-col gap-1 px-4 py-3"
        aria-label="Menu móvel"
      >
        {links.map((link) => (
          <Link
            key={link.href}
            href={link.href}
            onClick={onClose}
            className="rounded-md px-3 py-3 text-esotera-secondary hover:bg-esotera-surface-secondary hover:text-esotera-primary"
          >
            {link.label}
          </Link>
        ))}
        <Link
          href={user ? "/minha-conta" : "/login"}
          onClick={onClose}
          className="rounded-md px-3 py-3 text-esotera-secondary hover:bg-esotera-surface-secondary hover:text-esotera-primary"
        >
          {user ? `Olá, ${user.name.split(" ")[0]}` : "Entrar"}
        </Link>
        {user?.role === "admin" ? (
          <Link
            href="/admin"
            onClick={onClose}
            className="rounded-md px-3 py-3 text-esotera-secondary hover:bg-esotera-surface-secondary hover:text-esotera-primary"
          >
            Painel administrativo
          </Link>
        ) : null}
      </nav>
    </div>
  );
}
