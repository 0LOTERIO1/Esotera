"use client";

import { useState } from "react";
import { useSettingsStore } from "@/stores/settingsStore";
import { FREE_SHIPPING_STATES } from "@/config/shipping";
import { FormField, inputClassName } from "@/components/ui/FormField";
import { Button } from "@/components/ui/Button";
import { useToastStore } from "@/stores/toastStore";

export default function AdminSettingsPage() {
  const settings = useSettingsStore((s) => s.settings);
  const updateSettings = useSettingsStore((s) => s.updateSettings);
  const resetSettings = useSettingsStore((s) => s.resetSettings);
  const push = useToastStore((s) => s.push);
  const [form, setForm] = useState({
    storeName: settings.storeName,
    freeShippingMin: String(settings.freeShippingMin),
    freeShippingStates: settings.freeShippingStates.join(","),
    j3Price: String(settings.j3Price),
    j3CutoffHour: String(settings.j3CutoffHour),
    couponDiscount: String(settings.couponDiscount),
    couponMinPurchase: String(settings.couponMinPurchase),
    subsidyEnabled: settings.shippingSubsidy.enabled,
    subsidyAmount: String(settings.shippingSubsidy.amount),
  });

  function save() {
    const states = form.freeShippingStates
      .split(",")
      .map((s) => s.trim().toUpperCase())
      .filter(Boolean);

    updateSettings({
      storeName: form.storeName.trim() || "Esotera",
      freeShippingMin: Number(form.freeShippingMin) || 99.9,
      freeShippingStates: states.length ? states : [...FREE_SHIPPING_STATES],
      j3Price: Number(form.j3Price) || 12,
      j3CutoffHour: Number(form.j3CutoffHour) || 12,
      couponDiscount: Number(form.couponDiscount) || 5,
      couponMinPurchase: Number(form.couponMinPurchase) || 30,
      shippingSubsidy: {
        enabled: form.subsidyEnabled,
        amount: Number(form.subsidyAmount) || 10,
      },
    });
    push("success", "Configurações atualizadas na simulação.");
  }

  return (
    <div>
      <h1 className="font-serif text-3xl text-esotera-white">Configurações</h1>
      <p className="mt-1 text-sm text-esotera-muted">
        Alterações persistem no localStorage deste navegador.
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
        <FormField label="Valor do cupom" id="couponDiscount">
          <input
            id="couponDiscount"
            className={inputClassName}
            value={form.couponDiscount}
            onChange={(e) =>
              setForm({ ...form, couponDiscount: e.target.value })
            }
          />
        </FormField>
        <FormField label="Compra mínima do cupom" id="couponMin">
          <input
            id="couponMin"
            className={inputClassName}
            value={form.couponMinPurchase}
            onChange={(e) =>
              setForm({ ...form, couponMinPurchase: e.target.value })
            }
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
          <Button type="button" onClick={save}>
            Salvar
          </Button>
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
                couponDiscount: "5",
                couponMinPurchase: "30",
                subsidyEnabled: false,
                subsidyAmount: "10",
              });
              push("info", "Configurações restauradas ao padrão.");
            }}
          >
            Restaurar padrão
          </Button>
        </div>
      </div>
    </div>
  );
}
