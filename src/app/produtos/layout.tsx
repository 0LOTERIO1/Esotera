import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Produtos",
  description:
    "Catálogo Esotera — tarôs, oráculos e produtos esotéricos selecionados.",
  alternates: { canonical: "/produtos" },
};

export default function ProductsLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return children;
}
