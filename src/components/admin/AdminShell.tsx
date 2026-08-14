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
  Mail,
  Truck,
} from "lucide-react";
import { useAuthStore } from "@/stores/authStore";
import { useSettingsStore } from "@/stores/settingsStore";
import { BrandLogo } from "@/components/brand/BrandLogo";
import { ButtonLink } from "@/components/ui/Button";

const nav = [
  { href: "/admin", label: "Dashboard", icon: LayoutDashboard },
  { href: "/admin/produtos", label: "Produtos", icon: Package },
  { href: "/admin/pedidos", label: "Pedidos", icon: ShoppingBag },
  { href: "/admin/clientes", label: "Clientes", icon: Users },
  { href: "/admin/cupons", label: "Cupons", icon: Ticket },
  { href: "/admin/newsletter", label: "Newsletter", icon: Mail },
  { href: "/admin/j3-fulfillments", label: "Entregas J3", icon: Truck },
  { href: "/admin/configuracoes", label: "Configurações", icon: Settings },
];

/**
 * Shell administrativo.
 * Fase 1: autenticação pode vir da API (JWT), mas o painel ainda opera com dados mock.
 * A autorização Admin é verificada no front; a API já exige role Admin nas rotas /api/admin/*.
 */
export function AdminShell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const router = useRouter();
  const user = useAuthStore((s) => s.user);
  const hydrated = useAuthStore((s) => s.hydrated);
  const sessionReady = useAuthStore((s) => s.sessionReady);
  const storeName = useSettingsStore((s) => s.settings.storeName);
  const isAdmin = user?.role?.toLowerCase() === "admin";
  const authReady = hydrated && sessionReady;

  useEffect(() => {
    if (!authReady) return;
    if (!user) {
      const returnUrl = encodeURIComponent(pathname || "/admin");
      router.replace(`/login?returnUrl=${returnUrl}`);
      return;
    }
    if (user.role.toLowerCase() !== "admin") {
      router.replace("/minha-conta");
    }
  }, [authReady, user, router, pathname]);

  if (!authReady) {
    return (
      <div className="px-4 py-16 text-center text-esotera-muted">
        Verificando acesso administrativo…
      </div>
    );
  }

  if (!user) {
    return (
      <div className="px-4 py-16 text-center text-esotera-muted">
        Redirecionando para o login…
      </div>
    );
  }

  if (!isAdmin) {
    return (
      <div className="mx-auto max-w-lg px-4 py-16 text-center">
        <h1 className="font-serif text-2xl text-esotera-secondary">
          Acesso administrativo negado
        </h1>
        <p className="mt-3 text-sm text-esotera-muted">
          Esta área é restrita a administradores. Sua conta de cliente não tem
          permissão para gerenciar produtos, pedidos ou configurações.
        </p>
        <div className="mt-6 flex flex-wrap justify-center gap-3">
          <ButtonLink href="/minha-conta" variant="secondary">
            Minha conta
          </ButtonLink>
          <ButtonLink href="/">Ir para a loja</ButtonLink>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-[70vh] border-b border-esotera-border bg-esotera-background">
      <div className="border-b border-esotera-border bg-esotera-surface">
        <div className="mx-auto flex max-w-7xl items-center justify-between gap-4 px-4 py-3 sm:px-6">
          <div className="flex items-center gap-3">
            <BrandLogo variant="dark" href="/" />
            <span className="hidden text-sm font-medium text-esotera-muted sm:inline">
              {storeName} · Admin
            </span>
          </div>
          <Link
            href="/"
            className="inline-flex min-h-11 items-center gap-2 text-sm text-esotera-muted hover:text-esotera-primary"
          >
            <ArrowLeft size={14} /> Loja
          </Link>
        </div>
      </div>

      <div className="mx-auto flex max-w-7xl flex-col gap-6 px-4 py-6 sm:px-6 lg:flex-row">
        <aside className="w-full shrink-0 lg:w-56">
          <nav
            className="flex flex-row gap-1 overflow-x-auto pb-1 lg:flex-col lg:overflow-visible"
            aria-label="Admin"
          >
            {nav.map((item) => {
              const active =
                item.href === "/admin"
                  ? pathname === "/admin"
                  : pathname.startsWith(item.href);
              return (
                <Link
                  key={item.href}
                  href={item.href}
                  className={`flex min-h-11 items-center gap-2 whitespace-nowrap rounded-md px-3 py-2.5 text-sm ${
                    active
                      ? "bg-esotera-primary/10 font-medium text-esotera-primary"
                      : "text-esotera-muted hover:bg-esotera-surface-secondary hover:text-esotera-secondary"
                  }`}
                >
                  <item.icon size={16} aria-hidden />
                  {item.label}
                </Link>
              );
            })}
          </nav>
        </aside>
        <div className="min-w-0 flex-1">{children}</div>
      </div>
    </div>
  );
}
