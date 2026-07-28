"use client";

import { useEffect, useState } from "react";
import { Button } from "@/components/ui/Button";
import { FormField, inputClassName } from "@/components/ui/FormField";
import {
  newsletterApi,
  type NewsletterSubscription,
} from "@/services/api/newsletterApi";
import { ApiError } from "@/services/api/apiClient";
import { sessionService } from "@/services/api/sessionService";
import { formatDate } from "@/utils/format";
import { useToastStore } from "@/stores/toastStore";
import { useAuthStore } from "@/stores/authStore";

export default function AdminNewsletterPage() {
  const push = useToastStore((s) => s.push);
  const user = useAuthStore((s) => s.user);
  const hydrated = useAuthStore((s) => s.hydrated);
  const sessionReady = useAuthStore((s) => s.sessionReady);
  const authReady = hydrated && sessionReady;
  const isAdmin = user?.role?.toLowerCase() === "admin";

  const [items, setItems] = useState<NewsletterSubscription[]>([]);
  const [total, setTotal] = useState(0);
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState<"all" | "active" | "inactive">("all");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    if (!authReady || !isAdmin) return;

    let cancelled = false;
    (async () => {
      setLoading(true);
      setError(null);

      if (!sessionService.getToken()) {
        if (!cancelled) {
          setItems([]);
          setTotal(0);
          setError(
            "A newsletter administrativa exige sessão na API (JWT). Faça login com NEXT_PUBLIC_DATA_MODE=api e tente novamente.",
          );
          setLoading(false);
        }
        return;
      }

      try {
        const res = await newsletterApi.adminList({
          search: search.trim() || undefined,
          isActive: status === "all" ? null : status === "active",
          take: 200,
        });
        if (cancelled) return;
        setItems(res.items);
        setTotal(res.total);
      } catch (err) {
        if (cancelled) return;
        // 403/erros isolados: não fazer logout — só informar
        const message =
          err instanceof ApiError
            ? err.userMessage
            : err instanceof Error
              ? err.message
              : "Falha ao listar inscritos.";
        setError(message);
        push("error", message);
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [authReady, isAdmin, search, status, reloadKey, push]);

  async function exportCsv() {
    if (!sessionService.getToken()) {
      push("error", "Sessão na API necessária para exportar.");
      return;
    }
    try {
      const blob = await newsletterApi.adminExportCsv({
        search: search.trim() || undefined,
        isActive: status === "all" ? null : status === "active",
      });
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `newsletter-${new Date().toISOString().slice(0, 10)}.csv`;
      a.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      push(
        "error",
        err instanceof ApiError ? err.userMessage : "Falha ao exportar CSV.",
      );
    }
  }

  if (!authReady) {
    return (
      <p className="text-sm text-esotera-muted">Carregando sessão…</p>
    );
  }

  return (
    <div>
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="font-serif text-3xl text-esotera-secondary">
            Newsletter
          </h1>
          <p className="mt-1 text-sm text-esotera-muted">
            {total} inscrição(ões) · cadastro para uso futuro (sem disparo em
            massa nesta etapa)
          </p>
        </div>
        <div className="flex gap-2">
          <Button
            type="button"
            variant="secondary"
            onClick={() => setReloadKey((k) => k + 1)}
          >
            Atualizar
          </Button>
          <Button type="button" variant="secondary" onClick={() => void exportCsv()}>
            Exportar CSV
          </Button>
        </div>
      </div>

      {error ? (
        <p role="alert" className="mt-4 text-sm text-esotera-error">
          {error}
        </p>
      ) : null}

      <div className="mt-6 grid gap-3 sm:grid-cols-2">
        <FormField label="Pesquisar e-mail" id="nl-search">
          <input
            id="nl-search"
            className={inputClassName}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="trecho do e-mail"
          />
        </FormField>
        <FormField label="Status" id="nl-status">
          <select
            id="nl-status"
            className={inputClassName}
            value={status}
            onChange={(e) =>
              setStatus(e.target.value as "all" | "active" | "inactive")
            }
          >
            <option value="all">Todos</option>
            <option value="active">Ativos</option>
            <option value="inactive">Inativos</option>
          </select>
        </FormField>
      </div>

      <div className="mt-6 overflow-x-auto">
        <table className="min-w-full text-left text-sm">
          <thead className="border-b border-esotera-border text-esotera-muted">
            <tr>
              <th className="px-2 py-2 font-medium">E-mail</th>
              <th className="px-2 py-2 font-medium">Status</th>
              <th className="px-2 py-2 font-medium">Inscrição</th>
              <th className="px-2 py-2 font-medium">Consentimento</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={4} className="px-2 py-6 text-esotera-muted">
                  Carregando…
                </td>
              </tr>
            ) : items.length === 0 ? (
              <tr>
                <td colSpan={4} className="px-2 py-6 text-esotera-muted">
                  Nenhuma inscrição encontrada.
                </td>
              </tr>
            ) : (
              items.map((item) => (
                <tr key={item.id} className="border-b border-esotera-border/60">
                  <td className="px-2 py-2 text-esotera-text">{item.email}</td>
                  <td className="px-2 py-2">
                    {item.isActive ? "Ativo" : "Inativo"}
                  </td>
                  <td className="px-2 py-2 text-esotera-muted">
                    {formatDate(item.createdAtUtc)}
                  </td>
                  <td className="px-2 py-2 text-esotera-muted">
                    {formatDate(item.consentAtUtc)}
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
