"use client";

import { useEffect, useState } from "react";
import { useSettingsStore } from "@/stores/settingsStore";
import { FREE_SHIPPING_STATES } from "@/config/shipping";
import { FormField, inputClassName } from "@/components/ui/FormField";
import { Button } from "@/components/ui/Button";
import { useToastStore } from "@/stores/toastStore";
import { isApiMode } from "@/config/dataMode";
import { getSettingsRepository } from "@/services/repositories";
import { ApiError } from "@/services/api/apiClient";
import type { StoreSettings } from "@/types";

export default function AdminSettingsPage() {
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
      next.shippingSubsidy.amount < 0
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
                });
                push("info", "Configurações restauradas ao padrão.");
              }}
            >
              Restaurar padrão
            </Button>
          ) : null}
        </div>
      </div>
    </div>
  );
}
