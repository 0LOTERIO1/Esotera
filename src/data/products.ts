import type { Product } from "@/types";

/**
 * Catálogo do protótipo.
 * Produto 1: dados confirmados (nome, preço e conteúdo básico).
 * Produtos 2–6: nomes e preços de referência pública da loja.
 * Imagens atuais: geradas por IA para demonstração — substituir pelas fotos oficiais do cliente.
 * TODO: substituir fotos de demonstração e textos pendentes por conteúdo oficial.
 */
export const initialProducts: Product[] = [
  {
    id: "prod-waite-tradicional",
    slug: "taro-de-waite-tradicional-livro-manual",
    name: "Tarô de Waite Tradicional, Livro e Manual com 78 Cartas Plastificadas",
    shortDescription:
      "Kit clássico Rider-Waite em português com 78 cartas plastificadas e manual.",
    description:
      "O Tradicional Tarô de Waite em Português com livro. Contém 78 cartas (22 Arcanos Maiores e 56 Arcanos Menores), tamanho 12 × 7 cm, com ilustrações de Pamela Colman Smith. Um dos tarôs mais conhecidos do mundo ocidental, intuitivo e rico em simbolismos — ideal para questões do dia a dia, psicológicas e espirituais.",
    price: 39.9,
    category: "Tarôs",
    images: [
      "/images/products/waite-tradicional.png",
      "/images/products/waite-tradicional-2.png",
    ],
    features: [
      "78 cartas plastificadas",
      "22 Arcanos Maiores e 56 Menores",
      "Tamanho 12 × 7 cm",
      "Ilustrações de Pamela Colman Smith",
      "Manual em português",
    ],
    packageContents: [
      "78 cartas do Tarô de Waite",
      "Livro / manual explicativo",
    ],
    variations: ["Tarô + Livro", "Somente Tarô", "Somente Livro"],
    isFeatured: true,
    isAvailable: true,
  },
  {
    id: "prod-lenormand-primavera",
    slug: "taro-cigano-lenormand-primavera-rosa",
    name: "Tarô Cigano Baralho Lenormand Primavera Rosa",
    shortDescription:
      "Baralho Lenormand com 36 cartas e manual. (Demonstração — descrição completa pendente.)",
    description:
      "[Demonstração] Baralho Lenormand Primavera Rosa com 36 cartas e manual. Descrição oficial e fotos precisam ser fornecidas pelo cliente. Nome e preço de referência pública da loja Esotera.",
    price: 39.9,
    category: "Tarôs",
    images: ["/images/products/lenormand-primavera.png"],
    features: ["36 cartas", "Manual incluso", "Tema Primavera Rosa"],
    packageContents: ["36 cartas Lenormand", "Manual"],
    isFeatured: true,
    isAvailable: true,
    isDemo: true,
  },
  {
    id: "prod-livro-waite",
    slug: "livro-de-waite-manual-explicativo",
    name: "Livro de Waite Manual Explicativo para Tarot de Waite",
    shortDescription:
      "Manual explicativo de aproximadamente 160 páginas. (Demonstração.)",
    description:
      "[Demonstração] Manual explicativo para o Tarot de Waite. Conteúdo detalhado e fotos oficiais pendentes de envio pelo cliente. Nome e preço de referência pública.",
    price: 39.9,
    category: "Livros",
    images: ["/images/products/livro-waite.png"],
    features: ["Manual em português", "Aproximadamente 160 páginas"],
    packageContents: ["1 livro / manual"],
    isFeatured: false,
    isAvailable: true,
    isDemo: true,
  },
  {
    id: "prod-toalha-roxa",
    slug: "toalha-roxa-saquinho-taro",
    name: "Toalha Roxa + Saquinho para Guardar Cartas de Tarô",
    shortDescription:
      "Conjunto de toalha e saquinho para cuidado das cartas. (Demonstração.)",
    description:
      "[Demonstração] Toalha roxa com saquinho para guardar cartas de tarô. Especificações e fotos oficiais precisam ser enviadas pelo cliente.",
    price: 49.9,
    category: "Acessórios",
    images: ["/images/products/toalha-roxa.png"],
    features: ["Toalha roxa", "Saquinho incluso"],
    packageContents: ["1 toalha", "1 saquinho"],
    isFeatured: false,
    isAvailable: true,
    isDemo: true,
  },
  {
    id: "prod-taro-bruxas",
    slug: "taro-das-bruxas-com-livro",
    name: "Tarô das Bruxas com 78 Cartas e Livro Explicativo",
    shortDescription:
      "Baralho temático com 78 cartas e livro. (Demonstração.)",
    description:
      "[Demonstração] Tarô das Bruxas com 78 cartas e livro explicativo. Descrição completa e imagens oficiais pendentes. Nome e preço de referência pública.",
    price: 29.9,
    category: "Tarôs",
    images: ["/images/products/taro-bruxas.png"],
    features: ["78 cartas", "Livro explicativo"],
    packageContents: ["78 cartas", "Livro"],
    isFeatured: true,
    isAvailable: true,
    isDemo: true,
  },
  {
    id: "prod-waite-iniciante",
    slug: "rider-waite-taro-para-iniciante",
    name: "Rider Waite Tarô Esotera para Iniciante",
    shortDescription:
      "Edição para iniciantes com ilustrações e explicações nas cartas. (Demonstração.)",
    description:
      "[Demonstração] Rider Waite para iniciantes com 78 cartas ilustradas e explicativas. Conteúdo oficial e fotos precisam ser fornecidos pelo cliente.",
    price: 54.9,
    category: "Tarôs",
    images: ["/images/products/waite-iniciante.png"],
    features: [
      "78 cartas",
      "Ilustrações e explicações nas cartas",
      "Ideal para iniciantes",
    ],
    packageContents: ["78 cartas ilustradas"],
    isFeatured: false,
    isAvailable: true,
    isDemo: true,
  },
];

export const productCategories = [
  "Tarôs",
  "Livros",
  "Acessórios",
] as const;
