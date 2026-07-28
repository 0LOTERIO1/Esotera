"use client";

import Image from "next/image";
import { useState } from "react";
import {
  normalizeProductImageUrl,
  PRODUCT_IMAGE_PLACEHOLDER,
} from "@/utils/productImage";

type ProductImageProps = {
  src: string;
  alt: string;
  fill?: boolean;
  width?: number;
  height?: number;
  className?: string;
  sizes?: string;
  priority?: boolean;
};

/**
 * Suporta PNG/SVG locais, /media/ da API e Data URL.
 * Em falha de carregamento, usa placeholder local (sem ícone quebrado).
 */
export function ProductImage({
  src,
  alt,
  fill,
  width,
  height,
  className = "",
  sizes,
  priority,
}: ProductImageProps) {
  const normalized = normalizeProductImageUrl(src);
  const [failedFor, setFailedFor] = useState<string | null>(null);
  const broken = failedFor === normalized;
  const resolved = broken ? PRODUCT_IMAGE_PLACEHOLDER : normalized;
  const isSvg = resolved.toLowerCase().endsWith(".svg");
  const isDataUrl = resolved.startsWith("data:");

  const handleError = () => {
    if (normalized !== PRODUCT_IMAGE_PLACEHOLDER) {
      setFailedFor(normalized);
    }
  };

  if (isSvg || isDataUrl) {
    if (fill) {
      return (
        // eslint-disable-next-line @next/next/no-img-element
        <img
          src={resolved}
          alt={alt}
          className={`absolute inset-0 h-full w-full object-cover ${className}`}
          loading={priority ? "eager" : "lazy"}
          decoding="async"
          onError={handleError}
        />
      );
    }

    return (
      // eslint-disable-next-line @next/next/no-img-element
      <img
        src={resolved}
        alt={alt}
        width={width ?? 400}
        height={height ?? 500}
        className={`object-cover ${className}`}
        loading={priority ? "eager" : "lazy"}
        decoding="async"
        onError={handleError}
      />
    );
  }

  if (fill) {
    return (
      <Image
        src={resolved}
        alt={alt}
        fill
        className={`object-cover ${className}`}
        sizes={sizes}
        priority={priority}
        onError={handleError}
      />
    );
  }

  return (
    <Image
      src={resolved}
      alt={alt}
      width={width ?? 400}
      height={height ?? 500}
      className={`object-cover ${className}`}
      sizes={sizes}
      priority={priority}
      onError={handleError}
    />
  );
}
