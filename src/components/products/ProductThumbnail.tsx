"use client";

import { useState } from "react";
import { ImageOff } from "lucide-react";
import { normalizeProductImageUrl } from "@/utils/productImage";
import { withCloudinaryTransform } from "@/utils/cloudinaryImage";

type ProductThumbnailProps = {
  src?: string | null;
  alt: string;
  className?: string;
  sizeClassName?: string;
};

/**
 * Miniatura compacta para listagens (admin e similares).
 */
export function ProductThumbnail({
  src,
  alt,
  className = "",
  sizeClassName = "h-14 w-14",
}: ProductThumbnailProps) {
  const [failed, setFailed] = useState(false);
  const normalized = src?.trim()
    ? withCloudinaryTransform(normalizeProductImageUrl(src), "thumb")
    : null;
  const showImage = Boolean(normalized) && !failed;

  return (
    <div
      className={`relative shrink-0 overflow-hidden rounded-md border border-esotera-border bg-esotera-surface-secondary ${sizeClassName} ${className}`}
    >
      {showImage ? (
        // eslint-disable-next-line @next/next/no-img-element
        <img
          src={normalized!}
          alt={alt}
          className="absolute inset-0 h-full w-full object-cover"
          loading="lazy"
          decoding="async"
          onError={() => setFailed(true)}
        />
      ) : (
        <div
          className="absolute inset-0 flex flex-col items-center justify-center gap-0.5 px-1 text-esotera-muted"
          aria-hidden={!normalized}
        >
          <ImageOff size={16} strokeWidth={1.5} />
          <span className="text-[9px] leading-none">Sem foto</span>
        </div>
      )}
    </div>
  );
}
