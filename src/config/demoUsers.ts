import type { User } from "@/types";

export const DEMO_PASSWORD_HINT = "demo123";

/** Endereço vazio — sem valores fictícios; o usuário preenche via ViaCEP. */
const emptyDemoAddress = {
  cep: "",
  street: "",
  number: "",
  complement: undefined as string | undefined,
  neighborhood: "",
  city: "",
  state: "SP",
};

export const demoCustomer: User = {
  id: "user-demo-customer",
  name: "Maria Silva Demonstração",
  email: "cliente@esotera.demo",
  cpf: "529.982.247-25",
  phone: "(11) 98888-0000",
  address: { ...emptyDemoAddress },
  role: "customer",
  createdAt: "2026-01-01T12:00:00.000Z",
};

export const demoAdmin: User = {
  id: "user-demo-admin",
  name: "Admin Esotera",
  email: "admin@esotera.demo",
  cpf: "390.533.447-05",
  phone: "(11) 97777-0000",
  address: { ...emptyDemoAddress },
  role: "admin",
  createdAt: "2026-01-01T12:00:00.000Z",
};
