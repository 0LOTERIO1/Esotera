"use client";

import Image from "next/image";

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
 * Placeholders do protótipo são SVG locais.
 * Usa <img> nativo para SVG (next/image otimizado falha com frequência).
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
  const isSvg = src.toLowerCase().endsWith(".svg");

  if (isSvg) {
    if (fill) {
      return (
        // eslint-disable-next-line @next/next/no-img-element
        <img
          src={src}
          alt={alt}
          className={`absolute inset-0 h-full w-full ${className}`}
          loading={priority ? "eager" : "lazy"}
          decoding="async"
        />
      );
    }

    return (
      // eslint-disable-next-line @next/next/no-img-element
      <img
        src={src}
        alt={alt}
        width={width ?? 400}
        height={height ?? 500}
        className={className}
        loading={priority ? "eager" : "lazy"}
        decoding="async"
      />
    );
  }

  if (fill) {
    return (
      <Image
        src={src}
        alt={alt}
        fill
        className={className}
        sizes={sizes}
        priority={priority}
      />
    );
  }

  return (
    <Image
      src={src}
      alt={alt}
      width={width ?? 400}
      height={height ?? 500}
      className={className}
      sizes={sizes}
      priority={priority}
    />
  );
}
