"use client";

type ConfirmModalProps = {
  open: boolean;
  title: string;
  description: string;
  confirmLabel?: string;
  cancelLabel?: string;
  busy?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
};

export function ConfirmModal({
  open,
  title,
  description,
  confirmLabel = "Confirmar",
  cancelLabel = "Cancelar",
  busy = false,
  onConfirm,
  onCancel,
}: ConfirmModalProps) {
  if (!open) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-esotera-secondary/40 p-4"
      role="dialog"
      aria-modal="true"
      aria-labelledby="confirm-title"
    >
      <div className="w-full max-w-md rounded-lg border border-esotera-border bg-esotera-surface p-6 shadow-xl">
        <h2 id="confirm-title" className="font-serif text-xl text-esotera-secondary">
          {title}
        </h2>
        <p className="mt-2 text-sm text-esotera-muted">{description}</p>
        <div className="mt-6 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
          <button
            type="button"
            onClick={onCancel}
            disabled={busy}
            className="min-h-11 rounded-md border border-esotera-border px-4 py-2.5 text-sm text-esotera-secondary hover:bg-esotera-surface-secondary disabled:opacity-60"
          >
            {cancelLabel}
          </button>
          <button
            type="button"
            onClick={onConfirm}
            disabled={busy}
            className="min-h-11 rounded-md bg-esotera-primary px-4 py-2.5 text-sm font-medium text-white hover:bg-esotera-primary-hover disabled:opacity-60"
          >
            {busy ? "Aguarde…" : confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
