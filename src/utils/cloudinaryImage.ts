/**
 * Transformações de entrega Cloudinary (não destrutivas no original).
 * Centraliza manipulação de URL — não espalhar string replace nos componentes.
 */

const CLOUDINARY_HOST = "res.cloudinary.com";

export type CloudinaryImageSize = "thumb" | "card" | "detail" | "full";

const SIZE_TRANSFORMS: Record<CloudinaryImageSize, string> = {
  thumb: "c_fill,w_120,h_120,f_auto,q_auto",
  card: "c_fill,w_480,h_480,f_auto,q_auto",
  detail: "c_limit,w_900,h_900,f_auto,q_auto",
  full: "f_auto,q_auto",
};

export function isCloudinaryUrl(url: string): boolean {
  try {
    const parsed = new URL(url);
    return parsed.hostname === CLOUDINARY_HOST;
  } catch {
    return false;
  }
}

/**
 * Insere transformações no path `/image/upload/` quando a URL é Cloudinary.
 * URLs legadas/locais são devolvidas sem alteração.
 */
export function withCloudinaryTransform(
  url: string,
  size: CloudinaryImageSize = "card",
): string {
  if (!isCloudinaryUrl(url)) return url;

  const transform = SIZE_TRANSFORMS[size];
  const marker = "/image/upload/";
  const idx = url.indexOf(marker);
  if (idx < 0) return url;

  const before = url.slice(0, idx + marker.length);
  const after = url.slice(idx + marker.length);

  // Já tem transformação (ex.: f_auto) — não duplicar
  if (/^[a-z0-9_,]+\//i.test(after) && !after.startsWith("v") && !after.startsWith("esotera/")) {
    // Heurística frágil; se começa com v123 (versão) ou pasta, ok inserir
  }
  if (after.startsWith("f_auto") || after.startsWith("c_") || after.startsWith("w_")) {
    return url;
  }

  return `${before}${transform}/${after}`;
}
