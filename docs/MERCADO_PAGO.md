# Mercado Pago — Checkout Transparente (Payment Brick)

## Variáveis

### Vercel (frontend) — configurar só após validar webhook
- `NEXT_PUBLIC_MERCADO_PAGO_PUBLIC_KEY` — Public Key (pode ser pública)
- `NEXT_PUBLIC_DATA_MODE=api`
- `NEXT_PUBLIC_API_URL=https://esotera-api.onrender.com`
- `NEXT_PUBLIC_STORE_MODE=testing` (ou `production` no go-live)

Sem `NEXT_PUBLIC_MERCADO_PAGO_PUBLIC_KEY`, o Brick **não** é ativado.

### Render (backend)
- `MERCADO_PAGO_ACCESS_TOKEN` — **somente backend**, nunca `NEXT_PUBLIC_`
- `MERCADO_PAGO_WEBHOOK_SECRET` — validação `x-signature`
- `MERCADO_PAGO_ENVIRONMENT=test`
- `PUBLIC_API_BASE_URL=https://esotera-api.onrender.com`
- `MERCADO_PAGO_NOTIFICATION_URL` (opcional) — override do webhook

## Webhook a cadastrar no painel MP

```
https://esotera-api.onrender.com/api/webhooks/mercadopago
```

Eventos: **Pagamentos** (`payment`). Preferir `?source_news=webhooks` se o painel oferecer.

## Fluxo
1. `POST /api/orders` → pedido `awaiting_payment` (totais só no servidor)
2. Front abre `/pagamento/{id}` com Payment Brick
3. `POST /api/orders/{id}/payments` com token Pix/cartão (sem PAN/CVV)
4. Webhook consulta `GET /v1/payments/{id}` e atualiza o pedido
5. Retorno do navegador **nunca** marca como pago

## Regras
- Access Token nunca no navegador, Git, logs ou README com valor
- Status só via webhook/consulta autenticada
- Idempotência: `Idempotency-Key` no pedido e no pagamento
- Mock/simulação de aprovação: apenas `NEXT_PUBLIC_DATA_MODE=mock`
