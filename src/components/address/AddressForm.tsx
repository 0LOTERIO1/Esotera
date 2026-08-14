"use client";

import { useCallback, useState } from "react";
import { Button } from "@/components/ui/Button";
import { FormField, inputClassName } from "@/components/ui/FormField";
import { brazilianStates } from "@/data/brazilianStates";
import { useCepAutofill } from "@/hooks/useCepAutofill";
import {
  formatCepDisplay,
  validateAddressInput,
  type AddressFormErrors,
} from "@/utils/address";
import { maskCep } from "@/utils/masks";
import type { CepLookupResult } from "@/services/cep/viacepService";
import type { AddressInput, SavedAddress } from "@/types";

export type AddressFormValues = {
  cep: string;
  street: string;
  number: string;
  complement: string;
  neighborhood: string;
  city: string;
  state: string;
  isPrimary: boolean;
  /** null = ainda não escolhido (legado ou formulário novo) */
  isResidentialAddress: boolean | null;
};

export function emptyAddressFormValues(
  overrides?: Partial<AddressFormValues>,
): AddressFormValues {
  return {
    cep: "",
    street: "",
    number: "",
    complement: "",
    neighborhood: "",
    city: "",
    state: "SP",
    isPrimary: false,
    isResidentialAddress: null,
    ...overrides,
  };
}

export function savedAddressToFormValues(
  address: SavedAddress,
): AddressFormValues {
  return {
    cep: formatCepDisplay(address.cep),
    street: address.street,
    number: address.number,
    complement: address.complement ?? "",
    neighborhood: address.neighborhood,
    city: address.city,
    state: address.state,
    isPrimary: address.isPrimary,
    isResidentialAddress:
      address.isResidentialAddress === true ||
      address.isResidentialAddress === false
        ? address.isResidentialAddress
        : null,
  };
}

export function toAddressInput(values: AddressFormValues): AddressInput {
  return {
    cep: values.cep,
    street: values.street,
    number: values.number,
    complement: values.complement || undefined,
    neighborhood: values.neighborhood,
    city: values.city,
    state: values.state,
    isPrimary: values.isPrimary,
    isResidentialAddress: values.isResidentialAddress,
  };
}

type AddressFormProps = {
  title: string;
  initial?: AddressFormValues;
  /** Se true, inicia consultando ViaCEP só após o usuário alterar o CEP */
  requireCepTouch?: boolean;
  showPrimaryOption?: boolean;
  submitLabel?: string;
  idPrefix?: string;
  formError?: string | null;
  onSubmit: (values: AddressFormValues) => Promise<void>;
  onCancel?: () => void;
};

/**
 * Formulário de endereço reutilizável (Minha conta + Checkout).
 * ViaCEP centralizado em useCepAutofill / viacepService.
 */
export function AddressForm({
  title,
  initial,
  requireCepTouch = true,
  showPrimaryOption = true,
  submitLabel = "Salvar endereço",
  idPrefix = "addr",
  formError = null,
  onSubmit,
  onCancel,
}: AddressFormProps) {
  const [form, setForm] = useState<AddressFormValues>(
    () => initial ?? emptyAddressFormValues(),
  );
  const [fieldErrors, setFieldErrors] = useState<AddressFormErrors>({});
  const [submitting, setSubmitting] = useState(false);
  const [cepTouched, setCepTouched] = useState(!requireCepTouch);

  const applyCepResult = useCallback((result: CepLookupResult) => {
    setForm((prev) => ({
      ...prev,
      street: result.street,
      neighborhood: result.neighborhood,
      city: result.city,
      state: result.state || prev.state,
    }));
    setFieldErrors((prev) => {
      const next = { ...prev };
      delete next.cep;
      delete next.street;
      delete next.city;
      delete next.state;
      if (!result.neighborhood) {
        next.neighborhood = "Informe o bairro.";
      } else {
        delete next.neighborhood;
      }
      return next;
    });
  }, []);

  const {
    status: cepStatus,
    message: cepMessage,
    lookingUp,
    assertCepReadyForSubmit,
  } = useCepAutofill({
    cep: form.cep,
    enabled: cepTouched,
    onResolved: applyCepResult,
  });

  function setField<K extends keyof AddressFormValues>(
    key: K,
    value: AddressFormValues[K],
  ) {
    setForm((prev) => ({ ...prev, [key]: value }));
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (submitting || lookingUp) return;

    const errors = validateAddressInput(form);
    const cepBlock = assertCepReadyForSubmit();
    if (cepBlock) errors.cep = cepBlock;
    setFieldErrors(errors);
    if (Object.keys(errors).length > 0) return;

    setSubmitting(true);
    try {
      await onSubmit(form);
    } finally {
      setSubmitting(false);
    }
  }

  const id = (name: string) => `${idPrefix}-${name}`;
  const busy = submitting || lookingUp;
  const addressFieldsLocked = lookingUp;

  return (
    <form
      onSubmit={handleSubmit}
      className="space-y-3 rounded-md border border-esotera-border bg-esotera-surface-secondary p-4"
      noValidate
      aria-labelledby={`${idPrefix}-form-title`}
    >
      <h3
        id={`${idPrefix}-form-title`}
        className="font-serif text-lg text-esotera-secondary"
      >
        {title}
      </h3>

      {formError ? (
        <p role="alert" className="text-sm text-esotera-error">
          {formError}
        </p>
      ) : null}

      <div className="grid gap-3 sm:grid-cols-2">
        <FormField
          label="CEP"
          id={id("cep")}
          required
          error={
            fieldErrors.cep ||
            (cepStatus === "not_found" || cepStatus === "error"
              ? (cepMessage ?? undefined)
              : undefined)
          }
        >
          <input
            id={id("cep")}
            className={inputClassName}
            value={form.cep}
            onChange={(e) => {
              setCepTouched(true);
              setField("cep", maskCep(e.target.value));
            }}
            inputMode="numeric"
            autoComplete="postal-code"
            disabled={submitting}
            aria-busy={lookingUp}
          />
        </FormField>
        {lookingUp ? (
          <p className="sm:col-span-2 text-xs text-esotera-muted" role="status">
            Buscando CEP...
          </p>
        ) : null}
        {cepStatus === "ok" && cepMessage ? (
          <p className="sm:col-span-2 text-xs text-esotera-muted" role="status">
            {cepMessage}
          </p>
        ) : null}
        <FormField
          label="Estado"
          id={id("state")}
          required
          error={fieldErrors.state}
        >
          <select
            id={id("state")}
            className={inputClassName}
            value={form.state}
            onChange={(e) => setField("state", e.target.value.toUpperCase())}
            disabled={submitting || addressFieldsLocked}
          >
            {brazilianStates.map((s) => (
              <option key={s.uf} value={s.uf}>
                {s.uf} — {s.name}
              </option>
            ))}
          </select>
        </FormField>
        <div className="sm:col-span-2">
          <FormField
            label="Rua"
            id={id("street")}
            required
            error={fieldErrors.street}
          >
            <input
              id={id("street")}
              className={inputClassName}
              value={form.street}
              onChange={(e) => setField("street", e.target.value)}
              autoComplete="address-line1"
              disabled={submitting || addressFieldsLocked}
            />
          </FormField>
        </div>
        <FormField
          label="Número"
          id={id("number")}
          required
          error={fieldErrors.number}
        >
          <input
            id={id("number")}
            className={inputClassName}
            value={form.number}
            onChange={(e) => setField("number", e.target.value)}
            disabled={submitting}
          />
        </FormField>
        <FormField label="Complemento" id={id("complement")}>
          <input
            id={id("complement")}
            className={inputClassName}
            value={form.complement}
            onChange={(e) => setField("complement", e.target.value)}
            disabled={submitting}
          />
        </FormField>
        <FormField
          label="Bairro"
          id={id("neighborhood")}
          required
          error={fieldErrors.neighborhood}
        >
          <input
            id={id("neighborhood")}
            className={inputClassName}
            value={form.neighborhood}
            onChange={(e) => setField("neighborhood", e.target.value)}
            disabled={submitting || addressFieldsLocked}
          />
        </FormField>
        <FormField
          label="Cidade"
          id={id("city")}
          required
          error={fieldErrors.city}
        >
          <input
            id={id("city")}
            className={inputClassName}
            value={form.city}
            onChange={(e) => setField("city", e.target.value)}
            autoComplete="address-level2"
            disabled={submitting || addressFieldsLocked}
          />
        </FormField>
      </div>

      <fieldset className="space-y-2">
        <legend className="text-sm font-medium text-esotera-secondary">
          Tipo de endereço <span className="text-esotera-error">*</span>
        </legend>
        <div className="flex flex-wrap gap-4 text-sm text-esotera-muted">
          <label className="flex items-center gap-2">
            <input
              type="radio"
              name={id("residential")}
              checked={form.isResidentialAddress === true}
              onChange={() => setField("isResidentialAddress", true)}
              disabled={submitting}
            />
            Residencial
          </label>
          <label className="flex items-center gap-2">
            <input
              type="radio"
              name={id("residential")}
              checked={form.isResidentialAddress === false}
              onChange={() => setField("isResidentialAddress", false)}
              disabled={submitting}
            />
            Comercial
          </label>
        </div>
        {fieldErrors.isResidentialAddress ? (
          <p role="alert" className="text-sm text-esotera-error">
            {fieldErrors.isResidentialAddress}
          </p>
        ) : null}
      </fieldset>

      {showPrimaryOption ? (
        <label className="flex items-center gap-2 text-sm text-esotera-muted">
          <input
            type="checkbox"
            checked={form.isPrimary}
            onChange={(e) => setField("isPrimary", e.target.checked)}
            disabled={submitting}
          />
          Definir como endereço principal
        </label>
      ) : null}

      <div className="flex flex-wrap gap-2 pt-1">
        <Button type="submit" disabled={busy}>
          {submitting
            ? "Salvando…"
            : lookingUp
              ? "Buscando CEP..."
              : submitLabel}
        </Button>
        {onCancel ? (
          <Button
            type="button"
            variant="ghost"
            disabled={submitting}
            onClick={onCancel}
          >
            Cancelar
          </Button>
        ) : null}
      </div>
    </form>
  );
}
