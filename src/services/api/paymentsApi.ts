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
  mercadoPagoOrderId?: string | null;
  mercadoPagoPaymentId?: string | null;
  ticketUrl?: string | null;
  qrCode?: string | null;
  qrCodeBase64?: string | null;
  dateOfExpiration?: string | null;
  message: string;
};

export type PaymentEnvironmentConfig = {
  environment: string;
  sandboxPixEnabled: boolean;
  sandboxPixAmount: number;
  commercialCheckoutAllowedInTest: boolean;
};

export type SandboxPixTestResponse = {
  mercadoPagoOrderId: string;
  mercadoPagoPaymentId?: string | null;
  amount: number;
  currency: string;
  status: string;
  statusDetail: string;
  externalReference: string;
  ticketUrl?: string | null;
  qrCode?: string | null;
  qrCodeBase64?: string | null;
  dateOfExpiration?: string | null;
  message: string;
  isSandboxTest: boolean;
};

export const paymentsApi = {
  async getConfig(): Promise<PaymentEnvironmentConfig> {
    return apiClient.get<PaymentEnvironmentConfig>("/api/payments/config", {
      auth: false,
    });
  },

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

  async createSandboxPixTest(
    idempotencyKey: string,
  ): Promise<SandboxPixTestResponse> {
    return apiClient.post<SandboxPixTestResponse>(
      "/api/payments/sandbox/pix-test",
      {},
      {
        auth: true,
        headers: { "Idempotency-Key": idempotencyKey },
      },
    );
  },
};
