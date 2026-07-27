"use client";

import { defaultCoupon } from "@/config/coupon";
import { useSettingsStore } from "@/stores/settingsStore";
import { formatCurrency } from "@/utils/format";

export default function AdminCouponsPage() {
  const settings = useSettingsStore((s) => s.settings);

  return (
    <div>
      <h1 className="font-serif text-3xl text-esotera-white">Cupons</h1>
      <p className="mt-1 text-sm text-esotera-muted">
        Cupom de demonstração. Valores editáveis em Configurações.
      </p>
      <div className="mt-6 rounded-lg border border-esotera-graphite p-5">
        <p className="font-serif text-2xl text-esotera-gold">{defaultCoupon.code}</p>
        <dl className="mt-4 space-y-2 text-sm text-esotera-muted">
          <div className="flex justify-between gap-4">
            <dt>Desconto</dt>
            <dd>{formatCurrency(settings.couponDiscount)}</dd>
          </div>
          <div className="flex justify-between gap-4">
            <dt>Compra mínima</dt>
            <dd>{formatCurrency(settings.couponMinPurchase)}</dd>
          </div>
          <div className="flex justify-between gap-4">
            <dt>Aplica no frete</dt>
            <dd>Não</dd>
          </div>
          <div className="flex justify-between gap-4">
            <dt>Limite</dt>
            <dd>1 utilização por cliente</dd>
          </div>
        </dl>
      </div>
    </div>
  );
}
