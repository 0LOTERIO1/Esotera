import type { OrderStatus } from "@/types";

export const orderStatusLabels: Record<OrderStatus, string> = {
  awaiting_payment: "Aguardando pagamento",
  payment_approved: "Pagamento aprovado",
  preparing: "Em preparação",
  shipped: "Enviado",
  delivered: "Entregue",
  cancelled: "Cancelado",
};

export const paymentMethodLabels = {
  pix: "Pix",
  card: "Cartão de crédito",
  boleto: "Boleto bancário",
} as const;
