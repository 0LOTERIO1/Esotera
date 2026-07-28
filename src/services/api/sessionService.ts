/**
 * Armazenamento centralizado do JWT (fase 1: localStorage).
 * TODO: migrar para cookie HttpOnly / sessão mais segura quando houver backend público.
 */

const TOKEN_KEY = "esotera-api-token";

let tokenInMemory: string | null = null;
let unauthorizedHandler: (() => void) | null = null;

export const sessionService = {
  getToken(): string | null {
    if (tokenInMemory) return tokenInMemory;
    if (typeof window !== "undefined") {
      tokenInMemory = localStorage.getItem(TOKEN_KEY);
      return tokenInMemory;
    }
    return null;
  },

  setToken(token: string | null) {
    tokenInMemory = token;
    if (typeof window === "undefined") return;
    if (token) localStorage.setItem(TOKEN_KEY, token);
    else localStorage.removeItem(TOKEN_KEY);
  },

  clear() {
    this.setToken(null);
  },

  /** Chamado pelo apiClient em 401 de rotas protegidas */
  onUnauthorized(handler: () => void) {
    unauthorizedHandler = handler;
  },

  notifyUnauthorized() {
    this.clear();
    unauthorizedHandler?.();
  },
};
