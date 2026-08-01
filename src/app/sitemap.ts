import type { MetadataRoute } from "next";
import { getSiteUrl } from "@/config/site";

type ProductListItem = {
  slug?: string;
  updatedAtUtc?: string;
  isAvailable?: boolean;
  isArchived?: boolean;
};

async function fetchProductSlugs(): Promise<
  Array<{ slug: string; lastModified?: Date }>
> {
  const api =
    process.env.NEXT_PUBLIC_API_URL?.replace(/\/$/, "") ||
    "http://localhost:5080";
  try {
    const res = await fetch(`${api}/api/products`, {
      next: { revalidate: 3600 },
    });
    if (!res.ok) return [];
    const data = (await res.json()) as ProductListItem[];
    if (!Array.isArray(data)) return [];
    return data
      .filter((p) => p.slug && p.isAvailable !== false && !p.isArchived)
      .map((p) => ({
        slug: p.slug!,
        lastModified: p.updatedAtUtc ? new Date(p.updatedAtUtc) : undefined,
      }));
  } catch {
    return [];
  }
}

export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const siteUrl = getSiteUrl();
  const now = new Date();

  const staticRoutes: MetadataRoute.Sitemap = [
    {
      url: siteUrl,
      lastModified: now,
      changeFrequency: "weekly",
      priority: 1,
    },
    {
      url: `${siteUrl}/produtos`,
      lastModified: now,
      changeFrequency: "daily",
      priority: 0.9,
    },
    {
      url: `${siteUrl}/contato`,
      lastModified: now,
      changeFrequency: "monthly",
      priority: 0.6,
    },
    {
      url: `${siteUrl}/trocas-e-devolucoes`,
      lastModified: now,
      changeFrequency: "yearly",
      priority: 0.4,
    },
    {
      url: `${siteUrl}/termos`,
      lastModified: now,
      changeFrequency: "yearly",
      priority: 0.3,
    },
    {
      url: `${siteUrl}/privacidade`,
      lastModified: now,
      changeFrequency: "yearly",
      priority: 0.3,
    },
  ];

  const products = await fetchProductSlugs();
  const productRoutes: MetadataRoute.Sitemap = products.map((p) => ({
    url: `${siteUrl}/produtos/${p.slug}`,
    lastModified: p.lastModified ?? now,
    changeFrequency: "weekly",
    priority: 0.8,
  }));

  return [...staticRoutes, ...productRoutes];
}
