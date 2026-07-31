# Mercado Pago — Checkout Transparente (Orders API, fase 1: Pix)

## Variáveis

### Vercel (frontend)
- `NEXT_PUBLIC_MERCADO_PAGO_PUBLIC_KEY` — Public Key de **teste** da aplicação Orders
- `NEXT_PUBLIC_DATA_MODE=api`
- `NEXT_PUBLIC_API_URL=https://esotera-api.onrender.com`
- `NEXT_PUBLIC_STORE_MODE=testing` (ou `production` no go-live)

Sem `NEXT_PUBLIC_MERCADO_PAGO_PUBLIC_KEY`, o Brick **não** é ativado.

### Render (backend)
- `MERCADO_PAGO_ACCESS_TOKEN` — Access Token de **teste** da mesma aplicação (somente backend)
- `MERCADO_PAGO_WEBHOOK_SECRET` — validação `x-signature`
- `MERCADO_PAGO_ENVIRONMENT=test`
- `PUBLIC_API_BASE_URL=https://esotera-api.onrender.com`
- `MERCADO_PAGO_NOTIFICATION_URL` (opcional)

## Aplicação no painel MP
Criar/usar aplicativo com:
- Checkout Transparente
- **API de Orders**
- Credenciais de teste (Public Key + Access Token do **mesmo** app)

## Webhook

```
https://esotera-api.onrender.com/api/webhooks/mercadopago
```

Evento: **Order (Mercado Pago)** — topic `order` (não `payment`).

Após o webhook, o backend consulta `GET /v1/orders/{id}` antes de atualizar o pedido.

## Fluxo (fase 1 — somente Pix)
1. `POST /api/orders` → pedido `awaiting_payment`
2. Front abre `/pagamento/{id}` com Payment Brick (só Pix; cartão/boleto “Em breve”)
3. `POST /api/orders/{id}/payments` com `paymentMethodId=pix`
4. Backend cria order: `POST https://api.mercadopago.com/v1/orders`
5. Resposta traz `ORD…`, `PAY…`, QR / copia-e-cola; status `action_required` + `waiting_transfer` → **Aguardando pagamento**
6. Webhook `order.*` → `GET /v1/orders/{ORD…}` → atualiza pedido (`processed`/`accredited` → pago)
7. Retorno do navegador **nunca** marca como pago

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
- Mock/simulação de aprovação: apenas `NEXT_PUBLIC_DATA_MODE=mock`
