export type DataMode = "mock" | "api";

/**
 * Fonte de dados do frontend.
 * Em produção (Vercel), use NEXT_PUBLIC_DATA_MODE=api com NEXT_PUBLIC_API_URL apontando para a API no Render.
 * O catálogo oficial deve vir do Neon via API/admin — não de products.ts.
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
