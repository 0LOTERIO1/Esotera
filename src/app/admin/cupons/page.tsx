"use client";

import { useCallback, useEffect, useState } from "react";
import { Button } from "@/components/ui/Button";
import { FormField, inputClassName } from "@/components/ui/FormField";
import { ConfirmModal } from "@/components/ui/ConfirmModal";
import { useToastStore } from "@/stores/toastStore";
import { isApiMode } from "@/config/dataMode";
import { getCouponRepository } from "@/services/repositories";
import { ApiError } from "@/services/api/apiClient";
import { formatCurrency } from "@/utils/format";
import type { AdminCouponDto } from "@/services/api/couponsApi";

type ArchiveFilter = "active" | "archived" | "all";

const emptyForm = {
  code: "",
  discountAmount: "5",
  minPurchase: "30",
  maxTotalUses: "",
  oneUsePerCustomer: true,
  isActive: true,
  validFromUtc: "",
  validUntilUtc: "",
};

type FormState = typeof emptyForm;

function toIsoOrNull(value: string): string | null {
  if (!value.trim()) return null;
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? null : d.toISOString();
}

export default function AdminCouponsPage() {
  const push = useToastStore((s) => s.push);
  const apiMode = isApiMode();
  const [coupons, setCoupons] = useState<AdminCouponDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [archivedFilter, setArchivedFilter] = useState<ArchiveFilter>("active");
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<AdminCouponDto | null>(null);
  const [form, setForm] = useState<FormState>(emptyForm);
  const [formErrors, setFormErrors] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [archiveTarget, setArchiveTarget] = useState<AdminCouponDto | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const repo = getCouponRepository();
      if (!repo.listAdmin) {
        setError("Administração de cupons indisponível neste modo.");
        setCoupons([]);
        return;
      }
      const list = await repo.listAdmin({ archived: archivedFilter });
      setCoupons(list);
    } catch (err) {
      setError(
        err instanceof ApiError
          ? err.userMessage
          : err instanceof Error
            ? err.message
            : "Erro ao carregar cupons.",
      );
    } finally {
      setLoading(false);
    }
  }, [archivedFilter]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void load();
    }, 0);
    return () => window.clearTimeout(timer);
  }, [load]);

  function openCreate() {
    setEditing(null);
    setForm(emptyForm);
    setFormErrors({});
    setOpen(true);
  }

  function openEdit(coupon: AdminCouponDto) {
    setEditing(coupon);
    setForm({
      code: coupon.code,
      discountAmount: String(coupon.discountAmount),
      minPurchase: String(coupon.minPurchase),
      maxTotalUses:
        coupon.maxTotalUses != null ? String(coupon.maxTotalUses) : "",
      oneUsePerCustomer: coupon.oneUsePerCustomer,
      isActive: coupon.isActive,
      validFromUtc: coupon.validFromUtc
        ? coupon.validFromUtc.slice(0, 16)
        : "",
      validUntilUtc: coupon.validUntilUtc
        ? coupon.validUntilUtc.slice(0, 16)
        : "",
    });
    setFormErrors({});
    setOpen(true);
  }

  function validateForm(): boolean {
    const errors: Record<string, string> = {};
    if (!form.code.trim()) errors.code = "Código é obrigatório.";
    const discount = Number(form.discountAmount);
    if (!(discount > 0)) errors.discountAmount = "Desconto deve ser maior que zero.";
    const min = Number(form.minPurchase);
    if (Number.isNaN(min) || min < 0)
      errors.minPurchase = "Compra mínima inválida.";
    if (form.maxTotalUses.trim()) {
      const max = Number(form.maxTotalUses);
      if (!Number.isInteger(max) || max <= 0)
        errors.maxTotalUses = "Limite global deve ser um inteiro maior que zero.";
    }
    const from = toIsoOrNull(form.validFromUtc);
    const until = toIsoOrNull(form.validUntilUtc);
    if (form.validFromUtc && !from) errors.validFromUtc = "Data inicial inválida.";
    if (form.validUntilUtc && !until) errors.validUntilUtc = "Data final inválida.";
    if (from && until && from > until)
      errors.validUntilUtc = "Data inicial deve ser ≤ data final.";
    setFormErrors(errors);
    return Object.keys(errors).length === 0;
  }

  async function save() {
    if (!validateForm()) return;
    const repo = getCouponRepository();
    setSaving(true);
    try {
      const maxTotalUses = form.maxTotalUses.trim()
        ? Number(form.maxTotalUses)
        : null;
      if (editing) {
        if (!repo.update) throw new Error("Edição indisponível.");
        await repo.update(editing.id, {
          code: form.code,
          discountAmount: Number(form.discountAmount),
          minPurchase: Number(form.minPurchase),
          oneUsePerCustomer: form.oneUsePerCustomer,
          maxTotalUses,
          clearMaxTotalUses: maxTotalUses == null,
          isActive: form.isActive,
          validFromUtc: toIsoOrNull(form.validFromUtc),
          validUntilUtc: toIsoOrNull(form.validUntilUtc),
          clearValidFrom: !form.validFromUtc.trim(),
          clearValidUntil: !form.validUntilUtc.trim(),
        });
        push("success", "Cupom atualizado.");
      } else {
        if (!repo.create) throw new Error("Criação indisponível.");
        await repo.create({
          code: form.code,
          discountAmount: Number(form.discountAmount),
          minPurchase: Number(form.minPurchase),
          oneUsePerCustomer: form.oneUsePerCustomer,
          maxTotalUses,
          isActive: form.isActive,
          validFromUtc: toIsoOrNull(form.validFromUtc),
          validUntilUtc: toIsoOrNull(form.validUntilUtc),
        });
        push("success", "Cupom criado.");
      }
      setOpen(false);
      await load();
    } catch (err) {
      push(
        "error",
        err instanceof ApiError
          ? err.userMessage
          : err instanceof Error
            ? err.message
            : "Não foi possível salvar o cupom.",
      );
    } finally {
      setSaving(false);
    }
  }

  async function runAction(
    id: string,
    action: "activate" | "deactivate" | "archive" | "restore",
  ) {
    const repo = getCouponRepository();
    setBusyId(id);
    try {
      if (action === "activate") await repo.activate?.(id);
      if (action === "deactivate") await repo.deactivate?.(id);
      if (action === "archive") await repo.archive?.(id);
      if (action === "restore") await repo.restore?.(id);
      push(
        "success",
        action === "archive"
          ? "Cupom arquivado."
          : action === "restore"
            ? "Cupom restaurado."
            : action === "activate"
              ? "Cupom ativado."
              : "Cupom desativado.",
      );
      await load();
    } catch (err) {
      push(
        "error",
        err instanceof ApiError
          ? err.userMessage
          : err instanceof Error
            ? err.message
            : "Falha na operação.",
      );
    } finally {
      setBusyId(null);
      setArchiveTarget(null);
    }
  }

  return (
    <div>
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="font-serif text-3xl text-esotera-secondary">Cupons</h1>
          <p className="mt-1 text-sm text-esotera-muted">
            {apiMode
              ? "Gerencie cupons reais pela API. Uma utilização por cliente; limite global opcional."
              : "Dados locais no navegador (modo mock)."}
          </p>
        </div>
        <Button type="button" onClick={openCreate}>
          Novo cupom
        </Button>
      </div>

      <div className="mt-4 flex flex-wrap gap-2">
        {(
          [
            ["active", "Ativos"],
            ["archived", "Arquivados"],
            ["all", "Todos"],
          ] as const
        ).map(([value, label]) => (
          <Button
            key={value}
            type="button"
            variant={archivedFilter === value ? "primary" : "secondary"}
            onClick={() => setArchivedFilter(value)}
          >
            {label}
          </Button>
        ))}
      </div>

      {loading ? (
        <p className="mt-6 text-sm text-esotera-muted">Carregando cupons…</p>
      ) : error ? (
        <p className="mt-6 text-sm text-red-700" role="alert">
          {error}
        </p>
      ) : coupons.length === 0 ? (
        <p className="mt-6 text-sm text-esotera-muted">Nenhum cupom nesta lista.</p>
      ) : (
        <div className="mt-6 space-y-3">
          {coupons.map((c) => (
            <div
              key={c.id}
              className="rounded-lg border border-esotera-border p-4"
            >
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <p className="font-serif text-xl text-esotera-primary">
                    {c.code}
                  </p>
                  <p className="mt-1 text-sm text-esotera-muted">
                    {formatCurrency(c.discountAmount)} · mín.{" "}
                    {formatCurrency(c.minPurchase)} ·{" "}
                    {c.isArchived
                      ? "Arquivado"
                      : c.isActive
                        ? "Ativo"
                        : "Inativo"}
                    {c.maxTotalUses != null
                      ? ` · ${c.usageCount}/${c.maxTotalUses} usos`
                      : ` · ${c.usageCount} uso(s)`}
                  </p>
                </div>
                <div className="flex flex-wrap gap-2">
                  {!c.isArchived ? (
                    <>
                      <Button
                        type="button"
                        variant="secondary"
                        disabled={busyId === c.id}
                        onClick={() => openEdit(c)}
                      >
                        Editar
                      </Button>
                      <Button
                        type="button"
                        variant="secondary"
                        disabled={busyId === c.id}
                        onClick={() =>
                          void runAction(
                            c.id,
                            c.isActive ? "deactivate" : "activate",
                          )
                        }
                      >
                        {c.isActive ? "Desativar" : "Ativar"}
                      </Button>
                      <Button
                        type="button"
                        variant="ghost"
                        disabled={busyId === c.id}
                        onClick={() => setArchiveTarget(c)}
                      >
                        Arquivar
                      </Button>
                    </>
                  ) : (
                    <Button
                      type="button"
                      variant="secondary"
                      disabled={busyId === c.id}
                      onClick={() => void runAction(c.id, "restore")}
                    >
                      Restaurar
                    </Button>
                  )}
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {open ? (
        <div className="fixed inset-0 z-40 flex items-center justify-center bg-black/40 p-4">
          <div className="max-h-[90vh] w-full max-w-lg overflow-y-auto rounded-lg bg-esotera-surface p-5 shadow-lg">
            <h2 className="font-serif text-2xl text-esotera-secondary">
              {editing ? "Editar cupom" : "Novo cupom"}
            </h2>
            <div className="mt-4 grid gap-3">
              <FormField label="Código" id="code" error={formErrors.code}>
                <input
                  id="code"
                  className={inputClassName}
                  value={form.code}
                  onChange={(e) =>
                    setForm({ ...form, code: e.target.value.toUpperCase() })
                  }
                />
              </FormField>
              <FormField
                label="Desconto (R$)"
                id="discount"
                error={formErrors.discountAmount}
              >
                <input
                  id="discount"
                  className={inputClassName}
                  value={form.discountAmount}
                  onChange={(e) =>
                    setForm({ ...form, discountAmount: e.target.value })
                  }
                />
              </FormField>
              <FormField
                label="Compra mínima (R$)"
                id="min"
                error={formErrors.minPurchase}
              >
                <input
                  id="min"
                  className={inputClassName}
                  value={form.minPurchase}
                  onChange={(e) =>
                    setForm({ ...form, minPurchase: e.target.value })
                  }
                />
              </FormField>
              <FormField
                label="Limite global (opcional)"
                id="maxUses"
                error={formErrors.maxTotalUses}
              >
                <input
                  id="maxUses"
                  className={inputClassName}
                  value={form.maxTotalUses}
                  placeholder="Ilimitado"
                  onChange={(e) =>
                    setForm({ ...form, maxTotalUses: e.target.value })
                  }
                />
              </FormField>
              <FormField
                label="Válido de (opcional)"
                id="from"
                error={formErrors.validFromUtc}
              >
                <input
                  id="from"
                  type="datetime-local"
                  className={inputClassName}
                  value={form.validFromUtc}
                  onChange={(e) =>
                    setForm({ ...form, validFromUtc: e.target.value })
                  }
                />
              </FormField>
              <FormField
                label="Válido até (opcional)"
                id="until"
                error={formErrors.validUntilUtc}
              >
                <input
                  id="until"
                  type="datetime-local"
                  className={inputClassName}
                  value={form.validUntilUtc}
                  onChange={(e) =>
                    setForm({ ...form, validUntilUtc: e.target.value })
                  }
                />
              </FormField>
              <label className="flex items-center gap-2 text-sm text-esotera-muted">
                <input
                  type="checkbox"
                  checked={form.oneUsePerCustomer}
                  onChange={(e) =>
                    setForm({ ...form, oneUsePerCustomer: e.target.checked })
                  }
                />
                Uma utilização por cliente
              </label>
              <label className="flex items-center gap-2 text-sm text-esotera-muted">
                <input
                  type="checkbox"
                  checked={form.isActive}
                  onChange={(e) =>
                    setForm({ ...form, isActive: e.target.checked })
                  }
                />
                Ativo
              </label>
              <p className="text-xs text-esotera-muted">
                O cupom nunca reduz o frete diretamente. Contagem de usos não é
                editável.
              </p>
            </div>
            <div className="mt-5 flex justify-end gap-2">
              <Button
                type="button"
                variant="secondary"
                onClick={() => setOpen(false)}
                disabled={saving}
              >
                Cancelar
              </Button>
              <Button type="button" onClick={() => void save()} disabled={saving}>
                {saving ? "Salvando…" : "Salvar"}
              </Button>
            </div>
          </div>
        </div>
      ) : null}

      <ConfirmModal
        open={Boolean(archiveTarget)}
        title="Arquivar cupom"
        description={
          archiveTarget
            ? `Arquivar ${archiveTarget.code}? Ele deixará de ser utilizável.`
            : ""
        }
        confirmLabel="Arquivar"
        onCancel={() => setArchiveTarget(null)}
        onConfirm={() => {
          if (archiveTarget) void runAction(archiveTarget.id, "archive");
        }}
      />
    </div>
  );
}
