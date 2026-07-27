"use client";

import { useState } from "react";
import { FormField, inputClassName } from "@/components/ui/FormField";
import { Button } from "@/components/ui/Button";
import { useCartStore } from "@/stores/cartStore";
import { useAuthStore } from "@/stores/authStore";
import { useSettingsStore } from "@/stores/settingsStore";
import { useCartTotals } from "@/hooks/useCartTotals";
import { mockCouponService } from "@/services/coupon/mockCouponService";

export function CouponForm() {
  const [code, setCode] = useState("");
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const coupon = useCartStore((s) => s.coupon);
  const setCoupon = useCartStore((s) => s.setCoupon);
  const user = useAuthStore((s) => s.user);
  const settings = useSettingsStore((s) => s.settings);
  const { subtotal } = useCartTotals();

  function apply() {
    setMessage(null);
    setError(null);
    const result = mockCouponService.validate({
      code,
      subtotal,
      userId: user?.id,
      discountAmount: settings.couponDiscount,
      minPurchase: settings.couponMinPurchase,
    });

    if (!result.ok) {
      setError(result.message);
      return;
    }

    setCoupon({ code: result.code, discountAmount: result.discountAmount });
    setMessage("Cupom aplicado com sucesso.");
    setCode("");
  }

  function remove() {
    setCoupon(null);
    setMessage("Cupom removido.");
    setError(null);
  }

  return (
    <div className="space-y-3 rounded-lg border border-esotera-graphite p-4">
      <p className="text-sm font-medium text-esotera-beige">Cupom de desconto</p>
      {coupon ? (
        <div className="flex items-center justify-between gap-3 text-sm">
          <span className="text-esotera-gold">{coupon.code}</span>
          <Button type="button" variant="ghost" onClick={remove}>
            Remover
          </Button>
        </div>
      ) : (
        <div className="flex flex-col gap-2 sm:flex-row">
          <FormField label="Código do cupom" id="coupon-code" error={error ?? undefined}>
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
            <Button type="button" variant="secondary" onClick={apply} className="w-full sm:w-auto">
              Aplicar
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
