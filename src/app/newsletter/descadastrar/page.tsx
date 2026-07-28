"use client";

import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { Suspense, useEffect, useState } from "react";
import { newsletterApi } from "@/services/api/newsletterApi";
import { ApiError } from "@/services/api/apiClient";

function UnsubscribeInner() {
  const searchParams = useSearchParams();
  const token = searchParams.get("token") || "";
  const [status, setStatus] = useState<"loading" | "ok" | "error">(
    token ? "loading" : "error",
  );
  const [message, setMessage] = useState(
    token ? "" : "Link de descadastramento inválido.",
  );

  useEffect(() => {
    if (!token) return;
    let cancelled = false;
    void (async () => {
      try {
        const res = await newsletterApi.unsubscribe(token);
        if (cancelled) return;
        setStatus("ok");
        setMessage(res.message);
      } catch (err) {
        if (cancelled) return;
        setStatus("error");
        setMessage(
          err instanceof ApiError
            ? err.userMessage
            : "Não foi possível concluir o descadastramento.",
        );
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [token]);

  return (
    <div className="mx-auto max-w-md px-4 py-12 sm:px-6 text-center">
      <h1 className="font-serif text-3xl text-esotera-secondary">Newsletter</h1>
      <p
        className={`mt-4 text-sm ${
          status === "error" ? "text-esotera-error" : "text-esotera-muted"
        }`}
        role={status === "error" ? "alert" : "status"}
      >
        {status === "loading" ? "Processando…" : message}
      </p>
      <p className="mt-8 text-sm">
        <Link href="/" className="text-esotera-primary hover:underline">
          Voltar ao início
        </Link>
      </p>
    </div>
  );
}

export default function NewsletterUnsubscribePage() {
  return (
    <Suspense
      fallback={
        <div className="px-4 py-12 text-center text-esotera-muted">Carregando…</div>
      }
    >
      <UnsubscribeInner />
    </Suspense>
  );
}
