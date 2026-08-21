import { apiClient } from "./apiClient";

export type J3FulfillmentAdminListItem = {
  id: string;
  orderId: string;
  orderNumber: string;
  status: string;
  j3OrderId: string | null;
  j3OrderCode: string | null;
  j3TrackingNumber: string | null;
  attemptCount: number;
  lastErrorCode: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  completedAtUtc: string | null;
  canRetrySafely: boolean;
  needsManualReview: boolean;
  isPossiblyStuck: boolean;
};

export type J3FulfillmentAdminDetail = J3FulfillmentAdminListItem & {
  shippingMethodId: string;
  orderStatus: string;
  paymentStatus: string;
  j3DeliveryPointId: string | null;
  lastErrorAtUtc: string | null;
  canSendToJ3: boolean;
  eligibilityReason: string;
};

export type J3FulfillmentAdminPaged = {
  items: J3FulfillmentAdminListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
};

export const j3FulfillmentAdminApi = {
  list(params: {
    status?: string;
    orderId?: string;
    trackingNumber?: string;
    page?: number;
    pageSize?: number;
  }) {
    const q = new URLSearchParams();
    if (params.status) q.set("status", params.status);
    if (params.orderId) q.set("orderId", params.orderId);
    if (params.trackingNumber) q.set("trackingNumber", params.trackingNumber);
    if (params.page != null) q.set("page", String(params.page));
    if (params.pageSize != null) q.set("pageSize", String(params.pageSize));
    const qs = q.toString();
    return apiClient.get<J3FulfillmentAdminPaged>(
      `/api/admin/j3-fulfillments${qs ? `?${qs}` : ""}`,
      { auth: true },
    );
  },

  get(id: string) {
    return apiClient.get<J3FulfillmentAdminDetail>(
      `/api/admin/j3-fulfillments/${id}`,
      { auth: true },
    );
  },

  processOrder(orderId: string) {
    return apiClient.post<J3FulfillmentAdminProcessResult>(
      `/api/admin/orders/${orderId}/j3-fulfillment/process`,
      undefined,
      { auth: true },
    );
  },
};

export type J3FulfillmentAdminProcessResult = {
  orderId: string;
  fulfillmentId: string | null;
  orderNumber: string | null;
  status: string;
  canSendToJ3: boolean;
  eligibilityReason: string;
  j3OrderId: string | null;
  j3OrderCode: string | null;
  j3TrackingNumber: string | null;
  attemptCount: number;
  createdAtUtc: string | null;
  updatedAtUtc: string | null;
  needsManualReview: boolean;
  processed: boolean;
};

export function j3EligibilityUserMessage(reasonCode: string | undefined | null): string {
  switch (reasonCode) {
    case "FeatureDisabled":
      return "Integração J3 está desabilitada.";
    case "MissingFiscalInvoice":
    case "FiscalInvoiceNotAuthorized":
      return "NF-e autorizada necessária.";
    case "MissingNfeKey":
    case "InvalidNfeKey":
      return "Chave da NF-e inválida.";
    case "IncompleteShippingAddress":
      return "Endereço de entrega incompleto.";
    case "MissingResidentialFlag":
      return "Informe se o endereço é residencial ou comercial.";
    case "WrongShippingMethod":
      return "Pedido não utiliza frete J3.";
    case "PaymentNotApproved":
      return "Pagamento ainda não aprovado.";
    case "FulfillmentAlreadyCreated":
      return "Pedido já enviado para a J3.";
    case "FulfillmentAlreadyExists":
      return "Fulfillment J3 já está em processamento.";
    case "UnknownOutcomeRequiresReview":
      return "Resultado incerto. Não reenviar automaticamente.";
    case "RetryableFailureNotAutoRetried":
      return "Falha anterior exige revisão; retry automático não disponível nesta fase.";
    default:
      return "Não foi possível enviar para a J3.";
  }
}
