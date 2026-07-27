"use client";

import { useEffect, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { ProductImage } from "@/components/ui/ProductImage";
import { useAuthStore } from "@/stores/authStore";
import { useCartStore } from "@/stores/cartStore";
import { useOrdersStore } from "@/stores/ordersStore";
import { useSettingsStore } from "@/stores/settingsStore";
import { useCartTotals } from "@/hooks/useCartTotals";
import { OrderSummary } from "@/components/cart/OrderSummary";
import { ShippingOptions } from "@/components/checkout/ShippingOptions";
import { PaymentOptions } from "@/components/checkout/PaymentOptions";
import { Button, ButtonLink } from "@/components/ui/Button";
import { FormField, inputClassName } from "@/components/ui/FormField";
import { EmptyState } from "@/components/ui/EmptyState";
import { brazilianStates } from "@/data/brazilianStates";
import { maskCep } from "@/utils/masks";
import { validateCep } from "@/utils/validation";
import {
  mockShippingService,
  qualifiesForFreeShipping,
} from "@/services/shipping/mockShippingService";
import type { Address, PaymentMethod } from "@/types";
import { storeConfig } from "@/config/store";
import { Price } from "@/components/ui/Price";
import { useToastStore } from "@/stores/toastStore";

const steps = [
  "Identificação",
  "Endereço",
  "Entrega",
  "Pagamento",
  "Revisão",
] as const;

export default function CheckoutPage() {
  const router = useRouter();
  const user = useAuthStore((s) => s.user);
  const hydrated = useAuthStore((s) => s.hydrated);
  const cartItems = useCartStore((s) => s.items);
  const coupon = useCartStore((s) => s.coupon);
  const clearCart = useCartStore((s) => s.clearCart);
  const createOrder = useOrdersStore((s) => s.createOrder);
  const settings = useSettingsStore((s) => s.settings);
  const { lines, subtotal, discount, productsTotal } = useCartTotals();
  const push = useToastStore((s) => s.push);

  const [step, setStep] = useState(0);
  const [addressDraft, setAddressDraft] = useState<Address | null>(null);
  const [selectedShippingId, setSelectedShippingId] = useState<string | null>(
    null,
  );
  const [paymentMethod, setPaymentMethod] = useState<PaymentMethod>("pix");
  const [installments, setInstallments] = useState(1);

  const address = addressDraft ?? user?.address ?? null;

  useEffect(() => {
    if (!hydrated) return;
    if (!user) {
      router.replace("/login?returnUrl=/checkout");
    }
  }, [hydrated, user, router]);

  const shippingOptions = useMemo(() => {
    if (step < 2 || !address || !validateCep(address.cep)) return [];
    return mockShippingService.quoteShipping({
      cep: address.cep,
      state: address.state,
      productsTotalAfterDiscount: productsTotal,
      settings,
    });
  }, [step, address, productsTotal, settings]);

  const selectedShipping =
    shippingOptions.find((o) => o.id === selectedShippingId) ??
    shippingOptions[0] ??
    null;

  const freeHint = useMemo(() => {
    if (!address) return undefined;
    if (qualifiesForFreeShipping(productsTotal, address.state, settings)) {
      return "Frete grátis disponível para este pedido (Sul/Sudeste acima do mínimo).";
    }
    return undefined;
  }, [address, productsTotal, settings]);

  function updateAddress(partial: Partial<Address>) {
    const base = address ?? {
      cep: "",
      street: "",
      number: "",
      neighborhood: "",
      city: "",
      state: "SP",
    };
    setAddressDraft({ ...base, ...partial });
  }
  if (!hydrated || !user) {
    return (
      <div className="px-4 py-16 text-center text-esotera-muted">
        Verificando autenticação…
      </div>
    );
  }

  if (!cartItems.length || !lines.length) {
    return (
      <div className="mx-auto max-w-6xl px-4 py-10 sm:px-6">
        <EmptyState
          title="Carrinho vazio"
          description="Adicione produtos antes de finalizar a compra."
          action={<ButtonLink href="/produtos">Ver produtos</ButtonLink>}
        />
      </div>
    );
  }

  function next() {
    if (step === 1 && address) {
      if (!validateCep(address.cep) || !address.street || !address.number) {
        push("error", "Preencha o endereço completo.");
        return;
      }
    }
    if (step === 2 && !selectedShipping) {
      push("error", "Selecione uma modalidade de entrega.");
      return;
    }
    setStep((s) => Math.min(s + 1, steps.length - 1));
  }

  function finish() {
    if (!address || !selectedShipping || !user) return;

    const order = createOrder({
      userId: user.id,
      customerName: user.name,
      customerEmail: user.email,
      customerPhone: user.phone,
      customerCpf: user.cpf,
      items: lines.map((l) => ({
        productId: l.productId,
        name: l.product.name,
        price: l.product.price,
        quantity: l.quantity,
        variation: l.variation,
        image: l.product.images[0],
      })),
      subtotal,
      discount,
      couponCode: coupon?.code,
      shippingOption: selectedShipping,
      address,
      paymentMethod,
      installments: paymentMethod === "card" ? installments : undefined,
    });

    push("success", "Pedido simulado criado com sucesso.");
    router.push(`/pedido-confirmado/${order.id}`);
    clearCart();
  }

  const shippingPrice = selectedShipping?.price ?? 0;

  return (
    <div className="mx-auto max-w-6xl px-4 py-10 sm:px-6">
      <h1 className="font-serif text-4xl text-esotera-white">Checkout</h1>
      <p className="mt-2 text-sm text-esotera-gold-soft">{storeConfig.demoNotice}</p>

      <ol className="mt-6 flex flex-wrap gap-2" aria-label="Etapas do checkout">
        {steps.map((label, index) => (
          <li
            key={label}
            className={`rounded-md border px-3 py-1.5 text-xs ${
              index === step
                ? "border-esotera-gold text-esotera-gold"
                : index < step
                  ? "border-esotera-success/40 text-esotera-beige"
                  : "border-esotera-graphite text-esotera-muted"
            }`}
          >
            {index + 1}. {label}
          </li>
        ))}
      </ol>

      <div className="mt-8 grid gap-8 lg:grid-cols-[1fr_320px]">
        <div className="space-y-6 rounded-lg border border-esotera-graphite p-5">
          {step === 0 ? (
            <div className="space-y-2 text-sm text-esotera-muted">
              <h2 className="font-serif text-2xl text-esotera-beige">
                Identificação
              </h2>
              <p>
                <strong className="text-esotera-beige">Nome:</strong> {user.name}
              </p>
              <p>
                <strong className="text-esotera-beige">E-mail:</strong>{" "}
                {user.email}
              </p>
              <p>
                <strong className="text-esotera-beige">CPF:</strong> {user.cpf}
              </p>
              <p>
                <strong className="text-esotera-beige">Telefone:</strong>{" "}
                {user.phone}
              </p>
            </div>
          ) : null}

          {step === 1 && address ? (
            <div className="grid gap-3 sm:grid-cols-2">
              <h2 className="font-serif text-2xl text-esotera-beige sm:col-span-2">
                Endereço
              </h2>
              <FormField label="CEP" id="chk-cep" required>
                <input
                  id="chk-cep"
                  className={inputClassName}
                  value={address.cep}
                  onChange={(e) =>
                    updateAddress({ cep: maskCep(e.target.value) })
                  }
                />
              </FormField>
              <FormField label="Estado" id="chk-state" required>
                <select
                  id="chk-state"
                  className={inputClassName}
                  value={address.state}
                  onChange={(e) =>
                    updateAddress({ state: e.target.value })
                  }
                >
                  {brazilianStates.map((s) => (
                    <option key={s.uf} value={s.uf}>
                      {s.uf}
                    </option>
                  ))}
                </select>
              </FormField>
              <div className="sm:col-span-2">
                <FormField label="Endereço" id="chk-street" required>
                  <input
                    id="chk-street"
                    className={inputClassName}
                    value={address.street}
                    onChange={(e) =>
                      updateAddress({ street: e.target.value })
                    }
                  />
                </FormField>
              </div>
              <FormField label="Número" id="chk-number" required>
                <input
                  id="chk-number"
                  className={inputClassName}
                  value={address.number}
                  onChange={(e) =>
                    updateAddress({ number: e.target.value })
                  }
                />
              </FormField>
              <FormField label="Complemento" id="chk-complement">
                <input
                  id="chk-complement"
                  className={inputClassName}
                  value={address.complement ?? ""}
                  onChange={(e) =>
                    updateAddress({ complement: e.target.value })
                  }
                />
              </FormField>
              <FormField label="Bairro" id="chk-neighborhood" required>
                <input
                  id="chk-neighborhood"
                  className={inputClassName}
                  value={address.neighborhood}
                  onChange={(e) =>
                    updateAddress({ neighborhood: e.target.value })
                  }
                />
              </FormField>
              <FormField label="Cidade" id="chk-city" required>
                <input
                  id="chk-city"
                  className={inputClassName}
                  value={address.city}
                  onChange={(e) =>
                    updateAddress({ city: e.target.value })
                  }
                />
              </FormField>
            </div>
          ) : null}

          {step === 2 ? (
            <div>
              <h2 className="mb-4 font-serif text-2xl text-esotera-beige">
                Entrega
              </h2>
              <ShippingOptions
                options={shippingOptions}
                selectedId={selectedShipping?.id}
                onSelect={(option) => setSelectedShippingId(option.id)}
                freeShippingHint={freeHint}
              />
            </div>
          ) : null}

          {step === 3 ? (
            <div>
              <h2 className="mb-4 font-serif text-2xl text-esotera-beige">
                Pagamento
              </h2>
              <PaymentOptions
                method={paymentMethod}
                installments={installments}
                total={productsTotal + shippingPrice}
                onMethodChange={setPaymentMethod}
                onInstallmentsChange={setInstallments}
              />
            </div>
          ) : null}

          {step === 4 ? (
            <div className="space-y-4 text-sm text-esotera-muted">
              <h2 className="font-serif text-2xl text-esotera-beige">Revisão</h2>
              <ul className="space-y-3">
                {lines.map((l) => (
                  <li
                    key={`${l.productId}-${l.variation ?? ""}`}
                    className="flex gap-3"
                  >
                    <div className="relative h-16 w-12 overflow-hidden rounded">
                      <ProductImage
                        src={l.product.images[0]}
                        alt=""
                        fill
                        className="object-cover"
                        sizes="48px"
                      />
                    </div>
                    <div>
                      <p className="text-esotera-beige">{l.product.name}</p>
                      <p>
                        {l.quantity} × <Price value={l.product.price} />
                      </p>
                    </div>
                  </li>
                ))}
              </ul>
              {address ? (
                <p>
                  Entrega em {address.street}, {address.number} — {address.city}/
                  {address.state}
                </p>
              ) : null}
              {selectedShipping ? (
                <p>
                  Frete: {selectedShipping.provider} — {selectedShipping.name} (
                  {selectedShipping.estimatedDays})
                </p>
              ) : null}
              <p>
                Pagamento:{" "}
                {paymentMethod === "pix"
                  ? "Pix"
                  : paymentMethod === "boleto"
                    ? "Boleto"
                    : `Cartão em ${installments}x`}
              </p>
            </div>
          ) : null}

          <div className="flex flex-wrap gap-3 border-t border-esotera-graphite pt-4">
            {step > 0 ? (
              <Button
                type="button"
                variant="secondary"
                onClick={() => setStep((s) => s - 1)}
              >
                Voltar
              </Button>
            ) : null}
            {step < steps.length - 1 ? (
              <Button type="button" onClick={next}>
                Continuar
              </Button>
            ) : (
              <Button type="button" onClick={finish}>
                Finalizar pedido (simulação)
              </Button>
            )}
          </div>
        </div>

        <div className="lg:sticky lg:top-24 lg:self-start">
          <div className="mb-4 space-y-2 rounded-lg border border-esotera-graphite p-4 lg:hidden">
            <p className="text-sm text-esotera-beige">Resumo rápido</p>
            <p className="text-xs text-esotera-muted">
              {lines.length} item(ns) · Total estimado{" "}
              <Price value={productsTotal + shippingPrice} />
            </p>
          </div>
          <OrderSummary
            showShipping={step >= 2 && Boolean(selectedShipping)}
            shippingPrice={shippingPrice}
          />
        </div>
      </div>
    </div>
  );
}
