import { apiClient } from "./apiClient";

export type CreatePaymentApiRequest = {
  token?: string | null;
  paymentMethodId: string;
  installments?: number | null;
  issuerId?: string | null;
  payerEmail?: string | null;
  /** bank_transfer | credit_card | debit_card | ticket */
  paymentMethodType?: string | null;
  payerIdentificationType?: string | null;
  payerIdentificationNumber?: string | null;
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
  digitableLine?: string | null;
  barcodeContent?: string | null;
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

/** Normaliza possíveis variantes de casing da API. */
function normalizePaymentConfig(raw: Record<string, unknown>): PaymentEnvironmentConfig {
  const environment = String(
    raw.environment ?? raw.Environment ?? "Production",
  );
  const sandboxPixEnabled = Boolean(
    raw.sandboxPixEnabled ?? raw.SandboxPixEnabled ?? false,
  );
  const amountRaw = raw.sandboxPixAmount ?? raw.SandboxPixAmount ?? 50;
  const sandboxPixAmount =
    typeof amountRaw === "number" ? amountRaw : Number(amountRaw) || 50;
  const commercialCheckoutAllowedInTest = Boolean(
    raw.commercialCheckoutAllowedInTest ??
      raw.CommercialCheckoutAllowedInTest ??
      false,
  );
  return {
    environment,
    sandboxPixEnabled,
    sandboxPixAmount,
    commercialCheckoutAllowedInTest,
  };
}

function normalizeCreatePaymentResponse(
  raw: Record<string, unknown>,
): CreatePaymentApiResponse {
  return {
    orderId: String(raw.orderId ?? raw.OrderId ?? ""),
    orderNumber: String(raw.orderNumber ?? raw.OrderNumber ?? ""),
    amount: Number(raw.amount ?? raw.Amount ?? 0),
    currency: String(raw.currency ?? raw.Currency ?? "BRL"),
    status: String(raw.status ?? raw.Status ?? ""),
    mercadoPagoOrderId: (raw.mercadoPagoOrderId ??
      raw.MercadoPagoOrderId ??
      null) as string | null,
    mercadoPagoPaymentId: (raw.mercadoPagoPaymentId ??
      raw.MercadoPagoPaymentId ??
      null) as string | null,
    ticketUrl: (raw.ticketUrl ?? raw.TicketUrl ?? null) as string | null,
    qrCode: (raw.qrCode ?? raw.QrCode ?? null) as string | null,
    qrCodeBase64: (raw.qrCodeBase64 ?? raw.QrCodeBase64 ?? null) as
      | string
      | null,
    dateOfExpiration: (raw.dateOfExpiration ??
      raw.DateOfExpiration ??
      null) as string | null,
    message: String(raw.message ?? raw.Message ?? ""),
    digitableLine: (raw.digitableLine ?? raw.DigitableLine ?? null) as
      | string
      | null,
    barcodeContent: (raw.barcodeContent ?? raw.BarcodeContent ?? null) as
      | string
      | null,
  };
}

function normalizeSandboxResponse(
  raw: Record<string, unknown>,
): SandboxPixTestResponse {
  return {
    mercadoPagoOrderId: String(
      raw.mercadoPagoOrderId ?? raw.MercadoPagoOrderId ?? "",
    ),
    mercadoPagoPaymentId: (raw.mercadoPagoPaymentId ??
      raw.MercadoPagoPaymentId ??
      null) as string | null,
    amount: Number(raw.amount ?? raw.Amount ?? 50),
    currency: String(raw.currency ?? raw.Currency ?? "BRL"),
    status: String(raw.status ?? raw.Status ?? ""),
    statusDetail: String(raw.statusDetail ?? raw.StatusDetail ?? ""),
    externalReference: String(
      raw.externalReference ?? raw.ExternalReference ?? "",
    ),
    ticketUrl: (raw.ticketUrl ?? raw.TicketUrl ?? null) as string | null,
    qrCode: (raw.qrCode ?? raw.QrCode ?? null) as string | null,
    qrCodeBase64: (raw.qrCodeBase64 ?? raw.QrCodeBase64 ?? null) as
      | string
      | null,
    dateOfExpiration: (raw.dateOfExpiration ??
      raw.DateOfExpiration ??
      null) as string | null,
    message: String(raw.message ?? raw.Message ?? ""),
    isSandboxTest: Boolean(raw.isSandboxTest ?? raw.IsSandboxTest ?? true),
  };
}

export const paymentsApi = {
  async getConfig(): Promise<PaymentEnvironmentConfig> {
    const raw = await apiClient.get<Record<string, unknown>>(
      "/api/payments/config",
      { auth: false },
    );
    return normalizePaymentConfig(raw ?? {});
  },

  async createForOrder(
    orderId: string,
    input: CreatePaymentApiRequest,
    idempotencyKey: string,
  ): Promise<CreatePaymentApiResponse> {
    const raw = await apiClient.post<Record<string, unknown>>(
      `/api/orders/${orderId}/payments`,
      {
        token: input.token ?? null,
        paymentMethodId: input.paymentMethodId,
        installments: input.installments ?? null,
        issuerId: input.issuerId ?? null,
        payerEmail: input.payerEmail ?? null,
        paymentMethodType: input.paymentMethodType ?? null,
        payerIdentificationType: input.payerIdentificationType ?? null,
        payerIdentificationNumber: input.payerIdentificationNumber ?? null,
      },
      {
        auth: true,
        headers: { "Idempotency-Key": idempotencyKey },
      },
    );
    return normalizeCreatePaymentResponse(raw ?? {});
  },

  async createSandboxPixTest(
    idempotencyKey: string,
  ): Promise<SandboxPixTestResponse> {
    const raw = await apiClient.post<Record<string, unknown>>(
      "/api/payments/sandbox/pix-test",
      {},
      {
        auth: true,
        headers: { "Idempotency-Key": idempotencyKey },
      },
    );
    return normalizeSandboxResponse(raw ?? {});
  },
};
