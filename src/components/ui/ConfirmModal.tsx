"use client";

type ConfirmModalProps = {
  open: boolean;
  title: string;
  description: string;
  confirmLabel?: string;
  cancelLabel?: string;
  onConfirm: () => void;
  onCancel: () => void;
};

export function ConfirmModal({
  open,
  title,
  description,
  confirmLabel = "Confirmar",
  cancelLabel = "Cancelar",
  onConfirm,
  onCancel,
}: ConfirmModalProps) {
  if (!open) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4"
      role="dialog"
      aria-modal="true"
      aria-labelledby="confirm-title"
    >
      <div className="w-full max-w-md rounded-lg border border-esotera-graphite bg-esotera-navy p-6 shadow-xl">
        <h2 id="confirm-title" className="font-serif text-xl text-esotera-white">
          {title}
        </h2>
        <p className="mt-2 text-sm text-esotera-muted">{description}</p>
        <div className="mt-6 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
          <button
            type="button"
            onClick={onCancel}
            className="rounded-md border border-esotera-graphite px-4 py-2.5 text-sm text-esotera-beige hover:border-esotera-muted"
          >
            {cancelLabel}
          </button>
          <button
            type="button"
            onClick={onConfirm}
            className="rounded-md bg-esotera-gold px-4 py-2.5 text-sm font-medium text-esotera-black hover:bg-esotera-gold-soft"
          >
            {confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
