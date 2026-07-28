import { generateId } from "@/utils/format";
import type { Order, PaymentMethod } from "@/types";

export type PaymentSimulationInput = {
  method: PaymentMethod;
  installments?: number;
  total: number;
};

export type PaymentSimulationResult = {
  success: true;
  transactionId: string;
  method: PaymentMethod;
  installments?: number;
  message: string;
  /** Dados fictícios para UI — nunca dados reais de cartão */
  display?: {
    pixCode?: string;
    boletoCode?: string;
  };
};

export const mockPaymentService = {
  process(input: PaymentSimulationInput): PaymentSimulationResult {
    const transactionId = generateId("pay");

    if (input.method === "pix") {
      return {
        success: true,
        transactionId,
        method: "pix",
        message: "Instruções de pagamento Pix disponíveis no pedido.",
        display: {
          pixCode: `00020126580014BR.GOV.BCB.PIX0136${transactionId}`,
        },
      };
    }

    if (input.method === "boleto") {
      return {
        success: true,
        transactionId,
        method: "boleto",
        message: "Boleto gerado. Aguardando confirmação do pagamento.",
        display: {
          boletoCode: "23793.38128 60000.000003 00000.000400 1 8434000000" +
            Math.floor(input.total * 100)
              .toString()
              .padStart(4, "0"),
        },
      };
    }

    return {
      success: true,
      transactionId,
      method: "card",
      installments: input.installments ?? 1,
      message: "Pagamento com cartão registrado. Aguardando confirmação.",
    };
  },

  initialStatus(method: PaymentMethod): Order["status"] {
    if (method === "boleto") return "awaiting_payment";
    return "payment_approved";
  },
};
