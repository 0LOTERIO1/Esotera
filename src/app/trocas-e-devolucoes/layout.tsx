import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Trocas e devoluções",
  description: "Política de trocas e devoluções da Esotera.",
  alternates: { canonical: "/trocas-e-devolucoes" },
};

export default function TrocasLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return children;
}
