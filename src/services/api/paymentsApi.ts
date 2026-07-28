import { apiClient } from "./apiClient";

export type CreatePaymentApiRequest = {
  token?: string | null;
  paymentMethodId: string;
  installments?: number | null;
  issuerId?: string | null;
  payerEmail?: string | null;
};

export type CreatePaymentApiResponse = {
  orderId: string;
  orderNumber: string;
  amount: number;
  currency: string;
  status: string;
  mercadoPagoPaymentId?: string | null;
  ticketUrl?: string | null;
  qrCode?: string | null;
  qrCodeBase64?: string | null;
  message: string;
};

export const paymentsApi = {
  async createForOrder(
    orderId: string,
    input: CreatePaymentApiRequest,
    idempotencyKey: string,
  ): Promise<CreatePaymentApiResponse> {
    return apiClient.post<CreatePaymentApiResponse>(
      `/api/orders/${orderId}/payments`,
      {
        token: input.token ?? null,
        paymentMethodId: input.paymentMethodId,
        installments: input.installments ?? null,
        issuerId: input.issuerId ?? null,
        payerEmail: input.payerEmail ?? null,
      },
      {
        auth: true,
        headers: { "Idempotency-Key": idempotencyKey },
      },
    );
  },
};
