import { storeConfig } from "@/config/store";

/**
 * URL canônica do site (SEO, OG, sitemap).
 * Preferir NEXT_PUBLIC_SITE_URL; fallback conhecido de produção.
 */
export function getSiteUrl(): string {
  const fromEnv = process.env.NEXT_PUBLIC_SITE_URL?.trim();
  if (fromEnv) return fromEnv.replace(/\/$/, "");

  const vercel = process.env.VERCEL_URL?.trim();
  if (vercel) {
    const host = vercel.replace(/^https?:\/\//, "").replace(/\/$/, "");
    return `https://${host}`;
  }

  return "https://esotera.vercel.app";
}

export const siteConfig = {
  name: storeConfig.name,
  description: storeConfig.description,
  tagline: storeConfig.tagline,
  locale: "pt_BR",
  ogImagePath: "/og-image.png",
} as const;
