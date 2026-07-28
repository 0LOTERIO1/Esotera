/**
 * Normalização centralizada de URLs de imagem de produto.
 * Usado por pedidos (imagem congelada) e demais consumidores da API.
 */

import { apiClient } from "@/services/api/apiClient";

/** Placeholder local real em public/ — nunca hotlink externo */
export const PRODUCT_IMAGE_PLACEHOLDER =
  "/images/placeholder-product.svg";

/**
 * Aliases do seed do backend (caminhos .jpg que não existem em public/)
 * para os arquivos reais em public/images/products.
 */
const SEED_IMAGE_ALIASES: Record<string, string> = {
  "/images/products/waite-tradicional-1.jpg":
    "/images/products/waite-tradicional.png",
  "/images/products/waite-tradicional-2.jpg":
    "/images/products/waite-tradicional-2.png",
  "/images/products/waite-pocket-1.jpg":
    "/images/products/waite-iniciante.png",
  "/images/products/crowley-1.jpg": "/images/products/taro-bruxas.png",
  "/images/products/crowley-2.jpg": "/images/products/taro-bruxas.png",
  "/images/products/marselha-1.jpg":
    "/images/products/lenormand-primavera.png",
  "/images/products/78-graus-1.jpg": "/images/products/livro-waite.png",
  "/images/products/toalha-1.jpg": "/images/products/toalha-roxa.png",
};

function ensureLeadingSlash(path: string): string {
  if (path.startsWith("/") || path.startsWith("http") || path.startsWith("data:")) {
    return path;
  }
  return `/${path}`;
}

/**
 * Normaliza a URL de imagem congelada/retornada pela API.
 * - Preserva caminho válido local ou absoluto
 * - Corrige aliases conhecidos do seed
 * - Prefixa /media/ com a base da API
 * - Fallback para placeholder local
 */
export function normalizeProductImageUrl(
  imageUrl: string | null | undefined,
): string {
  const raw = imageUrl?.trim();
  if (!raw) return PRODUCT_IMAGE_PLACEHOLDER;

  // Data URL (upload mock) — preservar
  if (raw.startsWith("data:")) return raw;

  // Uploads do backend
  if (raw.startsWith("/media/") || raw.startsWith("media/")) {
    const path = ensureLeadingSlash(raw);
    return `${apiClient.getBaseUrl()}${path}`;
  }

  // URL absoluta http(s) — Cloudinary ou outra CDN HTTPS
  if (/^https?:\/\//i.test(raw)) {
    return raw;
  }

  const localPath = ensureLeadingSlash(raw);
  const aliased = SEED_IMAGE_ALIASES[localPath.toLowerCase()];
  if (aliased) return aliased;

  // Caminhos locais sob /images/ — manter (arquivo real em public/)
  if (localPath.startsWith("/images/")) {
    return localPath;
  }

  // Caminho relativo genérico com extensão de imagem
  if (/\.(png|jpe?g|webp|gif|svg)$/i.test(localPath)) {
    return localPath.startsWith("/images/")
      ? localPath
      : `/images/products/${localPath.replace(/^\/+/, "").split("/").pop()}`;
  }

  return PRODUCT_IMAGE_PLACEHOLDER;
}
