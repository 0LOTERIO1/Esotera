import { apiClient, ApiError } from "./apiClient";
import { sessionService } from "./sessionService";
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
        email: input.email,
        cpf: input.cpf,
        phone: input.phone,
        password: input.password,
      },
      { auth: false },
    );
    sessionService.setToken(response.token);
    return {
      token: response.token,
      user: mapApiUser(response.user, input.address),
    };
  },
};

export function toAuthUserMessage(error: unknown): string {
  if (error instanceof ApiError) return error.userMessage;
  if (error instanceof Error) return error.message;
  return "Falha na autenticação.";
}
