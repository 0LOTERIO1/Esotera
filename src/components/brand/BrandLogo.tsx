"use client";

import Image from "next/image";
import Link from "next/link";

type BrandLogoProps = {
  variant?: "dark" | "white";
  className?: string;
  priority?: boolean;
  href?: string | null;
};

const sizes = {
  dark: { width: 168, height: 40 },
  white: { width: 168, height: 40 },
};

export function BrandLogo({
  variant = "dark",
  className = "",
  priority,
  href = "/",
}: BrandLogoProps) {
  const src =
    variant === "white"
      ? "/images/brand/esotera-logo-white.png"
      : "/images/brand/esotera-logo-dark.png";
  const { width, height } = sizes[variant];

  const image = (
    <Image
      src={src}
      alt="Esotera"
      width={width}
      height={height}
      priority={priority}
      className={`h-8 w-auto sm:h-9 ${className}`}
      sizes="(max-width: 640px) 140px, 168px"
    />
  );

  if (href === null) return image;

  return (
    <Link href={href} className="inline-flex items-center" aria-label="Esotera">
      {image}
    </Link>
  );
}
