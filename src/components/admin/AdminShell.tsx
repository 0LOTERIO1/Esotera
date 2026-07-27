"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect } from "react";
import {
  LayoutDashboard,
  Package,
  ShoppingBag,
  Users,
  Ticket,
  Settings,
  ArrowLeft,
} from "lucide-react";
import { useAuthStore } from "@/stores/authStore";
import { useSettingsStore } from "@/stores/settingsStore";

const nav = [
  { href: "/admin", label: "Dashboard", icon: LayoutDashboard },
  { href: "/admin/produtos", label: "Produtos", icon: Package },
  { href: "/admin/pedidos", label: "Pedidos", icon: ShoppingBag },
  { href: "/admin/clientes", label: "Clientes", icon: Users },
  { href: "/admin/cupons", label: "Cupons", icon: Ticket },
  { href: "/admin/configuracoes", label: "Configurações", icon: Settings },
];

export function AdminShell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const router = useRouter();
  const user = useAuthStore((s) => s.user);
  const hydrated = useAuthStore((s) => s.hydrated);
  const storeName = useSettingsStore((s) => s.settings.storeName);

  useEffect(() => {
    if (!hydrated) return;
    if (!user) {
      router.replace("/login?returnUrl=/admin");
      return;
    }
    if (user.role !== "admin") {
      router.replace("/minha-conta");
    }
  }, [hydrated, user, router]);

  if (!hydrated || !user || user.role !== "admin") {
    return (
      <div className="px-4 py-16 text-center text-esotera-muted">
        Verificando acesso administrativo…
      </div>
    );
  }

  return (
    <div className="mx-auto flex min-h-[70vh] max-w-7xl flex-col gap-6 px-4 py-8 sm:px-6 lg:flex-row">
      <aside className="w-full shrink-0 lg:w-56">
        <p className="font-serif text-lg text-esotera-gold">{storeName} Admin</p>
        <nav className="mt-4 flex flex-row gap-1 overflow-x-auto lg:flex-col" aria-label="Admin">
          {nav.map((item) => {
            const active =
              item.href === "/admin"
                ? pathname === "/admin"
                : pathname.startsWith(item.href);
            return (
              <Link
                key={item.href}
                href={item.href}
                className={`flex items-center gap-2 whitespace-nowrap rounded-md px-3 py-2.5 text-sm ${
                  active
                    ? "bg-esotera-gold/15 text-esotera-gold"
                    : "text-esotera-muted hover:bg-esotera-graphite/40 hover:text-esotera-beige"
                }`}
              >
                <item.icon size={16} aria-hidden />
                {item.label}
              </Link>
            );
          })}
        </nav>
        <Link
          href="/"
          className="mt-4 inline-flex items-center gap-2 text-sm text-esotera-muted hover:text-esotera-gold"
        >
          <ArrowLeft size={14} /> Voltar à loja
        </Link>
      </aside>
      <div className="min-w-0 flex-1">{children}</div>
    </div>
  );
}
