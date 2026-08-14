export function formatCurrency(value: number): string {
  return new Intl.NumberFormat("pt-BR", {
    style: "currency",
    currency: "BRL",
  }).format(value);
}

/** Prazo em dias úteis; null/undefined = desconhecido (nunca tratar 0 como unknown). */
export function formatEstimatedDays(days: number | null | undefined): string {
  if (days == null) return "Prazo a confirmar";
  if (days === 0) return "Hoje (mesmo dia)";
  if (days === 1) return "1 dia útil";
  return `${days} dias úteis`;
}

export function formatDate(iso: string): string {
  return new Intl.DateTimeFormat("pt-BR", {
    dateStyle: "short",
    timeStyle: "short",
    timeZone: "America/Sao_Paulo",
  }).format(new Date(iso));
}

export function generateId(prefix: string): string {
  return `${prefix}-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}`;
}
