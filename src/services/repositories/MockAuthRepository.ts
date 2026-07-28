import { mockAuthService } from "@/services/auth/mockAuthService";
import type { IAuthRepository, RegisterInput } from "./IAuthRepository";
import type { User } from "@/types";

export class MockAuthRepository implements IAuthRepository {
  async login(email: string, password: string): Promise<User> {
    return mockAuthService.login(email, password);
  }

  async register(input: RegisterInput): Promise<User> {
    return mockAuthService.register({
      ...input,
      passwordProvided: Boolean(input.password),
    });
  }

  async loginDemoCustomer(): Promise<User> {
    return mockAuthService.loginAsDemoCustomer();
  }

  async loginDemoAdmin(): Promise<User> {
    return mockAuthService.loginAsDemoAdmin();
  }

  async logout(): Promise<void> {
    // Mock não precisa fazer nada especial no logout
  }
}
