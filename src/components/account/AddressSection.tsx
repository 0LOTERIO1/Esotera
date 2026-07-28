"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { getAddressRepository } from "@/services/repositories";
import { ApiError } from "@/services/api/apiClient";
import { useAuthStore } from "@/stores/authStore";
import { useToastStore } from "@/stores/toastStore";
import { Button } from "@/components/ui/Button";
import { ConfirmModal } from "@/components/ui/ConfirmModal";
import { EmptyState } from "@/components/ui/EmptyState";
import { LoadingState } from "@/components/ui/LoadingState";
import {
  AddressForm,
  emptyAddressFormValues,
  savedAddressToFormValues,
  toAddressInput,
} from "@/components/address/AddressForm";
import { formatAddressLines } from "@/utils/address";
import type { SavedAddress } from "@/types";

function errorMessage(err: unknown, fallback: string): string {
  if (err instanceof ApiError) return err.userMessage;
  if (err instanceof Error) return err.message;
  return fallback;
}

export function AddressSection() {
  const router = useRouter();
  const user = useAuthStore((s) => s.user);
  const push = useToastStore((s) => s.push);

  const [addresses, setAddresses] = useState<SavedAddress[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [formKey, setFormKey] = useState(0);
  const [pendingPrimaryId, setPendingPrimaryId] = useState<string | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<SavedAddress | null>(null);
  const [deleting, setDeleting] = useState(false);

  const handleAuthFailure = useCallback(
    (message: string) => {
      push("error", message);
      router.replace("/login?returnUrl=/minha-conta");
    },
    [push, router],
  );

  const loadAddresses = useCallback(async () => {
    if (!user) return;
    setLoading(true);
    setError(null);
    try {
      const list = await getAddressRepository().list();
      setAddresses(list);
    } catch (err) {
      const message = errorMessage(
        err,
        "Não foi possível carregar os endereços.",
      );
      if (err instanceof ApiError && err.status === 401) {
        handleAuthFailure(message);
        return;
      }
      if (err instanceof ApiError && err.status === 403) {
        setAddresses([]);
        setError("Acesso negado. Você não tem permissão para ver os endereços.");
        return;
      }
      setAddresses([]);
      setError(message);
    } finally {
      setLoading(false);
    }
  }, [user, handleAuthFailure]);

  useEffect(() => {
    if (!user) return;
    let cancelled = false;
    void Promise.resolve().then(async () => {
      if (cancelled) return;
      setLoading(true);
      setError(null);
      try {
        const list = await getAddressRepository().list();
        if (cancelled) return;
        setAddresses(list);
      } catch (err) {
        if (cancelled) return;
        const message = errorMessage(
          err,
          "Não foi possível carregar os endereços.",
        );
        if (err instanceof ApiError && err.status === 401) {
          handleAuthFailure(message);
          return;
        }
        if (err instanceof ApiError && err.status === 403) {
          setAddresses([]);
          setError(
            "Acesso negado. Você não tem permissão para ver os endereços.",
          );
          return;
        }
        setAddresses([]);
        setError(message);
      } finally {
        if (!cancelled) setLoading(false);
      }
    });
    return () => {
      cancelled = true;
    };
  }, [user, handleAuthFailure]);

  function openCreate() {
    setEditingId(null);
    setFormError(null);
    setFormKey((k) => k + 1);
    setFormOpen(true);
  }

  function openEdit(address: SavedAddress) {
    setEditingId(address.id);
    setFormError(null);
    setFormKey((k) => k + 1);
    setFormOpen(true);
  }

  function closeForm() {
    setFormOpen(false);
    setEditingId(null);
    setFormError(null);
  }

  async function handleFormSubmit(
    values: Parameters<typeof toAddressInput>[0],
  ) {
    setFormError(null);
    try {
      const repo = getAddressRepository();
      const payload = toAddressInput(values);
      if (editingId) {
        await repo.update(editingId, payload);
        push("success", "Endereço atualizado.");
      } else {
        await repo.create(payload);
        push("success", "Endereço cadastrado.");
      }
      const list = await repo.list();
      setAddresses(list);
      closeForm();
    } catch (err) {
      const message = errorMessage(err, "Não foi possível salvar o endereço.");
      if (err instanceof ApiError && err.status === 401) {
        handleAuthFailure(message);
        return;
      }
      if (err instanceof ApiError && err.status === 403) {
        setFormError("Acesso negado. Você não pode alterar este endereço.");
        return;
      }
      setFormError(message);
      throw err;
    }
  }

  async function handleSetPrimary(id: string) {
    if (pendingPrimaryId || deleting || formOpen) return;
    setPendingPrimaryId(id);
    try {
      const repo = getAddressRepository();
      await repo.setPrimary(id);
      const list = await repo.list();
      setAddresses(list);
      push("success", "Endereço principal atualizado.");
    } catch (err) {
      const message = errorMessage(
        err,
        "Não foi possível definir o endereço principal.",
      );
      if (err instanceof ApiError && err.status === 401) {
        handleAuthFailure(message);
        return;
      }
      push("error", message);
    } finally {
      setPendingPrimaryId(null);
    }
  }

  async function confirmDelete() {
    if (!deleteTarget || deleting) return;
    setDeleting(true);
    try {
      const repo = getAddressRepository();
      await repo.remove(deleteTarget.id);
      const list = await repo.list();
      setAddresses(list);
      push("success", "Endereço excluído.");
      setDeleteTarget(null);
    } catch (err) {
      const message = errorMessage(err, "Não foi possível excluir o endereço.");
      if (err instanceof ApiError && err.status === 401) {
        handleAuthFailure(message);
        return;
      }
      push("error", message);
    } finally {
      setDeleting(false);
    }
  }

  const busy = Boolean(pendingPrimaryId) || deleting;
  const editing = editingId
    ? addresses.find((a) => a.id === editingId)
    : undefined;

  return (
    <section className="rounded-lg border border-esotera-border p-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <h2 className="font-serif text-xl text-esotera-text">Endereços</h2>
        {!loading && !error ? (
          <Button
            type="button"
            variant="secondary"
            disabled={busy || formOpen}
            onClick={openCreate}
          >
            Novo endereço
          </Button>
        ) : null}
      </div>

      {loading ? (
        <div className="mt-4">
          <LoadingState label="Carregando endereços…" />
        </div>
      ) : null}

      {!loading && error ? (
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
      ) : null}

      {!loading && !error && addresses.length === 0 && !formOpen ? (
        <div className="mt-4">
          <EmptyState
            title="Nenhum endereço cadastrado"
            description="Cadastre um endereço para agilizar suas compras."
            action={
              <Button type="button" onClick={openCreate}>
                Cadastrar endereço
              </Button>
            }
          />
        </div>
      ) : null}

      {!loading && !error && addresses.length > 0 ? (
        <ul className="mt-4 space-y-3">
          {addresses.map((address) => {
            const lines = formatAddressLines(address);
            return (
              <li
                key={address.id}
                className={`rounded-md border p-4 text-sm ${
                  address.isPrimary
                    ? "border-esotera-primary/40 bg-esotera-primary/5"
                    : "border-esotera-border"
                }`}
              >
                <div className="space-y-3">
                  <div className="text-esotera-muted">
                    {address.isPrimary ? (
                      <span className="mb-2 inline-block rounded bg-esotera-primary/15 px-2 py-0.5 text-xs font-medium text-esotera-primary">
                        Principal
                      </span>
                    ) : null}
                    <p className="text-esotera-secondary">{lines.line1}</p>
                    <p>{lines.line2}</p>
                    <p>{lines.line3}</p>
                  </div>
                  <div className="flex flex-wrap gap-2 border-t border-esotera-border/70 pt-3">
                    {!address.isPrimary ? (
                      <Button
                        type="button"
                        variant="ghost"
                        disabled={busy}
                        onClick={() => void handleSetPrimary(address.id)}
                      >
                        {pendingPrimaryId === address.id
                          ? "Definindo…"
                          : "Tornar principal"}
                      </Button>
                    ) : null}
                    <Button
                      type="button"
                      variant="secondary"
                      disabled={busy}
                      onClick={() => openEdit(address)}
                    >
                      Editar endereço
                    </Button>
                    <Button
                      type="button"
                      variant="ghost"
                      disabled={busy}
                      onClick={() => setDeleteTarget(address)}
                    >
                      Excluir
                    </Button>
                  </div>
                </div>
              </li>
            );
          })}
        </ul>
      ) : null}

      {formOpen ? (
        <div className="mt-5">
          <AddressForm
            key={formKey}
            title={editingId ? "Editar endereço" : "Novo endereço"}
            initial={
              editing
                ? savedAddressToFormValues(editing)
                : emptyAddressFormValues({
                    isPrimary: addresses.length === 0,
                  })
            }
            requireCepTouch
            showPrimaryOption
            submitLabel={editingId ? "Salvar alterações" : "Cadastrar"}
            idPrefix={editingId ? "addr-edit" : "addr-new"}
            formError={formError}
            onCancel={closeForm}
            onSubmit={handleFormSubmit}
          />
        </div>
      ) : null}

      <ConfirmModal
        open={Boolean(deleteTarget)}
        title="Excluir endereço?"
        description={
          deleteTarget
            ? `Confirma a exclusão de ${deleteTarget.street}, ${deleteTarget.number}? Esta ação não pode ser desfeita.`
            : ""
        }
        confirmLabel="Excluir"
        busy={deleting}
        onCancel={() => {
          if (!deleting) setDeleteTarget(null);
        }}
        onConfirm={() => void confirmDelete()}
      />
    </section>
  );
}
