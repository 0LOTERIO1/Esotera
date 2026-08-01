import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Criar conta",
  description: "Cadastre-se na Esotera para comprar tarôs e produtos esotéricos.",
  alternates: { canonical: "/cadastro" },
  robots: { index: false, follow: false },
};

export default function CadastroLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return children;
}
