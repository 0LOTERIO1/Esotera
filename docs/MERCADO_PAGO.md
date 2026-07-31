# Mercado Pago — Checkout Transparente (Orders API, fase 1: Pix)

## Variáveis

### Vercel (frontend)
- `NEXT_PUBLIC_MERCADO_PAGO_PUBLIC_KEY` — Public Key de **teste** da aplicação Orders
- `NEXT_PUBLIC_MERCADO_PAGO_ENVIRONMENT=Test` (opcional; espelho do backend — não substitui `GET /api/payments/config`)
- `NEXT_PUBLIC_DATA_MODE=api`
- `NEXT_PUBLIC_API_URL=https://esotera-api.onrender.com`
- `NEXT_PUBLIC_STORE_MODE=testing` (ou `production` no go-live)

Sem `NEXT_PUBLIC_MERCADO_PAGO_PUBLIC_KEY`, o Brick comercial **não** é ativado. Em Test, o botão isolado “Gerar Pix de teste de R$ 50,00” ainda funciona via API.

### Render (backend)
- `MercadoPago__Environment=Test` **ou** `MERCADO_PAGO_ENVIRONMENT=Test` — tipado (`Test` | `Production`). **Nunca** inferir pelo Access Token.
- `MercadoPago__AccessToken=` / `MERCADO_PAGO_ACCESS_TOKEN=` — Access Token (somente backend)
- `MercadoPago__WebhookSecret=` / `MERCADO_PAGO_WEBHOOK_SECRET=`
- `MercadoPago__SandboxPixEnabled=true` / `MERCADO_PAGO_SANDBOX_PIX_ENABLED=true` — só tem efeito em Test; em Production é forçado `false`
- `MercadoPago__SandboxPixAmount=50.00` / `MERCADO_PAGO_SANDBOX_PIX_AMOUNT=50.00`
- `PUBLIC_API_BASE_URL=https://esotera-api.onrender.com`
- `MERCADO_PAGO_NOTIFICATION_URL` (opcional)

## Sandbox vs produção

| | Test | Production |
|---|---|---|
| Payer no checkout comercial | `APRO` + `test_user_br@testuser.com` | dados reais do cliente |
| Valor comercial | só se o total do pedido for R$ 50,00 (não altera o pedido) | valor real |
| `POST /api/payments/sandbox/pix-test` | Pix isolado R$ 50, sem pedido/estoque/cupom | **403 Forbidden** |
| Banner no front | “Ambiente de teste…” | oculto |

Conta compradora de teste do painel Mercado Pago **não** substitui o login Esotera.

## Webhook

```
https://esotera-api.onrender.com/api/webhooks/mercadopago
```

Evento: **Order (Mercado Pago)** — topic `order` (não `payment`).

Comportamento:
- `external_reference` com prefixo `teste_esotera_pix_50_` → ignorado (não atualiza pedido)
- order inexistente → 200, ignorado
- valor incompatível com o pedido → ignorado
- notificação repetida → idempotente

## Fluxo (fase 1 — somente Pix)
1. `POST /api/orders` → pedido `awaiting_payment`
2. Front abre `/pagamento/{id}`
3. Em Test: opção isolada de Pix R$ 50 **ou** checkout comercial (só se total = R$ 50)
4. Comercial: `POST /api/orders/{id}/payments` → `POST /v1/orders`
5. Webhook `order.*` → `GET /v1/orders/{ORD…}` → atualiza pedido
6. Retorno do navegador **nunca** marca como pago

## Endpoints Mercado Pago usados
| Operação | Endpoint |
|---|---|
| Criar Pix | `POST /v1/orders` |
| Consultar | `GET /v1/orders/{orderId}` |

**Não** usamos `POST /v1/payments` nesta fase.

## Regras
- Access Token nunca no navegador, Git, logs ou README com valor
- Status só via webhook/consulta autenticada
- Idempotência: `Idempotency-Key` / `X-Idempotency-Key`
- Cartão e boleto rejeitados no backend até fase seguinte
- Logs de erro MP: status HTTP, code, message, causes, request id, corpo sanitizado (sem QR / token / PII desnecessária)
- Mock/simulação de aprovação: apenas `NEXT_PUBLIC_DATA_MODE=mock`
