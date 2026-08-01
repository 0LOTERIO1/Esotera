import type { Metadata } from "next";
import { ProductDetailClient } from "./ProductDetailClient";
import { getSiteUrl, siteConfig } from "@/config/site";
import { storeConfig } from "@/config/store";

type PageProps = {
  params: Promise<{ slug: string }>;
};

type ProductSeo = {
  name?: string;
  shortDescription?: string | null;
  description?: string | null;
  price?: number;
  images?: Array<string | { secureUrl?: string }>;
  isAvailable?: boolean;
  category?: string;
};

async function fetchProductForSeo(slug: string): Promise<ProductSeo | null> {
  const api =
    process.env.NEXT_PUBLIC_API_URL?.replace(/\/$/, "") ||
    "http://localhost:5080";
  try {
    const res = await fetch(`${api}/api/products/${encodeURIComponent(slug)}`, {
      next: { revalidate: 300 },
    });
    if (res.status === 404) return null;
    if (!res.ok) return null;
    return (await res.json()) as ProductSeo;
  } catch {
    return null;
  }
}

function primaryImageUrl(product: ProductSeo): string | undefined {
  const first = product.images?.[0];
  if (!first) return undefined;
  if (typeof first === "string") return first;
  return first.secureUrl;
}

export async function generateMetadata({
  params,
}: PageProps): Promise<Metadata> {
  const { slug } = await params;
  const product = await fetchProductForSeo(slug);
  const siteUrl = getSiteUrl();

  if (!product?.name) {
    return {
      title: "Produto",
      description: storeConfig.description,
      alternates: { canonical: `/produtos/${slug}` },
    };
  }

  const description =
    (product.shortDescription || product.description || "").trim() ||
    storeConfig.description;
  const image = primaryImageUrl(product) || siteConfig.ogImagePath;

  return {
    title: product.name,
    description,
    alternates: { canonical: `/produtos/${slug}` },
    openGraph: {
      type: "website",
      title: product.name,
      description,
      url: `${siteUrl}/produtos/${slug}`,
      images: [{ url: image, alt: product.name }],
      siteName: storeConfig.name,
      locale: siteConfig.locale,
    },
    twitter: {
      card: "summary_large_image",
      title: product.name,
      description,
      images: [image],
    },
  };
}

function ProductJsonLd({
  slug,
  product,
}: {
  slug: string;
  product: ProductSeo;
}) {
  const siteUrl = getSiteUrl();
  const image = primaryImageUrl(product);
  const description =
    (product.shortDescription || product.description || "").trim() ||
    storeConfig.description;

  // Sem inventar avaliações, estoque numérico ou dados empresariais inexistentes.
  const data: Record<string, unknown> = {
    "@context": "https://schema.org",
    "@type": "Product",
    name: product.name,
    description,
    url: `${siteUrl}/produtos/${slug}`,
    brand: {
      "@type": "Brand",
      name: storeConfig.name,
    },
  };

  if (image) data.image = [image];
  if (product.category) data.category = product.category;
  if (typeof product.price === "number") {
    data.offers = {
      "@type": "Offer",
      url: `${siteUrl}/produtos/${slug}`,
      priceCurrency: "BRL",
      price: product.price.toFixed(2),
      availability:
        product.isAvailable === false
          ? "https://schema.org/OutOfStock"
          : "https://schema.org/InStock",
      seller: {
        "@type": "Organization",
        name: storeConfig.legalName,
      },
    };
  }

  return (
    <script
      type="application/ld+json"
      dangerouslySetInnerHTML={{ __html: JSON.stringify(data) }}
    />
  );
}

export default async function ProductDetailPage({ params }: PageProps) {
  const { slug } = await params;
  const product = await fetchProductForSeo(slug);

  return (
    <>
      {product?.name ? <ProductJsonLd slug={slug} product={product} /> : null}
      <ProductDetailClient params={Promise.resolve({ slug })} />
    </>
  );
}
