# Esotera — Protótipo de e-commerce (etapa 1)

Protótipo visual e navegável de loja de tarôs e produtos esotéricos.
Todos os fluxos (carrinho, cadastro, frete, pagamento e admin) são **simulados localmente** com `localStorage`. Nenhuma cobrança real é realizada.

## Tecnologias

- Next.js (App Router)
- TypeScript
- Tailwind CSS
- Zustand
- Lucide React

## Instalação

```bash
npm install
```

## Execução

```bash
npm run dev
```

Acesse [http://localhost:3000](http://localhost:3000).

## Scripts

```bash
npm run lint
npm run build
npm run start
```

## Contas de demonstração

| Perfil | E-mail | Senha (qualquer texto no login manual) |
|--------|--------|----------------------------------------|
| Cliente | `cliente@esotera.demo` | `demo123` |
| Admin | `admin@esotera.demo` | `demo123` |

Na tela de login também há botões:

- **Entrar como usuário de demonstração**
- **Entrar como administrador de demonstração**

## Cupom de teste

- Código: `DESCONTO5`
- Desconto: R$ 5,00 nos produtos
- Compra mínima: R$ 30,00
- 1 uso por cliente (controlado no `localStorage`)

## Onde alterar configurações

| Item | Arquivo |
|------|---------|
| Nome e dados da loja | `src/config/store.ts` (também editável em `/admin/configuracoes`) |
| Cores / tema | `src/config/theme.ts` e `src/app/globals.css` |
| Produtos | `src/data/products.ts` |
| Frete grátis, J3, subsídio | `src/config/shipping.ts` |
| Cupom | `src/config/coupon.ts` |
| Usuários demo | `src/config/demoUsers.ts` |

### Ativar subsídio de frete

Em `src/config/shipping.ts` (ou no painel admin):

```ts
shippingSubsidy: {
  enabled: true, // padrão: false
  amount: 10
}
```

## Estrutura principal

```text
src/
  app/           # Rotas (home, produtos, carrinho, checkout, conta, admin)
  components/    # UI, layout, home, cart, checkout, admin
  config/        # Loja, frete, cupom, tema, demos
  data/          # Produtos e estados
  hooks/
  services/      # Auth, frete, pagamento, cupom (mocks)
  stores/        # Zustand + persist
  types/
  utils/
public/images/products/  # Placeholders locais (sem hotlink)
```

## Variáveis de ambiente

Copie `.env.example` para `.env.local` quando for integrar APIs reais.
Nesta etapa os valores ficam vazios — não há integrações ativas.

## Observações da Shopee

A loja na Shopee bloqueou o acesso automatizado. Imagens oficiais não foram baixadas.
O produto principal (Tarô de Waite, R$ 39,90) usa dados confirmados publicamente.
Demais itens usam nome/preço de referência e conteúdo marcado como demonstração.

## TODOs no código

- Cobertura oficial de CEP da J3 (`simulatedJ3CepRanges`)
- Subsídio de frete aguardando confirmação (`enabled: false`)
- Fotos e descrições oficiais a enviar pelo cliente
- Integrações futuras (Mercado Pago, Melhor Envio, J3, UpSeller)
