import { apiClient, ApiError } from "./apiClient";
import { sessionService } from "./sessionService";
import { onlyDigits } from "@/utils/validation";
import type { User, UserRole, Address } from "@/types";

export type ApiUserDto = {
  id: string;
  name: string;
  email: string;
  cpf?: string | null;
  phone?: string | null;
  role: string;
  createdAt?: string;
};

export type ApiAuthResponse = {
  token: string;
  user: ApiUserDto;
};

type LoginRequest = {
  email: string;
  password: string;
};

function normalizeRole(role: string): UserRole {
  return role.toLowerCase() === "admin" ? "admin" : "customer";
}

function emptyAddress(): Address {
  return {
    cep: "",
    street: "",
    number: "",
    neighborhood: "",
    city: "",
    state: "",
  };
}

export function mapApiUser(dto: ApiUserDto, address?: Address): User {
  return {
    id: dto.id,
    name: dto.name,
    email: dto.email,
    cpf: dto.cpf ?? "",
    phone: dto.phone ?? "",
    role: normalizeRole(dto.role),
    address: address ?? emptyAddress(),
    createdAt: dto.createdAt ?? new Date().toISOString(),
  };
}

export const authApi = {
  async login(email: string, password: string): Promise<{ token: string; user: User }> {
    const body: LoginRequest = { email, password };
    // Rota pública — sem Authorization
    const response = await apiClient.post<ApiAuthResponse>("/api/auth/login", body, {
      auth: false,
    });
    sessionService.setToken(response.token);
    return {
      token: response.token,
      user: mapApiUser(response.user),
    };
  },

  async me(): Promise<User> {
    const response = await apiClient.get<ApiUserDto>("/api/auth/me", { auth: true });
    return mapApiUser(response);
  },

  /** Compatível com cadastro futuro; não usado no painel admin nesta fase */
  async register(input: {
    name: string;
    email: string;
    cpf: string;
    phone: string;
    password: string;
    address: Address;
  }): Promise<{ token: string; user: User }> {
    const response = await apiClient.post<ApiAuthResponse>(
      "/api/auth/register",
      {
        name: input.name,
        email: input.email.trim(),
        // Normaliza antes da validação da API (remove máscara).
        cpf: onlyDigits(input.cpf) || null,
        phone: onlyDigits(input.phone) || null,
        password: input.password,
        acceptedTerms: true,
        acceptedPrivacy: true,
      },
      { auth: false },
    );
    sessionService.setToken(response.token);
    return {
      token: response.token,
      user: mapApiUser(response.user, input.address),
    };
  },

  async forgotPassword(email: string): Promise<{ message: string }> {
    return apiClient.post(
      "/api/auth/forgot-password",
      { email },
      { auth: false },
    );
  },

  async resetPassword(input: {
    token: string;
    newPassword: string;
    confirmPassword: string;
  }): Promise<{ message: string }> {
    return apiClient.post("/api/auth/reset-password", input, { auth: false });
  },
};

export function toAuthUserMessage(error: unknown): string {
  if (error instanceof ApiError) return error.userMessage;
  if (error instanceof Error) return error.message;
  return "Falha na autenticação.";
}
