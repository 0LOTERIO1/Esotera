import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Termos de uso",
  description: "Termos de uso da loja Esotera.",
  alternates: { canonical: "/termos" },
};

export default function TermosLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return children;
}
