"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import Cropper from "react-easy-crop";
import type { Area } from "react-easy-crop";
import { Button } from "@/components/ui/Button";
import { cropImageFile, type CropAreaPixels } from "@/utils/imageCrop";
import { validateProductImage } from "@/utils/imageStorage";

export interface ImageEditorProps {
  file: File;
  onConfirm(file: File): void;
  onCancel(): void;
}

type AspectOption = {
  id: string;
  label: string;
  /** null = proporção original da imagem */
  ratio: number | null;
};

const ASPECTS: AspectOption[] = [
  { id: "square", label: "1:1", ratio: 1 },
  { id: "portrait", label: "4:5", ratio: 4 / 5 },
  { id: "landscape", label: "4:3", ratio: 4 / 3 },
  { id: "original", label: "Original", ratio: null },
];

const MIN_ZOOM = 1;
const MAX_ZOOM = 4;

/**
 * Etapa intermediária entre escolher o arquivo e enviar ao Cloudinary.
 * Permite zoom, reposicionamento, recorte e rotação sem downscale prévio:
 * o recorte é aplicado sobre o arquivo original.
 */
export function ImageEditor({ file, onConfirm, onCancel }: ImageEditorProps) {
  const [naturalAspect, setNaturalAspect] = useState<number | null>(null);
  const [aspectId, setAspectId] = useState("square");
  const [crop, setCrop] = useState({ x: 0, y: 0 });
  const [zoom, setZoom] = useState(MIN_ZOOM);
  const [rotation, setRotation] = useState(0);
  const [croppedArea, setCroppedArea] = useState<CropAreaPixels | null>(null);
  const [processing, setProcessing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fileError = useMemo(() => validateProductImage(file), [file]);
  const objectUrl = useMemo(
    () => (validateProductImage(file) ? null : URL.createObjectURL(file)),
    [file],
  );

  useEffect(() => {
    if (!objectUrl) return;
    return () => URL.revokeObjectURL(objectUrl);
  }, [objectUrl]);

  useEffect(() => {
    let cancelled = false;
    createImageBitmap(file)
      .then((bitmap) => {
        if (!cancelled) setNaturalAspect(bitmap.width / bitmap.height);
        bitmap.close();
      })
      .catch(() => {
        if (!cancelled) setNaturalAspect(1);
      });
    return () => {
      cancelled = true;
    };
  }, [file]);

  const aspect = useMemo(() => {
    const option = ASPECTS.find((a) => a.id === aspectId) ?? ASPECTS[0];
    return option.ratio ?? naturalAspect ?? 1;
  }, [aspectId, naturalAspect]);

  const onCropComplete = useCallback((_area: Area, areaPixels: Area) => {
    setCroppedArea(areaPixels);
  }, []);

  function reset() {
    setCrop({ x: 0, y: 0 });
    setZoom(MIN_ZOOM);
    setRotation(0);
    setAspectId("square");
    setError(null);
  }

  async function confirm() {
    if (!croppedArea) return;
    setProcessing(true);
    setError(null);
    try {
      const edited = await cropImageFile(file, croppedArea, rotation);
      const validation = validateProductImage(edited);
      if (validation) throw new Error(validation);
      onConfirm(edited);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Falha ao processar a imagem.");
    } finally {
      setProcessing(false);
    }
  }

  return (
    <div className="fixed inset-0 z-[60] flex items-end justify-center bg-esotera-secondary/50 p-0 sm:items-center sm:p-4">
      <div
        role="dialog"
        aria-modal
        aria-labelledby="image-editor-title"
        className="max-h-[95vh] w-full max-w-xl overflow-y-auto rounded-t-xl border border-esotera-border bg-esotera-surface p-4 shadow-xl sm:rounded-xl sm:p-6"
      >
        <h2 id="image-editor-title" className="font-serif text-xl text-esotera-secondary">
          Ajustar imagem
        </h2>
        <p className="mt-1 text-xs text-esotera-muted">
          Enquadre o produto antes de enviar. O recorte é aplicado sobre o arquivo
          original, sem perda extra de qualidade.
        </p>

        <div className="relative mt-4 h-72 overflow-hidden rounded-lg bg-esotera-secondary/90 sm:h-80">
          {objectUrl ? (
            <Cropper
              image={objectUrl}
              crop={crop}
              zoom={zoom}
              rotation={rotation}
              aspect={aspect}
              minZoom={MIN_ZOOM}
              maxZoom={MAX_ZOOM}
              restrictPosition
              showGrid
              onCropChange={setCrop}
              onZoomChange={setZoom}
              onCropComplete={onCropComplete}
            />
          ) : null}
        </div>

        <div className="mt-4 space-y-4">
          <div>
            <span className="block text-sm font-medium text-esotera-secondary">
              Proporção
            </span>
            <div className="mt-1.5 flex flex-wrap gap-2">
              {ASPECTS.map((option) => (
                <Button
                  key={option.id}
                  type="button"
                  variant={aspectId === option.id ? "primary" : "ghost"}
                  className="h-9 px-3 text-xs"
                  disabled={processing}
                  onClick={() => setAspectId(option.id)}
                >
                  {option.label}
                </Button>
              ))}
            </div>
          </div>

          <div>
            <label
              htmlFor="image-editor-zoom"
              className="block text-sm font-medium text-esotera-secondary"
            >
              Zoom
            </label>
            <input
              id="image-editor-zoom"
              type="range"
              min={MIN_ZOOM}
              max={MAX_ZOOM}
              step={0.01}
              value={zoom}
              disabled={processing}
              onChange={(e) => setZoom(Number(e.target.value))}
              className="mt-1.5 w-full accent-esotera-primary"
            />
          </div>

          <div className="flex flex-wrap gap-2">
            <Button
              type="button"
              variant="secondary"
              className="h-9 px-3 text-xs"
              disabled={processing}
              onClick={() => setRotation((r) => (r + 90) % 360)}
            >
              Girar 90°
            </Button>
            <Button
              type="button"
              variant="ghost"
              className="h-9 px-3 text-xs"
              disabled={processing}
              onClick={reset}
            >
              Redefinir
            </Button>
          </div>
        </div>

        {fileError || error ? (
          <p role="alert" className="mt-3 text-xs text-esotera-error">
            {fileError ?? error}
          </p>
        ) : null}

        <div className="mt-5 flex flex-col gap-2 sm:flex-row sm:justify-end">
          <Button type="button" variant="ghost" disabled={processing} onClick={onCancel}>
            Cancelar
          </Button>
          <Button
            type="button"
            disabled={processing || !croppedArea || !objectUrl || Boolean(fileError)}
            onClick={() => void confirm()}
          >
            {processing ? "Processando…" : "Confirmar imagem"}
          </Button>
        </div>
      </div>
    </div>
  );
}
