# Mercado Pago — preparação (sem credenciais neste repositório)

## Variáveis

### Vercel (frontend)
- `NEXT_PUBLIC_MERCADO_PAGO_PUBLIC_KEY` — Public Key do SDK (pode ser pública)
- `NEXT_PUBLIC_DATA_MODE=api` — catálogo e pedidos via API/Neon
- `NEXT_PUBLIC_API_URL` — URL da API no Render
- `NEXT_PUBLIC_STORE_MODE=production` — bloqueia finalização sem pagamento real

### Render (backend)
- `MERCADO_PAGO_ACCESS_TOKEN` — **somente backend**, nunca `NEXT_PUBLIC_`
- `MERCADO_PAGO_WEBHOOK_SECRET` — validação do webhook
- `MERCADO_PAGO_ENVIRONMENT=test|production`

## Regras
- Access Token nunca vai ao navegador nem a logs
- Pagamento criado só no backend; valor recalculado no servidor
- Status do pedido atualizado pelo webhook, não pelo frontend
- Credencial exposta deve ser revogada; use apenas tokens novos no painel do MP

## Ainda necessário antes de vendas reais
1. Nova Public Key na Vercel
2. Novo Access Token no Render
3. Integração Checkout/Preference + webhook idempotente
4. `isRealPaymentEnabled()` liberado no frontend após integração
