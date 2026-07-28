"use client";

import Link from "next/link";
import { useCallback, useEffect, useRef, useState } from "react";
import { getAddressRepository } from "@/services/repositories";
import { ApiError } from "@/services/api/apiClient";
import { useToastStore } from "@/stores/toastStore";
import { Button } from "@/components/ui/Button";
import { EmptyState } from "@/components/ui/EmptyState";
import { LoadingState } from "@/components/ui/LoadingState";
import {
  AddressForm,
  emptyAddressFormValues,
  toAddressInput,
  type AddressFormValues,
} from "@/components/address/AddressForm";
import { formatAddressLines } from "@/utils/address";
import type { SavedAddress } from "@/types";

function errorMessage(err: unknown, fallback: string): string {
  if (err instanceof ApiError) return err.userMessage;
  if (err instanceof Error) return err.message;
  return fallback;
}

function pickDefaultAddressId(list: SavedAddress[]): string | null {
  if (!list.length) return null;
  return list.find((a) => a.isPrimary)?.id ?? list[0]?.id ?? null;
}

type CheckoutAddressStepProps = {
  active: boolean;
  /** Endereço escolhido para entrega (cópia da lista da API) */
  onSelectedAddressChange: (address: SavedAddress | null) => void;
  onAuthFailure: (message: string) => void;
};

/**
 * Etapa de endereço do checkout: lista real via repository + cadastro inline.
 * Seleção por addressId; não altera o principal da conta ao escolher entrega.
 */
export function CheckoutAddressStep({
  active,
  onSelectedAddressChange,
  onAuthFailure,
}: CheckoutAddressStepProps) {
  const push = useToastStore((s) => s.push);

  const [addresses, setAddresses] = useState<SavedAddress[]>([]);
  const [selectedAddressId, setSelectedAddressId] = useState<string | null>(
    null,
  );
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [formKey, setFormKey] = useState(0);

  const userPickedRef = useRef(false);
  const selectedIdRef = useRef<string | null>(null);
  const onSelectedRef = useRef(onSelectedAddressChange);
  const onAuthRef = useRef(onAuthFailure);
  const activeRef = useRef(active);

  useEffect(() => {
    onSelectedRef.current = onSelectedAddressChange;
    onAuthRef.current = onAuthFailure;
    activeRef.current = active;
  }, [onSelectedAddressChange, onAuthFailure, active]);

  const notifySelection = useCallback(
    (list: SavedAddress[], id: string | null) => {
      selectedIdRef.current = id;
      setSelectedAddressId(id);
      const address = id ? (list.find((a) => a.id === id) ?? null) : null;
      onSelectedRef.current(address);
    },
    [],
  );

  const resolveSelection = useCallback(
    (list: SavedAddress[], preferId?: string | null) => {
      if (preferId && list.some((a) => a.id === preferId)) {
        userPickedRef.current = true;
        notifySelection(list, preferId);
        return;
      }

      const current = selectedIdRef.current;
      if (current && list.some((a) => a.id === current)) {
        notifySelection(list, current);
        return;
      }

      // Carga inicial ou seleção inválida → principal (ou primeiro)
      userPickedRef.current = false;
      notifySelection(list, pickDefaultAddressId(list));
    },
    [notifySelection],
  );

  const loadAddresses = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const list = await getAddressRepository().list();
      setAddresses(list);
      resolveSelection(list);
    } catch (err) {
      const message = errorMessage(
        err,
        "Não foi possível carregar os endereços.",
      );
      if (err instanceof ApiError && err.status === 401) {
        onAuthRef.current(message);
        return;
      }
      if (err instanceof ApiError && err.status === 403) {
        setAddresses([]);
        notifySelection([], null);
        setError("Acesso negado. Você não tem permissão para ver os endereços.");
        return;
      }
      setAddresses([]);
      notifySelection([], null);
      setError(message);
    } finally {
      setLoading(false);
    }
  }, [notifySelection, resolveSelection]);

  useEffect(() => {
    if (!active) return;
    let cancelled = false;

    void Promise.resolve().then(async () => {
      if (cancelled) return;
      setLoading(true);
      setError(null);
      try {
        const list = await getAddressRepository().list();
        if (cancelled) return;
        setAddresses(list);
        resolveSelection(list);
      } catch (err) {
        if (cancelled) return;
        const message = errorMessage(
          err,
          "Não foi possível carregar os endereços.",
        );
        if (err instanceof ApiError && err.status === 401) {
          onAuthRef.current(message);
          return;
        }
        if (err instanceof ApiError && err.status === 403) {
          setAddresses([]);
          notifySelection([], null);
          setError(
            "Acesso negado. Você não tem permissão para ver os endereços.",
          );
          return;
        }
        setAddresses([]);
        notifySelection([], null);
        setError(message);
      } finally {
        if (!cancelled) setLoading(false);
      }
    });

    return () => {
      cancelled = true;
    };
  }, [active, notifySelection, resolveSelection]);

  function selectAddress(id: string) {
    userPickedRef.current = true;
    notifySelection(addresses, id);
  }

  function openCreateForm() {
    setFormError(null);
    setFormKey((k) => k + 1);
    setFormOpen(true);
  }

  async function handleCreate(values: AddressFormValues) {
    setFormError(null);
    try {
      const repo = getAddressRepository();
      const created = await repo.create(toAddressInput(values));
      const list = await repo.list();
      setAddresses(list);
      resolveSelection(list, created.id);
      setFormOpen(false);
      push("success", "Endereço cadastrado.");
    } catch (err) {
      const message = errorMessage(err, "Não foi possível salvar o endereço.");
      if (err instanceof ApiError && err.status === 401) {
        onAuthRef.current(message);
        return;
      }
      if (err instanceof ApiError && err.status === 403) {
        setFormError("Acesso negado. Você não pode cadastrar endereço.");
        return;
      }
      setFormError(message);
      throw err;
    }
  }

  if (loading) {
    return (
      <div>
        <h2 className="font-serif text-2xl text-esotera-text">Endereço</h2>
        <div className="mt-4">
          <LoadingState label="Carregando endereços…" />
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div>
        <h2 className="font-serif text-2xl text-esotera-text">Endereço</h2>
        <div className="mt-4">
          <EmptyState
            title="Endereços indisponíveis"
            description={error}
            action={
              <Button type="button" onClick={() => void loadAddresses()}>
                Tentar novamente
              </Button>
            }
          />
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <h2 className="font-serif text-2xl text-esotera-text">Endereço</h2>
        <div className="flex flex-wrap gap-2">
          <Button
            type="button"
            variant="secondary"
            disabled={formOpen}
            onClick={openCreateForm}
          >
            Cadastrar endereço
          </Button>
          <Link
            href="/minha-conta"
            className="inline-flex min-h-11 items-center text-sm text-esotera-muted hover:text-esotera-primary"
          >
            Gerenciar endereços
          </Link>
        </div>
      </div>

      {addresses.length === 0 && !formOpen ? (
        <EmptyState
          title="Nenhum endereço cadastrado"
          description="Cadastre um endereço de entrega para continuar a compra."
          action={
            <Button type="button" onClick={openCreateForm}>
              Cadastrar endereço
            </Button>
          }
        />
      ) : null}

      {addresses.length > 0 ? (
        <fieldset className="space-y-3">
          <legend className="sr-only">Selecione o endereço de entrega</legend>
          {addresses.map((address) => {
            const lines = formatAddressLines(address);
            const selected = selectedAddressId === address.id;
            return (
              <label
                key={address.id}
                className={`flex cursor-pointer items-start gap-3 rounded-md border p-4 text-sm transition focus-within:outline focus-within:outline-2 focus-within:outline-offset-2 focus-within:outline-esotera-primary ${
                  selected
                    ? "border-esotera-primary bg-esotera-primary/5"
                    : address.isPrimary
                      ? "border-esotera-primary/30 hover:border-esotera-primary"
                      : "border-esotera-border hover:border-esotera-muted"
                }`}
              >
                <input
                  type="radio"
                  name="checkout-address"
                  value={address.id}
                  checked={selected}
                  onChange={() => selectAddress(address.id)}
                  className="mt-1"
                />
                <span className="flex-1 text-esotera-muted">
                  {address.isPrimary ? (
                    <span className="mb-2 inline-block rounded bg-esotera-primary/15 px-2 py-0.5 text-xs font-medium text-esotera-primary">
                      Principal
                    </span>
                  ) : null}
                  <span className="block text-esotera-secondary">{lines.line1}</span>
                  <span className="block">{lines.line2}</span>
                  <span className="block">{lines.line3}</span>
                </span>
              </label>
            );
          })}
        </fieldset>
      ) : null}

      {formOpen ? (
        <AddressForm
          key={formKey}
          title="Novo endereço de entrega"
          initial={emptyAddressFormValues({
            isPrimary: addresses.length === 0,
          })}
          requireCepTouch
          showPrimaryOption
          submitLabel="Usar este endereço"
          idPrefix="chk-addr"
          formError={formError}
          onCancel={() => {
            setFormOpen(false);
            setFormError(null);
          }}
          onSubmit={handleCreate}
        />
      ) : null}
    </div>
  );
}
