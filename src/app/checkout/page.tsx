"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
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
import { CheckoutAddressStep } from "@/components/checkout/CheckoutAddressStep";
import { Button, ButtonLink } from "@/components/ui/Button";
import { EmptyState } from "@/components/ui/EmptyState";
import {
  quoteShippingSafe,
  qualifiesForFreeShipping,
} from "@/services/shipping/shippingService";
import type { PaymentMethod, SavedAddress, ShippingOption } from "@/types";
import { Price } from "@/components/ui/Price";
import { useToastStore } from "@/stores/toastStore";
import { formatAddressLines } from "@/utils/address";
import { validateCep } from "@/utils/validation";
import { ApiError } from "@/services/api/apiClient";
import {
  createIdempotencyKey,
  fingerprintOrderAttempt,
} from "@/utils/orderIdempotency";
import { isApiMode } from "@/config/dataMode";
import {
  canCompleteCheckoutWithoutRealPayment,
  isRealPaymentEnabled,
} from "@/config/storeMode";
import { resolveUnitPrice } from "@/utils/productPricing";

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
  const sessionReady = useAuthStore((s) => s.sessionReady);
  const logout = useAuthStore((s) => s.logout);
  const authReady = hydrated && sessionReady;
  const cartItems = useCartStore((s) => s.items);
  const coupon = useCartStore((s) => s.coupon);
  const clearCart = useCartStore((s) => s.clearCart);
  const createOrder = useOrdersStore((s) => s.createOrder);
  const settings = useSettingsStore((s) => s.settings);
  const { lines, subtotal, discount, productsTotal } = useCartTotals();
  const push = useToastStore((s) => s.push);

  const [step, setStep] = useState(0);
  /** Seleção por ID real — snapshot do endereço vem do repository */
  const [selectedAddress, setSelectedAddress] = useState<SavedAddress | null>(
    null,
  );
  const [selectedShippingId, setSelectedShippingId] = useState<string | null>(
    null,
  );
  const [shippingOptions, setShippingOptions] = useState<ShippingOption[]>([]);
  const [shippingLoading, setShippingLoading] = useState(false);
  const [shippingError, setShippingError] = useState<string | null>(null);
  const [shippingQuoteKey, setShippingQuoteKey] = useState(0);
  const [paymentMethod, setPaymentMethod] = useState<PaymentMethod>("pix");
  const [installments, setInstallments] = useState(1);
  const [finishing, setFinishing] = useState(false);
  const [finishError, setFinishError] = useState<string | null>(null);
  const [uncertainResult, setUncertainResult] = useState(false);
  const idempotencyKeyRef = useRef<string | null>(null);
  const attemptFingerprintRef = useRef<string | null>(null);
  const activeRequestRef = useRef(false);

  useEffect(() => {
    if (!authReady) return;
    if (!user) {
      router.replace("/login?returnUrl=/checkout");
    }
  }, [authReady, user, router]);

  const handleAuthFailure = useCallback(
    async (message: string) => {
      push("error", message);
      await logout();
      router.replace("/login?returnUrl=/checkout");
    },
    [logout, push, router],
  );

  const addressIdRef = useRef<string | null>(null);

  const handleSelectedAddressChange = useCallback(
    (address: SavedAddress | null) => {
      const nextId = address?.id ?? null;
      if (addressIdRef.current !== nextId) {
        addressIdRef.current = nextId;
        setSelectedShippingId(null);
        setShippingOptions([]);
        setShippingError(null);
      }
      setSelectedAddress(address);
    },
    [],
  );

  const quoteShipping = useCallback(() => {
    if (!selectedAddress || !validateCep(selectedAddress.cep)) {
      setShippingOptions([]);
      setShippingError(null);
      setShippingLoading(false);
      return;
    }

    setShippingLoading(true);
    setShippingError(null);

    window.setTimeout(() => {
      void (async () => {
        try {
          const result = await quoteShippingSafe({
            cep: selectedAddress.cep,
            state: selectedAddress.state,
            productsTotalAfterDiscount: productsTotal,
            settings,
          });
          // Falha de cotação: opções vazias — nunca inventar preço no checkout.
          setShippingOptions(result.options);
          setSelectedShippingId((prev) => {
            if (prev && result.options.some((o) => o.id === prev)) return prev;
            return result.options[0]?.id ?? null;
          });
          setShippingError(
            result.ok ? null : (result.errorMessage ?? "Não foi possível calcular o frete."),
          );
        } catch {
          setShippingOptions([]);
          setSelectedShippingId(null);
          setShippingError(
            "Não foi possível calcular o frete. Tente novamente.",
          );
        } finally {
          setShippingLoading(false);
        }
      })();
    }, 200);
  }, [selectedAddress, productsTotal, settings]);

  useEffect(() => {
    if (step < 2) return;
    // Deferir para evitar setState síncrono no corpo do effect
    const timer = window.setTimeout(() => {
      quoteShipping();
    }, 0);
    return () => window.clearTimeout(timer);
  }, [step, quoteShipping, shippingQuoteKey]);

  const selectedShipping =
    shippingOptions.find((o) => o.id === selectedShippingId) ?? null;

  const freeHint = useMemo(() => {
    if (!selectedAddress) return undefined;
    if (
      qualifiesForFreeShipping(productsTotal, selectedAddress.state, settings)
    ) {
      return "Frete grátis disponível para este pedido (Sul/Sudeste acima do mínimo).";
    }
    return undefined;
  }, [selectedAddress, productsTotal, settings]);

  if (!authReady || !user) {
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
    if (step === 1) {
      if (!selectedAddress?.id) {
        push("error", "Selecione ou cadastre um endereço de entrega.");
        return;
      }
      if (
        !validateCep(selectedAddress.cep) ||
        !selectedAddress.street ||
        !selectedAddress.number
      ) {
        push("error", "O endereço selecionado está incompleto.");
        return;
      }
    }
    if (step === 2) {
      if (shippingLoading) {
        push("info", "Aguarde o cálculo do frete.");
        return;
      }
      if (!selectedShipping) {
        push("error", "Selecione uma modalidade de entrega.");
        return;
      }
    }
    setStep((s) => Math.min(s + 1, steps.length - 1));
  }

  async function finish() {
    if (!selectedAddress || !selectedShipping || !user || finishing) return;
    if (activeRequestRef.current) return;

    if (!isRealPaymentEnabled() && !canCompleteCheckoutWithoutRealPayment()) {
      push(
        "info",
        "A finalização de compras estará disponível em breve, quando o pagamento estiver integrado.",
      );
      return;
    }

    // Modo API: somente addressId do usuário autenticado (sem endereço inline)
    if (isApiMode() && !selectedAddress.id) {
      push("error", "Selecione um endereço de entrega válido da sua conta.");
      return;
    }

    const attempt = {
      addressId: selectedAddress.id,
      items: lines.map((l) => ({
        productId: l.productId,
        quantity: l.quantity,
        variation: l.variation,
      })),
      shippingMethodId: selectedShipping.id,
      paymentMethod,
      installments: paymentMethod === "card" ? installments : undefined,
      couponCode: coupon?.code,
    };
    const fingerprint = fingerprintOrderAttempt(attempt);

    if (
      !idempotencyKeyRef.current ||
      attemptFingerprintRef.current !== fingerprint
    ) {
      idempotencyKeyRef.current = createIdempotencyKey();
      attemptFingerprintRef.current = fingerprint;
      setUncertainResult(false);
    }

    setFinishing(true);
    setFinishError(null);
    activeRequestRef.current = true;

    try {
      const addressForOrder = {
        cep: selectedAddress.cep,
        street: selectedAddress.street,
        number: selectedAddress.number,
        complement: selectedAddress.complement,
        neighborhood: selectedAddress.neighborhood,
        city: selectedAddress.city,
        state: selectedAddress.state,
      };

      const order = await createOrder({
        userId: user.id,
        customerName: user.name,
        customerEmail: user.email,
        customerPhone: user.phone,
        customerCpf: user.cpf,
        items: lines.map((l) => ({
          productId: l.productId,
          name: l.product.name,
          price: resolveUnitPrice(l.product, l.variation),
          quantity: l.quantity,
          variation: l.variation,
          image: l.product.images[0],
        })),
        subtotal,
        discount,
        couponCode: coupon?.code,
        shippingOption: selectedShipping,
        address: addressForOrder,
        addressId: selectedAddress.id,
        paymentMethod,
        installments: paymentMethod === "card" ? installments : undefined,
        idempotencyKey: idempotencyKeyRef.current ?? undefined,
      });

      idempotencyKeyRef.current = null;
      attemptFingerprintRef.current = null;
      setUncertainResult(false);
      clearCart();

      if (isApiMode() && isRealPaymentEnabled()) {
        push("success", "Pedido criado. Conclua o pagamento.");
        router.push(`/pagamento/${order.id}`);
        return;
      }

      push("success", "Pedido criado com sucesso.");
      router.push(`/pedido-confirmado/${order.id}`);
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        await handleAuthFailure(error.userMessage);
        return;
      }

      if (error instanceof ApiError && error.status === 409) {
        setFinishError(error.userMessage);
        idempotencyKeyRef.current = null;
        attemptFingerprintRef.current = null;
        setUncertainResult(false);
        push("error", error.userMessage);
        return;
      }

      const isUncertain =
        error instanceof ApiError &&
        (error.status === 0 || error.status >= 500);

      if (isUncertain) {
        setUncertainResult(true);
        setFinishError(
          "Não foi possível confirmar o resultado do pedido. Toque em Tentar novamente — se o pedido já foi criado, ele será recuperado.",
        );
        push("info", "Resultado incerto — tente novamente sem alterar o pedido.");
        return;
      }

      const message =
        error instanceof ApiError
          ? error.userMessage
          : error instanceof Error
            ? error.message
            : "Erro ao criar pedido.";
      setFinishError(message);
      setUncertainResult(false);
      push("error", message);
    } finally {
      activeRequestRef.current = false;
      setFinishing(false);
    }
  }

  const shippingPrice = selectedShipping?.price ?? 0;
  const shippingMode =
    step < 2 ? "omit" : selectedShipping ? "selected" : "pending";
  const addressLines = selectedAddress
    ? formatAddressLines(selectedAddress)
    : null;

  const checkoutBlocked =
    !isRealPaymentEnabled() && !canCompleteCheckoutWithoutRealPayment();

  return (
    <div className="mx-auto max-w-6xl px-4 py-10 sm:px-6">
      <h1 className="font-serif text-4xl text-esotera-secondary">Checkout</h1>
      <p className="mt-2 text-sm text-esotera-muted">
        Confirme endereço, frete e forma de pagamento para concluir seu pedido.
      </p>

      <ol className="mt-6 flex flex-wrap gap-2" aria-label="Etapas do checkout">
        {steps.map((label, index) => (
          <li
            key={label}
            className={`rounded-md border px-3 py-1.5 text-xs ${
              index === step
                ? "border-esotera-primary text-esotera-primary"
                : index < step
                  ? "border-esotera-success/40 text-esotera-text"
                  : "border-esotera-border text-esotera-muted"
            }`}
          >
            {index + 1}. {label}
          </li>
        ))}
      </ol>

      <div className="mt-8 grid gap-8 lg:grid-cols-[1fr_320px]">
        <div className="space-y-6 rounded-lg border border-esotera-border p-5">
          {step === 0 ? (
            <div className="space-y-2 text-sm text-esotera-muted">
              <h2 className="font-serif text-2xl text-esotera-text">
                Identificação
              </h2>
              <p>
                <strong className="text-esotera-text">Nome:</strong> {user.name}
              </p>
              <p>
                <strong className="text-esotera-text">E-mail:</strong>{" "}
                {user.email}
              </p>
              <p>
                <strong className="text-esotera-text">CPF:</strong> {user.cpf}
              </p>
              <p>
                <strong className="text-esotera-text">Telefone:</strong>{" "}
                {user.phone}
              </p>
            </div>
          ) : null}

          {/* Mantém montado para preservar seleção ao voltar da etapa Entrega */}
          <div hidden={step !== 1}>
            <CheckoutAddressStep
              active={step === 1}
              onSelectedAddressChange={handleSelectedAddressChange}
              onAuthFailure={handleAuthFailure}
            />
          </div>

          {step === 2 ? (
            <div>
              <h2 className="mb-4 font-serif text-2xl text-esotera-text">
                Entrega
              </h2>
              {selectedAddress ? (
                <p className="mb-4 text-sm text-esotera-muted">
                  Entrega em {formatAddressLines(selectedAddress).line1} —{" "}
                  {formatAddressLines(selectedAddress).line2}
                </p>
              ) : (
                <p className="mb-4 text-sm text-esotera-error" role="alert">
                  Selecione um endereço na etapa anterior.
                </p>
              )}
              <ShippingOptions
                options={shippingOptions}
                selectedId={selectedShipping?.id}
                onSelect={(option) => setSelectedShippingId(option.id)}
                freeShippingHint={freeHint}
                loading={shippingLoading}
                error={shippingError}
                onRetry={() => setShippingQuoteKey((k) => k + 1)}
              />
            </div>
          ) : null}

          {step === 3 ? (
            <div>
              <h2 className="mb-4 font-serif text-2xl text-esotera-text">
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
              <h2 className="font-serif text-2xl text-esotera-text">Revisão</h2>
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
                      <p className="text-esotera-text">{l.product.name}</p>
                      <p>
                        {l.quantity} ×{" "}
                        <Price value={resolveUnitPrice(l.product, l.variation)} />
                      </p>
                    </div>
                  </li>
                ))}
              </ul>
              {addressLines ? (
                <div>
                  <p className="text-esotera-text">Entrega</p>
                  <p>{addressLines.line1}</p>
                  <p>{addressLines.line2}</p>
                  <p>{addressLines.line3}</p>
                </div>
              ) : null}
              {selectedShipping ? (
                <p>
                  Frete: {selectedShipping.provider} — {selectedShipping.name} (
                  {selectedShipping.estimatedDays})
                  {selectedShipping.price === 0 ? " · Grátis" : null}
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

          <div className="flex flex-wrap gap-3 border-t border-esotera-border pt-4">
            {step > 0 ? (
              <Button
                type="button"
                variant="secondary"
                disabled={finishing}
                onClick={() => setStep((s) => s - 1)}
              >
                Voltar
              </Button>
            ) : null}
            {step < steps.length - 1 ? (
              <Button type="button" onClick={next} disabled={finishing}>
                Continuar
              </Button>
            ) : (
              <div className="flex flex-col gap-3">
                {finishError ? (
                  <p className="text-sm text-esotera-error" role="alert">
                    {finishError}
                  </p>
                ) : null}
                <Button
                  type="button"
                  onClick={() => void finish()}
                  disabled={finishing || checkoutBlocked}
                >
                  {finishing
                    ? "Processando pedido..."
                    : uncertainResult
                      ? "Tentar novamente"
                      : checkoutBlocked
                        ? "Pagamento em breve"
                        : "Finalizar pedido"}
                </Button>
              </div>
            )}
          </div>
        </div>

        <div className="lg:sticky lg:top-24 lg:self-start">
          <div className="mb-4 space-y-2 rounded-lg border border-esotera-border p-4 lg:hidden">
            <p className="text-sm text-esotera-text">Resumo rápido</p>
            <p className="text-xs text-esotera-muted">
              {lines.length} item(ns) · Total estimado{" "}
              <Price
                value={
                  shippingMode === "selected"
                    ? productsTotal + shippingPrice
                    : productsTotal
                }
              />
            </p>
          </div>
          <OrderSummary
            shippingMode={shippingMode}
            shippingPrice={shippingPrice}
          />
        </div>
      </div>
    </div>
  );
}
