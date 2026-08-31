"use client";

import { Suspense, useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import { useSettingsStore } from "@/stores/settingsStore";
import { FREE_SHIPPING_STATES } from "@/config/shipping";
import { FormField, inputClassName } from "@/components/ui/FormField";
import { Button } from "@/components/ui/Button";
import { useToastStore } from "@/stores/toastStore";
import { isApiMode } from "@/config/dataMode";
import { getSettingsRepository } from "@/services/repositories";
import { ApiError } from "@/services/api/apiClient";
import {
  melhorEnvioApi,
  melhorEnvioErrorMessage,
  type MelhorEnvioDiagnosticsDto,
  type MelhorEnvioStatusDto,
} from "@/services/api/melhorEnvioApi";
import type { StoreSettings } from "@/types";

function formatUtc(iso: string | null): string {
  if (!iso) return "—";
  try {
    return new Date(iso).toLocaleString("pt-BR", { timeZone: "UTC" }) + " UTC";
  } catch {
    return "—";
  }
}

function yesNo(value: boolean | null | undefined): string {
  if (value === null || value === undefined) return "não verificado";
  return value ? "sim" : "não";
}

function MelhorEnvioSection() {
  const apiMode = isApiMode();
  const push = useToastStore((s) => s.push);
  const searchParams = useSearchParams();
  const [status, setStatus] = useState<MelhorEnvioStatusDto | null>(null);
  const [loading, setLoading] = useState(apiMode);
  const [connecting, setConnecting] = useState(false);
  const [diagnostics, setDiagnostics] =
    useState<MelhorEnvioDiagnosticsDto | null>(null);
  const [diagnosticsBusy, setDiagnosticsBusy] = useState<
    "config" | "probe" | null
  >(null);

  useEffect(() => {
    const me = searchParams.get("me");
    if (!me) return;
    if (me === "connected") {
      push("success", "Melhor Envio conectado com sucesso.");
    } else if (me === "error") {
      push("error", melhorEnvioErrorMessage(searchParams.get("reason")));
    }
    // Limpa a query da barra sem recarregar o restante do estado.
    if (typeof window !== "undefined") {
      const url = new URL(window.location.href);
      url.searchParams.delete("me");
      url.searchParams.delete("reason");
      window.history.replaceState({}, "", url.pathname + url.search);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- toast once from callback query
  }, []);

  useEffect(() => {
    if (!apiMode) {
      return;
    }
    let cancelled = false;
    void (async () => {
      setLoading(true);
      try {
        const next = await melhorEnvioApi.getStatus();
        if (!cancelled) setStatus(next);
      } catch (err) {
        if (!cancelled) {
          push(
            "error",
            err instanceof ApiError
              ? err.userMessage
              : "Não foi possível carregar o status do Melhor Envio.",
          );
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [apiMode, push]);

  async function connect() {
    setConnecting(true);
    try {
      const { authorizationUrl } = await melhorEnvioApi.startAuthorize();
      window.location.assign(authorizationUrl);
    } catch (err) {
      push(
        "error",
        err instanceof ApiError
          ? err.userMessage
          : "Não foi possível iniciar a conexão com o Melhor Envio.",
      );
      setConnecting(false);
    }
  }

  async function runDiagnostics(probe: boolean) {
    setDiagnosticsBusy(probe ? "probe" : "config");
    try {
      const next = await melhorEnvioApi.getDiagnostics(probe);
      setDiagnostics(next);
      setStatus(next.connection);
    } catch (err) {
      push(
        "error",
        err instanceof ApiError
          ? err.userMessage
          : "Não foi possível executar o diagnóstico do Melhor Envio.",
      );
    } finally {
      setDiagnosticsBusy(null);
    }
  }

  const environmentLabel =
    diagnostics?.configuredEnvironment ?? status?.environment ?? "ambiente não verificado";

  if (!apiMode) {
    return (
      <section className="mt-10 max-w-xl border-t border-esotera-secondary/15 pt-8">
        <h2 className="font-serif text-2xl text-esotera-secondary">
          Melhor Envio
        </h2>
        <p className="mt-2 text-sm text-esotera-muted">
          Disponível apenas no modo API (conexão OAuth).
        </p>
      </section>
    );
  }

  return (
    <section className="mt-10 max-w-xl border-t border-esotera-secondary/15 pt-8">
      <h2 className="font-serif text-2xl text-esotera-secondary">
        Melhor Envio
      </h2>
      <p className="mt-1 text-sm text-esotera-muted">
        Conexão OAuth Melhor Envio ({environmentLabel}) — escopo
        shipping-calculate. A cotação real depende
        da flag &quot;Cotação Melhor Envio ativa&quot; nas configurações abaixo —
        independente deste status OAuth e de MELHOR_ENVIO_ENABLED.
      </p>

      {loading ? (
        <p className="mt-4 text-sm text-esotera-muted">Carregando status…</p>
      ) : (
        <div className="mt-4 space-y-2 text-sm text-esotera-muted">
          <p>
            Status:{" "}
            <span className="text-esotera-secondary">
              {status?.connected ? "Conectado" : "Desconectado"}
            </span>
          </p>
          {status?.connected ? (
            <>
              <p>Ambiente: {status.environment ?? "—"}</p>
              <p>Escopos: {status.scopes ?? "—"}</p>
              <p>
                Access token válido até:{" "}
                {formatUtc(status.accessTokenExpiresAtUtc)}
              </p>
              <p>
                Refresh válido até:{" "}
                {formatUtc(status.refreshTokenExpiresAtUtc)}
              </p>
              {status.needsReauthorization ? (
                <p className="text-esotera-secondary">
                  É necessário reconectar (refresh expirado).
                </p>
              ) : null}
            </>
          ) : (
            <p>
              Servidor configurado:{" "}
              {status?.configured ? "sim" : "não — defina as variáveis no Render"}
            </p>
          )}
          <div className="pt-3">
            <Button
              type="button"
              onClick={() => void connect()}
              disabled={connecting || status?.configured === false}
            >
              {connecting ? "Redirecionando…" : "Conectar Melhor Envio"}
            </Button>
          </div>

          <div className="mt-6 border-t border-esotera-secondary/15 pt-4">
            <h3 className="text-sm font-medium text-esotera-secondary">
              Diagnóstico
            </h3>
            <div className="mt-3 flex flex-wrap gap-3">
              <Button
                type="button"
                variant="secondary"
                onClick={() => void runDiagnostics(false)}
                disabled={diagnosticsBusy !== null}
              >
                {diagnosticsBusy === "config"
                  ? "Verificando…"
                  : "Verificar configuração"}
              </Button>
              <Button
                type="button"
                variant="secondary"
                onClick={() => void runDiagnostics(true)}
                disabled={diagnosticsBusy !== null}
              >
                {diagnosticsBusy === "probe"
                  ? "Testando…"
                  : "Testar cotação segura"}
              </Button>
            </div>
            <p className="mt-2 text-xs text-esotera-muted">
              &quot;Verificar configuração&quot; não chama o Melhor Envio.
              &quot;Testar cotação segura&quot; faz apenas uma cotação fixa de
              teste (somente leitura): não cria envio, não gera etiqueta e não
              compra frete.
            </p>

            {diagnostics ? (
              <dl className="mt-4 space-y-1 text-sm text-esotera-muted">
                <div>
                  Ambiente configurado:{" "}
                  <span className="text-esotera-secondary">
                    {diagnostics.configuredEnvironment}
                  </span>
                </div>
                <div>Base URL: {diagnostics.baseUrl}</div>
                <div>Servidor configurado: {yesNo(diagnostics.configured)}</div>
                <div>Token presente: {yesNo(diagnostics.tokenPresent)}</div>
                <div>
                  Autenticação validada: {yesNo(diagnostics.canAuthenticate)}
                </div>
                <div>
                  Conexão salva — ambiente:{" "}
                  {diagnostics.connection.environment ?? "—"}
                </div>
                <div>
                  Conexão salva — status:{" "}
                  {diagnostics.connection.connected
                    ? "conectado"
                    : "desconectado"}
                  {diagnostics.connection.needsReauthorization
                    ? " (reautorizar)"
                    : ""}
                  {diagnostics.connection.environmentMismatch
                    ? " (ambiente divergente)"
                    : ""}
                </div>
                <div>
                  Conexão salva — escopos:{" "}
                  {diagnostics.connection.scopes ?? "—"}
                </div>
                <div>
                  Access token válido até:{" "}
                  {formatUtc(diagnostics.connection.accessTokenExpiresAtUtc)}
                </div>
                <div>
                  Refresh token válido até:{" "}
                  {formatUtc(diagnostics.connection.refreshTokenExpiresAtUtc)}
                </div>
                <div className="pt-1 text-esotera-secondary">
                  {diagnostics.message}
                </div>
              </dl>
            ) : null}
          </div>
        </div>
      )}
    </section>
  );
}

function AdminSettingsForm() {
  const settings = useSettingsStore((s) => s.settings);
  const saveSettings = useSettingsStore((s) => s.saveSettings);
  const resetSettings = useSettingsStore((s) => s.resetSettings);
  const push = useToastStore((s) => s.push);
  const apiMode = isApiMode();
  const [loading, setLoading] = useState(apiMode);
  const [saving, setSaving] = useState(false);
  const [form, setForm] = useState({
    storeName: settings.storeName,
    freeShippingMin: String(settings.freeShippingMin),
    freeShippingStates: settings.freeShippingStates.join(","),
    j3Price: String(settings.j3Price),
    j3CutoffHour: String(settings.j3CutoffHour),
    subsidyEnabled: settings.shippingSubsidy.enabled,
    subsidyAmount: String(settings.shippingSubsidy.amount),
    shippingOriginCep: settings.shippingOriginCep ?? "08061-420",
    packageLengthCm: String(settings.packageLengthCm ?? 16),
    packageWidthCm: String(settings.packageWidthCm ?? 11),
    packageHeightCm: String(settings.packageHeightCm ?? 6),
    packageWeightGrams: String(settings.packageWeightGrams ?? 400),
    melhorEnvioQuoteEnabled: settings.melhorEnvioQuoteEnabled ?? false,
  });

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      if (!apiMode) {
        setForm({
          storeName: settings.storeName,
          freeShippingMin: String(settings.freeShippingMin),
          freeShippingStates: settings.freeShippingStates.join(","),
          j3Price: String(settings.j3Price),
          j3CutoffHour: String(settings.j3CutoffHour),
          subsidyEnabled: settings.shippingSubsidy.enabled,
          subsidyAmount: String(settings.shippingSubsidy.amount),
          shippingOriginCep: settings.shippingOriginCep ?? "08061-420",
          packageLengthCm: String(settings.packageLengthCm ?? 16),
          packageWidthCm: String(settings.packageWidthCm ?? 11),
          packageHeightCm: String(settings.packageHeightCm ?? 6),
          packageWeightGrams: String(settings.packageWeightGrams ?? 400),
          melhorEnvioQuoteEnabled: settings.melhorEnvioQuoteEnabled ?? false,
        });
        setLoading(false);
        return;
      }
      setLoading(true);
      try {
        const admin = await getSettingsRepository().getAdmin();
        if (cancelled) return;
        setForm({
          storeName: admin.storeName,
          freeShippingMin: String(admin.freeShippingMin),
          freeShippingStates: admin.freeShippingStates.join(","),
          j3Price: String(admin.j3Price),
          j3CutoffHour: String(admin.j3CutoffHour),
          subsidyEnabled: admin.shippingSubsidy.enabled,
          subsidyAmount: String(admin.shippingSubsidy.amount),
          shippingOriginCep: admin.shippingOriginCep ?? "08061-420",
          packageLengthCm: String(admin.packageLengthCm ?? 16),
          packageWidthCm: String(admin.packageWidthCm ?? 11),
          packageHeightCm: String(admin.packageHeightCm ?? 6),
          packageWeightGrams: String(admin.packageWeightGrams ?? 400),
          melhorEnvioQuoteEnabled: admin.melhorEnvioQuoteEnabled ?? false,
        });
        useSettingsStore.setState({ settings: admin });
      } catch (err) {
        if (!cancelled) {
          push(
            "error",
            err instanceof ApiError
              ? err.userMessage
              : "Não foi possível carregar as configurações.",
          );
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps -- load once on mount / mode
  }, [apiMode]);

  async function save() {
    const states = form.freeShippingStates
      .split(",")
      .map((s) => s.trim().toUpperCase())
      .filter(Boolean);

    if (!form.storeName.trim()) {
      push("error", "Nome da loja é obrigatório.");
      return;
    }
    if (states.length === 0) {
      push("error", "Informe ao menos um estado elegível.");
      return;
    }
    if (states.some((s) => !/^[A-Z]{2}$/.test(s))) {
      push("error", "Siglas de estado inválidas.");
      return;
    }

    const next: StoreSettings = {
      ...settings,
      storeName: form.storeName.trim(),
      freeShippingMin: Number(form.freeShippingMin),
      freeShippingStates: [...new Set(states)],
      j3Price: Number(form.j3Price),
      j3CutoffHour: Number(form.j3CutoffHour),
      shippingSubsidy: {
        enabled: form.subsidyEnabled,
        amount: Number(form.subsidyAmount),
      },
      shippingOriginCep: form.shippingOriginCep.trim(),
      packageLengthCm: Number(form.packageLengthCm),
      packageWidthCm: Number(form.packageWidthCm),
      packageHeightCm: Number(form.packageHeightCm),
      packageWeightGrams: Number(form.packageWeightGrams),
      melhorEnvioQuoteEnabled: form.melhorEnvioQuoteEnabled,
    };

    if (
      Number.isNaN(next.freeShippingMin) ||
      next.freeShippingMin < 0 ||
      Number.isNaN(next.j3Price) ||
      next.j3Price < 0 ||
      !Number.isInteger(next.j3CutoffHour) ||
      next.j3CutoffHour < 0 ||
      next.j3CutoffHour > 23 ||
      Number.isNaN(next.shippingSubsidy.amount) ||
      next.shippingSubsidy.amount < 0 ||
      Number.isNaN(next.packageLengthCm!) ||
      next.packageLengthCm! < 1 ||
      Number.isNaN(next.packageWidthCm!) ||
      next.packageWidthCm! < 1 ||
      Number.isNaN(next.packageHeightCm!) ||
      next.packageHeightCm! < 1 ||
      !Number.isInteger(next.packageWeightGrams!) ||
      next.packageWeightGrams! < 1
    ) {
      push("error", "Revise os valores numéricos.");
      return;
    }

    setSaving(true);
    try {
      await saveSettings(next);
      push(
        "success",
        apiMode
          ? "Configurações salvas na API."
          : "Configurações atualizadas na simulação.",
      );
    } catch (err) {
      push(
        "error",
        err instanceof ApiError
          ? err.userMessage
          : "Não foi possível salvar as configurações.",
      );
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return (
      <div>
        <h1 className="font-serif text-3xl text-esotera-secondary">
          Configurações
        </h1>
        <p className="mt-4 text-sm text-esotera-muted">Carregando…</p>
      </div>
    );
  }

  return (
    <div>
      <h1 className="font-serif text-3xl text-esotera-secondary">Configurações</h1>
      <p className="mt-1 text-sm text-esotera-muted">
        {apiMode
          ? "Valores comerciais da loja (API). Cupons são gerenciados em Cupons."
          : "Alterações persistem no localStorage deste navegador. Cupons em Cupons."}
      </p>

      <div className="mt-6 grid max-w-xl gap-4">
        <FormField label="Nome da loja" id="storeName">
          <input
            id="storeName"
            className={inputClassName}
            value={form.storeName}
            onChange={(e) => setForm({ ...form, storeName: e.target.value })}
          />
        </FormField>
        <FormField label="Valor mínimo do frete grátis" id="freeMin">
          <input
            id="freeMin"
            className={inputClassName}
            value={form.freeShippingMin}
            onChange={(e) =>
              setForm({ ...form, freeShippingMin: e.target.value })
            }
          />
        </FormField>
        <FormField
          label="Estados elegíveis (UF separados por vírgula)"
          id="freeStates"
        >
          <input
            id="freeStates"
            className={inputClassName}
            value={form.freeShippingStates}
            onChange={(e) =>
              setForm({ ...form, freeShippingStates: e.target.value })
            }
          />
        </FormField>
        <FormField label="Preço da J3" id="j3Price">
          <input
            id="j3Price"
            className={inputClassName}
            value={form.j3Price}
            onChange={(e) => setForm({ ...form, j3Price: e.target.value })}
          />
        </FormField>
        <FormField label="Horário limite da J3 (0–23)" id="j3Cutoff">
          <input
            id="j3Cutoff"
            className={inputClassName}
            value={form.j3CutoffHour}
            onChange={(e) => setForm({ ...form, j3CutoffHour: e.target.value })}
          />
        </FormField>
        <label className="flex items-center gap-2 text-sm text-esotera-muted">
          <input
            type="checkbox"
            checked={form.subsidyEnabled}
            onChange={(e) =>
              setForm({ ...form, subsidyEnabled: e.target.checked })
            }
          />
          Subsídio de frete habilitado
        </label>
        <FormField label="Valor do subsídio" id="subsidyAmount">
          <input
            id="subsidyAmount"
            className={inputClassName}
            value={form.subsidyAmount}
            onChange={(e) =>
              setForm({ ...form, subsidyAmount: e.target.value })
            }
            disabled={!form.subsidyEnabled}
          />
        </FormField>

        <h2 className="mt-4 font-serif text-xl text-esotera-secondary">
          Pacote e cotação Melhor Envio
        </h2>
        <FormField label="CEP de origem" id="shippingOriginCep">
          <input
            id="shippingOriginCep"
            className={inputClassName}
            value={form.shippingOriginCep}
            onChange={(e) =>
              setForm({ ...form, shippingOriginCep: e.target.value })
            }
          />
        </FormField>
        <div className="grid grid-cols-2 gap-3">
          <FormField label="Comprimento (cm)" id="packageLengthCm">
            <input
              id="packageLengthCm"
              className={inputClassName}
              value={form.packageLengthCm}
              onChange={(e) =>
                setForm({ ...form, packageLengthCm: e.target.value })
              }
            />
          </FormField>
          <FormField label="Largura (cm)" id="packageWidthCm">
            <input
              id="packageWidthCm"
              className={inputClassName}
              value={form.packageWidthCm}
              onChange={(e) =>
                setForm({ ...form, packageWidthCm: e.target.value })
              }
            />
          </FormField>
          <FormField label="Altura (cm)" id="packageHeightCm">
            <input
              id="packageHeightCm"
              className={inputClassName}
              value={form.packageHeightCm}
              onChange={(e) =>
                setForm({ ...form, packageHeightCm: e.target.value })
              }
            />
          </FormField>
          <FormField label="Peso (g)" id="packageWeightGrams">
            <input
              id="packageWeightGrams"
              className={inputClassName}
              value={form.packageWeightGrams}
              onChange={(e) =>
                setForm({ ...form, packageWeightGrams: e.target.value })
              }
            />
          </FormField>
        </div>
        <label className="flex items-center gap-2 text-sm text-esotera-muted">
          <input
            type="checkbox"
            checked={form.melhorEnvioQuoteEnabled}
            onChange={(e) =>
              setForm({ ...form, melhorEnvioQuoteEnabled: e.target.checked })
            }
          />
          Cotação Melhor Envio ativa
        </label>
        <p className="text-xs text-esotera-muted">
          Independente de MELHOR_ENVIO_ENABLED e do OAuth conectado. Padrão do
          pacote: 16×11×6 cm, 400 g, CEP 08061-420.
        </p>

        <div className="flex flex-wrap gap-2 pt-2">
          <Button type="button" onClick={() => void save()} disabled={saving}>
            {saving ? "Salvando…" : "Salvar"}
          </Button>
          {!apiMode ? (
            <Button
              type="button"
              variant="secondary"
              onClick={() => {
                resetSettings();
                setForm({
                  storeName: "Esotera",
                  freeShippingMin: "99.9",
                  freeShippingStates: FREE_SHIPPING_STATES.join(","),
                  j3Price: "12",
                  j3CutoffHour: "12",
                  subsidyEnabled: false,
                  subsidyAmount: "10",
                  shippingOriginCep: "08061-420",
                  packageLengthCm: "16",
                  packageWidthCm: "11",
                  packageHeightCm: "6",
                  packageWeightGrams: "400",
                  melhorEnvioQuoteEnabled: false,
                });
                push("info", "Configurações restauradas ao padrão.");
              }}
            >
              Restaurar padrão
            </Button>
          ) : null}
        </div>
      </div>

      <Suspense
        fallback={
          <p className="mt-10 text-sm text-esotera-muted">
            Carregando Melhor Envio…
          </p>
        }
      >
        <MelhorEnvioSection />
      </Suspense>
    </div>
  );
}

export default function AdminSettingsPage() {
  return <AdminSettingsForm />;
}
