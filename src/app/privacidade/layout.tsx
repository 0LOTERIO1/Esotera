import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Privacidade",
  description: "Política de privacidade da Esotera.",
  alternates: { canonical: "/privacidade" },
};

export default function PrivacidadeLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return children;
}
