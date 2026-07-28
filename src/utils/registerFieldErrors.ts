import { ApiError } from "@/services/api/apiClient";

/** Campos do formulário de cadastro que podem receber erros da API. */
export type RegisterFormField =
  | "name"
  | "email"
  | "cpf"
  | "phone"
  | "password"
  | "confirmPassword"
  | "terms"
  | "privacy";

const FIELD_ALIASES: Record<string, RegisterFormField> = {
  name: "name",
  email: "email",
  cpf: "cpf",
  phone: "phone",
  password: "password",
  acceptedterms: "terms",
  acceptedprivacy: "privacy",
};

function normalizePropertyKey(key: string): string {
  return key
    .replace(/^\$\./, "")
    .replace(/^request\./i, "")
    .trim()
    .toLowerCase();
}

/**
 * Mapeia ValidationProblemDetails.errors da API para os campos do formulário.
 * Nunca associa erro de CPF ao e-mail (e vice-versa).
 */
export function mapRegisterApiFieldErrors(
  error: unknown,
): Partial<Record<RegisterFormField, string>> {
  if (!(error instanceof ApiError) || !error.errors) {
    return {};
  }

  const next: Partial<Record<RegisterFormField, string>> = {};

  for (const [rawKey, messages] of Object.entries(error.errors)) {
    const message = messages?.find((m) => typeof m === "string" && m.trim().length > 0);
    if (!message) continue;

    const key = normalizePropertyKey(rawKey);
    const field = FIELD_ALIASES[key];
    if (field && !next[field]) {
      next[field] = message;
    }
  }

  return next;
}

/** Fallback quando a API não envia errors por campo. */
export function inferRegisterErrorField(
  message: string,
): RegisterFormField | "form" {
  const lower = message.toLowerCase();
  if (lower.includes("cpf")) return "cpf";
  if (lower.includes("telefone") || lower.includes("phone")) return "phone";
  if (lower.includes("senha") || lower.includes("password")) return "password";
  if (lower.includes("nome") && !lower.includes("usuário")) return "name";
  if (lower.includes("e-mail") || lower.includes("email")) return "email";
  if (lower.includes("termo")) return "terms";
  if (lower.includes("privacidade")) return "privacy";
  return "form";
}
