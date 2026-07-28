export const storeConfig = {
  name: "Esotera",
  legalName: "Esotera Livraria",
  tagline: "Autoconhecimento e espiritualidade através do tarô",
  description:
    "Loja especializada em tarôs e produtos esotéricos selecionados com cuidado.",
  email: "esoteralivraria1@gmail.com",
  phone: "(11) 97970-7396",
  whatsapp: "(11) 97970-7396",
  whatsappE164: "5511979707396",
  address: {
    street: "",
    city: "São Paulo",
    state: "SP",
    cep: "",
  },
  social: {
    instagram: "https://www.instagram.com/esotera_taro/",
    instagramHandle: "@esotera_taro",
    facebook: null as string | null,
    youtube: null as string | null,
  },
  includedCardNotice:
    "Todos os tarôs acompanham um cartão da Esotera com nossos canais oficiais no Instagram e WhatsApp.",
  whatsappExchangeMessage:
    "Olá! Preciso de ajuda com uma troca ou devolução na Esotera. Meu número do pedido é: ",
} as const;

export function whatsappHref(message?: string): string {
  const text = encodeURIComponent(message ?? "");
  return `https://wa.me/${storeConfig.whatsappE164}${text ? `?text=${text}` : ""}`;
}

export function mailtoHref(): string {
  return `mailto:${storeConfig.email}`;
}
