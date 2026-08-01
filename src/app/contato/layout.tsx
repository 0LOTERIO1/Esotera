import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Contato",
  description:
    "Fale com a Esotera pelo Instagram, WhatsApp ou e-mail oficiais.",
  alternates: { canonical: "/contato" },
};

export default function ContatoLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return children;
}
