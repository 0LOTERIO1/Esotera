import type { Product } from "@/types";

/**
 * Catálogo inicial (mock / seed local).
 * Produto 1: dados confirmados (nome, preço e conteúdo básico).
 * Produtos 2–6: nomes e preços de referência pública da loja.
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
    variations: [
      {
        id: "trad-taro-livro",
        name: "Tarô + Livro",
        price: 39.9,
        isAvailable: true,
      },
      {
        id: "trad-somente-taro",
        name: "Somente Tarô",
        price: 39.9,
        isAvailable: true,
      },
      {
        id: "trad-somente-livro",
        name: "Somente Livro",
        price: 0,
        isAvailable: false,
      },
    ],
    isFeatured: true,
    isAvailable: true,
  },
  {
    id: "prod-lenormand-primavera",
    slug: "taro-cigano-lenormand-primavera-rosa",
    name: "Tarô Cigano Baralho Lenormand Primavera Rosa",
    shortDescription: "Baralho Lenormand com 36 cartas e manual.",
    description:
      "Baralho Lenormand Primavera Rosa com 36 cartas e manual. Ideal para leituras práticas e estudos do sistema Lenormand.",
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
    shortDescription: "Manual explicativo de aproximadamente 160 páginas.",
    description:
      "Manual explicativo para o Tarot de Waite, com conteúdo pensado para apoiar iniciantes e leitores em aprofundamento.",
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
      "Conjunto de toalha e saquinho para cuidado das cartas.",
    description:
      "Toalha roxa com saquinho para guardar e proteger cartas de tarô durante o uso e o armazenamento.",
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
    shortDescription: "Baralho temático com 78 cartas e livro.",
    description:
      "Tarô das Bruxas com 78 cartas e livro explicativo, com estética e simbolismo voltados a leituras intuitivas.",
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
    slug: "rider-waite-taro-esotera-para-iniciante",
    name: "Rider Waite Tarô Esotera para Iniciante com 78 Cartas, Ilustrações e Explicações nas Cartas",
    shortDescription:
      "Edição para iniciantes com ilustrações e explicações nas cartas.",
    description:
      "Rider Waite para iniciantes com 78 cartas ilustradas e explicativas. Escolha entre somente o tarô ou o kit com livro.",
    price: 54.9,
    category: "Tarôs",
    images: ["/images/products/waite-iniciante.png"],
    features: [
      "78 cartas",
      "Ilustrações e explicações nas cartas",
      "Ideal para iniciantes",
    ],
    packageContents: ["78 cartas ilustradas"],
    variations: [
      {
        id: "var-somente-taro",
        name: "Somente Tarô",
        price: 54.9,
        isAvailable: true,
        sku: "SKU-WAITE-TAROT",
      },
      {
        id: "var-taro-livro",
        name: "Tarô + Livro",
        price: 79.9,
        isAvailable: true,
        sku: "SKU-WAITE-KIT",
      },
      {
        id: "var-somente-livro",
        name: "Somente Livro",
        price: 0,
        isAvailable: false,
        sku: "SKU-WAITE-LIVRO",
      },
    ],
    isFeatured: true,
    isAvailable: true,
    isDemo: false,
  },
];

export const productCategories = [
  "Tarôs",
  "Livros",
  "Acessórios",
] as const;
