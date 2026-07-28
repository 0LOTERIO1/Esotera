import type { User, Address } from "@/types";

export type RegisterInput = {
  name: string;
  email: string;
  cpf: string;
  phone: string;
  address: Address;
  password: string;
};

export interface IAuthRepository {
  login(email: string, password: string): Promise<User>;
  register(input: RegisterInput): Promise<User>;
  loginDemoCustomer(): Promise<User>;
  loginDemoAdmin(): Promise<User>;
  logout(): Promise<void>;
  /** Restaura sessão via GET /api/auth/me (modo API) */
  restoreSession?(): Promise<User | null>;
}
