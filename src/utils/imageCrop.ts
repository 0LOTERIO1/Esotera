/**
 * Recorte/rotação client-side preservando qualidade.
 * O recorte é aplicado sobre o arquivo original (sem downscale prévio) e só
 * reduz resolução/qualidade se o resultado passar do limite aceito pela API.
 */

import { MAX_IMAGE_BYTES } from "./imageStorage";

export type CropAreaPixels = {
  x: number;
  y: number;
  width: number;
  height: number;
};

/** Maior lado permitido no arquivo final: alto o suficiente para zoom no storefront. */
const MAX_OUTPUT_SIDE = 2000;
const JPEG_QUALITY_STEPS = [0.92, 0.86, 0.8, 0.72];

type OutputMime = "image/jpeg" | "image/png" | "image/webp";

function outputMimeFor(file: File): OutputMime {
  if (file.type === "image/png") return "image/png";
  if (file.type === "image/webp") return "image/webp";
  return "image/jpeg";
}

function extensionFor(mime: OutputMime): string {
  if (mime === "image/png") return "png";
  if (mime === "image/webp") return "webp";
  return "jpg";
}

function rotatedBoundingBox(width: number, height: number, degrees: number) {
  const radians = (degrees * Math.PI) / 180;
  return {
    width: Math.abs(Math.cos(radians) * width) + Math.abs(Math.sin(radians) * height),
    height: Math.abs(Math.sin(radians) * width) + Math.abs(Math.cos(radians) * height),
  };
}

function canvasToBlob(
  canvas: HTMLCanvasElement,
  mime: OutputMime,
  quality?: number,
): Promise<Blob> {
  return new Promise((resolve, reject) => {
    canvas.toBlob(
      (blob) => {
        if (blob) resolve(blob);
        else reject(new Error("Não foi possível processar a imagem neste navegador."));
      },
      mime,
      quality,
    );
  });
}

function context2d(canvas: HTMLCanvasElement): CanvasRenderingContext2D {
  const ctx = canvas.getContext("2d");
  if (!ctx) {
    throw new Error("Não foi possível processar a imagem neste navegador.");
  }
  ctx.imageSmoothingEnabled = true;
  ctx.imageSmoothingQuality = "high";
  return ctx;
}

function scaleCanvas(source: HTMLCanvasElement, scale: number): HTMLCanvasElement {
  const target = document.createElement("canvas");
  target.width = Math.max(1, Math.round(source.width * scale));
  target.height = Math.max(1, Math.round(source.height * scale));
  context2d(target).drawImage(source, 0, 0, target.width, target.height);
  return target;
}

/**
 * Baixa uma imagem já publicada (Cloudinary) como File editável.
 * Usa a URL original, sem transformação, para não reeditar sobre um derivado.
 */
export async function fetchImageAsFile(url: string, fileName = "imagem"): Promise<File> {
  let response: Response;
  try {
    response = await fetch(url, { mode: "cors", credentials: "omit" });
  } catch {
    throw new Error("Não foi possível carregar a imagem atual para edição.");
  }
  if (!response.ok) {
    throw new Error("Não foi possível carregar a imagem atual para edição.");
  }

  const blob = await response.blob();
  const mime = blob.type || "image/jpeg";
  const extension = mime.split("/")[1]?.split("+")[0] || "jpg";
  const baseName = fileName.replace(/\.[^.]+$/, "") || "imagem";

  return new File([blob], `${baseName}.${extension}`, {
    type: mime,
    lastModified: Date.now(),
  });
}

/**
 * Aplica rotação + recorte e devolve um File pronto para upload.
 * `crop` usa coordenadas em pixels da imagem original (formato do react-easy-crop).
 */
export async function cropImageFile(
  file: File,
  crop: CropAreaPixels,
  rotation = 0,
): Promise<File> {
  const bitmap = await createImageBitmap(file);

  try {
    const box = rotatedBoundingBox(bitmap.width, bitmap.height, rotation);
    const rotated = document.createElement("canvas");
    rotated.width = Math.max(1, Math.round(box.width));
    rotated.height = Math.max(1, Math.round(box.height));

    const rotatedCtx = context2d(rotated);
    rotatedCtx.translate(rotated.width / 2, rotated.height / 2);
    rotatedCtx.rotate((rotation * Math.PI) / 180);
    rotatedCtx.drawImage(bitmap, -bitmap.width / 2, -bitmap.height / 2);

    let output = document.createElement("canvas");
    output.width = Math.max(1, Math.round(crop.width));
    output.height = Math.max(1, Math.round(crop.height));
    context2d(output).drawImage(
      rotated,
      Math.round(crop.x),
      Math.round(crop.y),
      output.width,
      output.height,
      0,
      0,
      output.width,
      output.height,
    );

    const longestSide = Math.max(output.width, output.height);
    if (longestSide > MAX_OUTPUT_SIDE) {
      output = scaleCanvas(output, MAX_OUTPUT_SIDE / longestSide);
    }

    const mime = outputMimeFor(file);
    const blob = await encodeWithinLimit(output, mime);
    const baseName = file.name.replace(/\.[^.]+$/, "") || "produto";

    return new File([blob], `${baseName}.${extensionFor(mime)}`, {
      type: mime,
      lastModified: Date.now(),
    });
  } finally {
    bitmap.close();
  }
}

/**
 * Codifica priorizando qualidade; só degrada (qualidade e depois resolução)
 * se o arquivo exceder o limite de 5 MB aceito pelo backend.
 */
async function encodeWithinLimit(
  canvas: HTMLCanvasElement,
  mime: OutputMime,
): Promise<Blob> {
  if (mime === "image/png") {
    const png = await canvasToBlob(canvas, mime);
    if (png.size <= MAX_IMAGE_BYTES) return png;
    // PNG sem perda estourou o limite: converte para JPEG mantendo resolução.
    return encodeWithinLimit(canvas, "image/jpeg");
  }

  let current = canvas;
  for (let attempt = 0; attempt < 4; attempt += 1) {
    for (const quality of JPEG_QUALITY_STEPS) {
      const blob = await canvasToBlob(current, mime, quality);
      if (blob.size <= MAX_IMAGE_BYTES) return blob;
    }
    current = scaleCanvas(current, 0.8);
  }

  throw new Error(
    "A imagem ainda ficou acima de 5 MB após o ajuste. Use um arquivo menor.",
  );
}
