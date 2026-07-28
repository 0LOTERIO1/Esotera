export type DataMode = "mock" | "api";

/**
 * Fonte de dados do frontend.
 * Padrão: mock (localStorage) — usado na Vercel até existir API pública.
 * Defina NEXT_PUBLIC_DATA_MODE=api apenas em desenvolvimento local com a API rodando.
 */
export function getDataMode(): DataMode {
  const mode = process.env.NEXT_PUBLIC_DATA_MODE?.toLowerCase();
  return mode === "api" ? "api" : "mock";
}

export function isApiMode(): boolean {
  return getDataMode() === "api";
}

export function isMockMode(): boolean {
  return getDataMode() === "mock";
}
