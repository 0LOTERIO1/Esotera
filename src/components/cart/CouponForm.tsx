"use client";

import { useState } from "react";
import { FormField, inputClassName } from "@/components/ui/FormField";
import { Button } from "@/components/ui/Button";
import { useCartStore } from "@/stores/cartStore";
import { useAuthStore } from "@/stores/authStore";
import { useCartTotals } from "@/hooks/useCartTotals";
import { getCouponRepository } from "@/services/repositories";
import { ApiError } from "@/services/api/apiClient";

export function CouponForm() {
  const [code, setCode] = useState("");
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const coupon = useCartStore((s) => s.coupon);
  const setCoupon = useCartStore((s) => s.setCoupon);
  const user = useAuthStore((s) => s.user);
  const { subtotal } = useCartTotals();

  async function apply() {
    setMessage(null);
    setError(null);
    setBusy(true);

    try {
      const repo = getCouponRepository();
      const result = await repo.validate({
        code,
        subtotal,
        userId: user?.id,
      });

      if (!result.ok) {
        setError(result.message);
        return;
      }

      setCoupon({ code: result.code, discountAmount: result.discountAmount });
      setMessage("Cupom aplicado com sucesso.");
      setCode("");
    } catch (err) {
      setError(
        err instanceof ApiError
          ? err.userMessage
          : err instanceof Error
            ? err.message
            : "Erro ao validar cupom.",
      );
    } finally {
      setBusy(false);
    }
  }

  function remove() {
    setCoupon(null);
    setMessage("Cupom removido.");
    setError(null);
  }

  return (
    <div className="space-y-3 rounded-lg border border-esotera-border p-4">
      <p className="text-sm font-medium text-esotera-text">Cupom de desconto</p>
      {coupon ? (
        <div className="flex items-center justify-between gap-3 text-sm">
          <span className="text-esotera-primary">{coupon.code}</span>
          <Button type="button" variant="ghost" onClick={remove}>
            Remover
          </Button>
        </div>
      ) : (
        <div className="flex flex-col gap-2 sm:flex-row">
          <FormField
            label="Código do cupom"
            id="coupon-code"
            error={error ?? undefined}
          >
            <input
              id="coupon-code"
              value={code}
              onChange={(e) => setCode(e.target.value.toUpperCase())}
              className={inputClassName}
              placeholder="DESCONTO5"
              aria-invalid={Boolean(error)}
              aria-describedby={error ? "coupon-code-error" : undefined}
            />
          </FormField>
          <div className="flex items-end">
            <Button
              type="button"
              variant="secondary"
              onClick={() => void apply()}
              className="w-full sm:w-auto"
              disabled={busy || !code.trim()}
            >
              {busy ? "Validando…" : "Aplicar"}
            </Button>
          </div>
        </div>
      )}
      {message ? (
        <p role="status" className="text-xs text-esotera-success">
          {message}
        </p>
      ) : null}
    </div>
  );
}
