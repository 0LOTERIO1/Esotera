import { authApi, toAuthUserMessage } from "@/services/api/authApi";
import { sessionService } from "@/services/api/sessionService";
import { ApiError } from "@/services/api/apiClient";
import type { IAuthRepository, RegisterInput } from "./IAuthRepository";
import type { User } from "@/types";

/**
 * Auth via API .NET (JWT).
 * Botões demo usam as mesmas contas seed do backend.
 */
export class ApiAuthRepository implements IAuthRepository {
  async login(email: string, password: string): Promise<User> {
    try {
      const result = await authApi.login(email, password);
      return result.user;
    } catch (error) {
      throw new Error(toAuthUserMessage(error));
    }
  }

  async register(input: RegisterInput): Promise<User> {
    try {
      const result = await authApi.register(input);
      return result.user;
    } catch (error) {
      throw new Error(toAuthUserMessage(error));
    }
  }

  async loginDemoCustomer(): Promise<User> {
    return this.login("cliente@esotera.demo", "demo123");
  }

  async loginDemoAdmin(): Promise<User> {
    return this.login("admin@esotera.demo", "demo123");
  }

  async logout(): Promise<void> {
    sessionService.clear();
  }

  async restoreSession(): Promise<User | null> {
    const token = sessionService.getToken();
    if (!token) return null;
    try {
      return await authApi.me();
    } catch (error) {
      if (error instanceof ApiError && (error.status === 401 || error.status === 0)) {
        sessionService.clear();
        return null;
      }
      sessionService.clear();
      return null;
    }
  }
}
