/**
 * Validação e compressão client-side de imagens.
 * Em modo API o arquivo original vai ao backend (Cloudinary).
 * Em modo mock, Data URL é usado apenas como pré-visualização local.
 */

export const MAX_IMAGE_BYTES = 5 * 1024 * 1024;
const ACCEPTED = ["image/png", "image/jpeg", "image/jpg", "image/webp"];

export function validateProductImage(file: File): string | null {
  if (!ACCEPTED.includes(file.type)) {
    return "Formato inválido. Use PNG, JPG, JPEG ou WebP.";
  }
  if (file.size === 0) {
    return "Arquivo vazio.";
  }
  if (file.size > MAX_IMAGE_BYTES) {
    return "A imagem deve ter no máximo 5 MB.";
  }
  return null;
}

/**
 * Converte e comprime a imagem para Data URL (Base64).
 * Redimensiona para no máximo 1200px no maior lado.
 */
export async function fileToCompressedDataUrl(file: File): Promise<string> {
  const validationError = validateProductImage(file);
  if (validationError) {
    throw new Error(validationError);
  }

  const bitmap = await createImageBitmap(file);
  const maxSide = 1600;
  const scale = Math.min(1, maxSide / Math.max(bitmap.width, bitmap.height));
  const width = Math.max(1, Math.round(bitmap.width * scale));
  const height = Math.max(1, Math.round(bitmap.height * scale));

  const canvas = document.createElement("canvas");
  canvas.width = width;
  canvas.height = height;
  const ctx = canvas.getContext("2d");
  if (!ctx) {
    throw new Error("Não foi possível processar a imagem neste navegador.");
  }
  ctx.drawImage(bitmap, 0, 0, width, height);
  bitmap.close();

  const mime = file.type === "image/png" ? "image/png" : "image/jpeg";
  const quality = mime === "image/jpeg" ? 0.9 : undefined;
  const dataUrl = canvas.toDataURL(mime, quality);

  // Estimativa aproximada do tamanho em bytes do Base64
  const approxBytes = Math.ceil((dataUrl.length * 3) / 4);
  if (approxBytes > MAX_IMAGE_BYTES * 1.5) {
    throw new Error(
      "A imagem processada ainda está grande demais. Tente um arquivo menor.",
    );
  }

  return dataUrl;
}

export function isQuotaExceededError(error: unknown): boolean {
  return (
    error instanceof DOMException &&
    (error.name === "QuotaExceededError" || error.code === 22)
  );
}
