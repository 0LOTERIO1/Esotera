import { demoAdmin, demoCustomer } from "@/config/demoUsers";
import { STORAGE_KEYS, safeParseJSON } from "@/utils/storage";
import { generateId } from "@/utils/format";
import type { Address, User } from "@/types";

export type RegisterInput = {
  name: string;
  email: string;
  cpf: string;
  phone: string;
  address: Address;
  /** Senha não é armazenada em texto puro — apenas valida presença no fluxo */
  passwordProvided: boolean;
};

function readUsers(): User[] {
  if (typeof window === "undefined") return [demoCustomer, demoAdmin];
  const stored = safeParseJSON<User[]>(
    localStorage.getItem(STORAGE_KEYS.users),
    [],
  );
  const seed = [demoCustomer, demoAdmin];
  const merged = [...seed];
  for (const user of stored) {
    if (!merged.some((u) => u.email === user.email)) {
      merged.push(user);
    }
  }
  return merged;
}

function writeUsers(users: User[]) {
  const custom = users.filter(
    (u) => u.id !== demoCustomer.id && u.id !== demoAdmin.id,
  );
  localStorage.setItem(STORAGE_KEYS.users, JSON.stringify(custom));
}

export const mockAuthService = {
  listUsers(): User[] {
    return readUsers();
  },

  findByEmail(email: string): User | undefined {
    return readUsers().find(
      (u) => u.email.toLowerCase() === email.trim().toLowerCase(),
    );
  },

  register(input: RegisterInput): User {
    if (!input.passwordProvided) {
      throw new Error("Informe uma senha para continuar.");
    }
    const existing = this.findByEmail(input.email);
    if (existing) {
      throw new Error("Já existe uma conta com este e-mail.");
    }
    const user: User = {
      id: generateId("user"),
      name: input.name.trim(),
      email: input.email.trim().toLowerCase(),
      cpf: input.cpf,
      phone: input.phone,
      address: input.address,
      role: "customer",
      createdAt: new Date().toISOString(),
    };
    const users = readUsers();
    users.push(user);
    writeUsers(users);
    return user;
  },

  /**
   * Login simulado: não valida hash real.
   * Aceita qualquer senha não vazia para usuários cadastrados,
   * ou os e-mails de demonstração.
   */
  login(email: string, password: string): User {
    if (!password.trim()) {
      throw new Error("Informe a senha.");
    }
    const user = this.findByEmail(email);
    if (!user) {
      throw new Error("E-mail ou senha inválidos.");
    }
    return user;
  },

  loginAsDemoCustomer(): User {
    return demoCustomer;
  },

  loginAsDemoAdmin(): User {
    return demoAdmin;
  },
};
