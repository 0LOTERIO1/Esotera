"use client";

import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { FormField, inputClassName } from "@/components/ui/FormField";
import { ConfirmModal } from "@/components/ui/ConfirmModal";
import { ProductThumbnail } from "@/components/products/ProductThumbnail";
import {
  MAX_IMAGE_BYTES,
  validateProductImage,
} from "@/utils/imageStorage";
import type { ProductImageMeta } from "@/types";

type Props = {
  productId: string;
  images: ProductImageMeta[];
  busy: boolean;
  onUpload: (file: File, isPrimary: boolean) => Promise<void>;
  onSetPrimary: (imageId: string) => Promise<void>;
  onUpdateAlt: (imageId: string, altText: string) => Promise<void>;
  onDelete: (imageId: string) => Promise<void>;
  onReorder: (imageIds: string[]) => Promise<void>;
  /** Modo mock: apenas pré-visualização local, sem upload real. */
  mockMode?: boolean;
};

export function ProductImageManager({
  productId,
  images,
  busy,
  onUpload,
  onSetPrimary,
  onUpdateAlt,
  onDelete,
  onReorder,
  mockMode = false,
}: Props) {
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [deleteId, setDeleteId] = useState<string | null>(null);
  const [altDrafts, setAltDrafts] = useState<Record<string, string>>({});

  async function handleFile(file: File | null, isPrimary: boolean) {
    if (!file) return;
    const validation = validateProductImage(file);
    if (validation) {
      setError(validation);
      return;
    }
    setError(null);
    setUploading(true);
    try {
      await onUpload(file, isPrimary);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Falha no upload.");
    } finally {
      setUploading(false);
    }
  }

  function move(index: number, dir: -1 | 1) {
    const next = index + dir;
    if (next < 0 || next >= images.length) return;
    const ids = images.map((i) => i.id);
    [ids[index], ids[next]] = [ids[next], ids[index]];
    void onReorder(ids);
  }

  return (
    <div className="space-y-3" aria-labelledby={`images-${productId}`}>
      <div>
        <h3 id={`images-${productId}`} className="text-sm font-medium text-esotera-secondary">
          Imagens
        </h3>
        <p className="text-xs text-esotera-muted">
          JPEG, PNG ou WebP · máx. {(MAX_IMAGE_BYTES / (1024 * 1024)).toFixed(0)} MB · até 8
          imagens.
          {mockMode
            ? " Modo demonstração: pré-visualização local (sem Cloudinary)."
            : " Upload via API → Cloudinary."}
        </p>
      </div>

      <div className="flex flex-wrap gap-2">
        <label className="inline-flex cursor-pointer items-center">
          <span className="sr-only">Enviar imagem principal</span>
          <input
            type="file"
            accept="image/png,image/jpeg,image/webp"
            className="block w-full max-w-xs text-sm text-esotera-muted file:mr-3 file:rounded-md file:border-0 file:bg-esotera-primary file:px-3 file:py-2 file:text-sm file:font-medium file:text-white"
            disabled={busy || uploading}
            onChange={(e) => {
              void handleFile(e.target.files?.[0] ?? null, true);
              e.target.value = "";
            }}
          />
        </label>
        <label className="inline-flex cursor-pointer items-center">
          <span className="sr-only">Enviar imagem adicional</span>
          <input
            type="file"
            accept="image/png,image/jpeg,image/webp"
            className="block w-full max-w-xs text-sm text-esotera-muted file:mr-3 file:rounded-md file:border-0 file:bg-esotera-surface-secondary file:px-3 file:py-2 file:text-sm file:font-medium file:text-esotera-secondary"
            disabled={busy || uploading}
            onChange={(e) => {
              void handleFile(e.target.files?.[0] ?? null, false);
              e.target.value = "";
            }}
          />
        </label>
      </div>

      {uploading ? (
        <p className="text-xs text-esotera-muted" role="status">
          Enviando imagem…
        </p>
      ) : null}
      {error ? (
        <p role="alert" className="text-xs text-esotera-error">
          {error}
        </p>
      ) : null}

      {images.length === 0 ? (
        <p className="text-sm text-esotera-muted">Nenhuma imagem cadastrada.</p>
      ) : (
        <ul className="space-y-3">
          {images.map((img, index) => (
            <li
              key={img.id}
              className="flex flex-col gap-2 rounded-md border border-esotera-border p-2 sm:flex-row sm:items-start"
            >
              <ProductThumbnail src={img.secureUrl} alt={img.altText || "Produto"} />
              <div className="min-w-0 flex-1 space-y-2">
                <div className="flex flex-wrap gap-2 text-xs">
                  {img.isPrimary ? (
                    <span className="rounded bg-esotera-primary/10 px-1.5 py-0.5 text-esotera-primary">
                      Principal
                    </span>
                  ) : (
                    <Button
                      type="button"
                      variant="ghost"
                      className="h-8 px-2 text-xs"
                      disabled={busy}
                      onClick={() => void onSetPrimary(img.id)}
                    >
                      Definir principal
                    </Button>
                  )}
                  <Button
                    type="button"
                    variant="ghost"
                    className="h-8 px-2 text-xs"
                    disabled={busy || index === 0}
                    onClick={() => move(index, -1)}
                  >
                    ↑
                  </Button>
                  <Button
                    type="button"
                    variant="ghost"
                    className="h-8 px-2 text-xs"
                    disabled={busy || index === images.length - 1}
                    onClick={() => move(index, 1)}
                  >
                    ↓
                  </Button>
                  <Button
                    type="button"
                    variant="ghost"
                    className="h-8 px-2 text-xs text-esotera-error"
                    disabled={busy}
                    onClick={() => setDeleteId(img.id)}
                  >
                    Remover
                  </Button>
                </div>
                <FormField label="Texto alternativo" id={`alt-${img.id}`}>
                  <div className="flex gap-2">
                    <input
                      id={`alt-${img.id}`}
                      className={inputClassName}
                      value={altDrafts[img.id] ?? img.altText ?? ""}
                      onChange={(e) =>
                        setAltDrafts((d) => ({ ...d, [img.id]: e.target.value }))
                      }
                    />
                    <Button
                      type="button"
                      variant="secondary"
                      className="shrink-0"
                      disabled={busy}
                      onClick={() =>
                        void onUpdateAlt(img.id, altDrafts[img.id] ?? img.altText ?? "")
                      }
                    >
                      Salvar alt
                    </Button>
                  </div>
                </FormField>
              </div>
            </li>
          ))}
        </ul>
      )}

      <ConfirmModal
        open={Boolean(deleteId)}
        title="Remover imagem"
        description="A imagem será removida do produto. Pedidos antigos mantêm o snapshot."
        confirmLabel="Remover"
        busy={busy}
        onCancel={() => setDeleteId(null)}
        onConfirm={() => {
          if (!deleteId) return;
          void onDelete(deleteId).finally(() => setDeleteId(null));
        }}
      />
    </div>
  );
}
