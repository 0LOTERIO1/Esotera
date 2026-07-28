# Esotera — E-commerce de produtos esotéricos

Loja de tarôs e produtos esotéricos com frontend Next.js e backend ASP.NET Core.

**Dois modos de operação:**
- **Mock** (padrão): dados simulados no `localStorage`, ideal para desenvolvimento e deploy na Vercel
- **API**: integração completa com backend ASP.NET Core + PostgreSQL

## Arquitetura

```mermaid
graph LR
    A[Frontend Next.js] -->|NEXT_PUBLIC_DATA_MODE=mock| B[localStorage]
    A -->|NEXT_PUBLIC_DATA_MODE=api| C[API ASP.NET]
    C --> D[Entity Framework]
    D --> E[PostgreSQL]
```

## Tecnologias

### Frontend
- Next.js (App Router)
- TypeScript
- Tailwind CSS
- Zustand
- Lucide React

### Backend
- ASP.NET Core 9.0
- Entity Framework Core
- PostgreSQL
- JWT Authentication

## Instalação

### Frontend

```bash
npm install
```

### Backend (opcional, apenas para modo API)

```bash
cd backend
dotnet restore
```

Configure `backend/appsettings.local.json` com sua connection string PostgreSQL (veja `backend/README.md` para detalhes).

## Execução

### Modo Mock (padrão - localStorage)

```bash
npm run dev
```

Acesse [http://localhost:3000](http://localhost:3000).

Neste modo, todos os dados ficam no navegador. Ideal para desenvolvimento sem backend e para o deploy na Vercel.

### Modo API (com backend)

1. Inicie o backend:

```bash
cd backend
dotnet run --project Esotera.Api
```

Backend disponível em [http://localhost:5080](http://localhost:5080).

2. Configure o frontend:

Crie `.env.local`:
```bash
NEXT_PUBLIC_DATA_MODE=api
NEXT_PUBLIC_API_URL=http://localhost:5080
```

3. Inicie o frontend:

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

### Modo Mock

| Perfil | E-mail | Senha |
|--------|--------|-------|
| Cliente | `cliente@esotera.demo` | `demo123` |
| Admin | `admin@esotera.demo` | `demo123` |

Na tela de login há botões de acesso rápido para login demo (apenas no modo mock).

### Modo API

Veja `backend/README.md` para credenciais de seed do banco de dados.

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
  app/                # Rotas (home, produtos, carrinho, checkout, conta, admin)
  components/         # UI, layout, home, cart, checkout, admin
  config/             # Loja, frete, cupom, tema, demos, dataMode
  data/               # Produtos e estados (seed para modo mock)
  hooks/
  services/
    api/              # API clients (authApi, productsApi, ordersApi, etc)
    repositories/     # Camada de abstração (IAuthRepository, etc)
                      # - Mock*Repository: usa localStorage
                      # - Api*Repository: usa API clients
    auth/             # mockAuthService (legacy)
    products/         # productRepository (legacy)
    coupon/           # mockCouponService (legacy)
    shipping/         # mockShippingService
    payment/          # mockPaymentService
  stores/             # Zustand + persist (usa repositories)
  types/
  utils/
public/images/products/  # Placeholders locais
backend/              # Backend ASP.NET Core (veja backend/README.md)
```

## Variáveis de ambiente

Copie `.env.example` para `.env.local` para configurar:

```bash
# Modo de dados: "mock" (padrão) ou "api"
NEXT_PUBLIC_DATA_MODE=mock

# URL da API (apenas para modo "api")
NEXT_PUBLIC_API_URL=http://localhost:5080

# Integrações futuras (Mercado Pago, Melhor Envio, J3)
NEXT_PUBLIC_MERCADO_PAGO_PUBLIC_KEY=
MERCADO_PAGO_ACCESS_TOKEN=
MELHOR_ENVIO_CLIENT_ID=
MELHOR_ENVIO_CLIENT_SECRET=
J3_API_URL=
J3_API_TOKEN=
```

**Importante:** O padrão `NEXT_PUBLIC_DATA_MODE=mock` garante que o deploy na Vercel funcione sem backend.

## Deploy

### Frontend na Vercel

O frontend pode ser publicado na **Vercel** no modo mock (sem backend):

```bash
npx vercel --prod
```

**Importante:** Não configure `NEXT_PUBLIC_DATA_MODE` ou `NEXT_PUBLIC_API_URL` nas variáveis de ambiente da Vercel. O padrão é modo mock, que funciona perfeitamente sem backend.

URL pública: após o deploy, a Vercel gera um endereço `*.vercel.app`. Quando houver domínio próprio, basta apontá-lo no painel da Vercel.

### Backend (opcional)

O backend ASP.NET Core pode ser hospedado em:
- Azure App Service
- AWS Elastic Beanstalk
- Google Cloud Run
- Servidor VPS com Docker

Veja `backend/README.md` para instruções de deploy.

## Próximos passos

- Integrações de pagamento (Mercado Pago)
- Integrações de frete (Melhor Envio, J3)
- Exportação de pedidos (UpSeller)
- Otimização de imagens
- Testes E2E
- CI/CD

## Notas

- A loja na Shopee bloqueou o acesso automatizado. Imagens oficiais não foram baixadas.
- O produto principal (Tarô de Waite, R$ 39,90) usa dados confirmados publicamente.
- Demais itens usam nome/preço de referência e conteúdo marcado como demonstração.
