export const storeConfig = {
  name: "Esotera",
  legalName: "Esotera Livraria",
  tagline: "Autoconhecimento e espiritualidade através do tarô",
  description:
    "Loja especializada em tarôs e produtos esotéricos selecionados com cuidado.",
  /** Contatos oficiais ainda não fornecidos — não exibir na UI pública */
  email: null as string | null,
  phone: null as string | null,
  whatsapp: null as string | null,
  /** Origem operacional — uso interno de frete; não exibir na UI pública */
  address: {
    street: "",
    city: "São Paulo",
    state: "SP",
    cep: "",
  },
  social: {
    instagram: null as string | null,
    facebook: null as string | null,
    youtube: null as string | null,
  },
} as const;
