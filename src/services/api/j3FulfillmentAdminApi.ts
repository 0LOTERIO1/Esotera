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
  j3RemoteStatus: string | null;
  j3LastStatusSyncAtUtc: string | null;
  j3LastStatusSyncErrorCode: string | null;
  j3LastStatusSyncErrorAtUtc: string | null;
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

/** Body JSON de POST .../j3-identifiers/hydrate (200). */
export type J3IdentifierHydrationResult = {
  orderId: string;
  orderNumber: string | null;
  fulfillmentId: string;
  fulfillmentStatus: string;
  j3OrderId: string | null;
  j3OrderCode: string | null;
  j3TrackingNumber: string | null;
  outcome: string;
  errorCode: string | null;
  lookupHttpSent: boolean;
  operationName: string;
};

/** Body JSON de POST .../j3-tracking/sync (200). */
export type J3TrackingSyncResult = {
  orderId: string;
  orderNumber: string | null;
  fulfillmentId: string;
  fulfillmentStatus: string;
  j3OrderId: string | null;
  j3OrderCode: string | null;
  j3TrackingNumber: string | null;
  j3RemoteStatus: string | null;
  j3LastStatusSyncAtUtc: string | null;
  j3LastStatusSyncErrorCode: string | null;
  j3LastStatusSyncErrorAtUtc: string | null;
  outcome: string;
  errorCode: string | null;
  lookupHttpSent: boolean;
  operationName: string;
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

  hydrateIdentifiers(orderId: string) {
    return apiClient.post<J3IdentifierHydrationResult>(
      `/api/admin/orders/${orderId}/j3-identifiers/hydrate`,
      undefined,
      { auth: true },
    );
  },

  syncTracking(orderId: string) {
    return apiClient.post<J3TrackingSyncResult>(
      `/api/admin/orders/${orderId}/j3-tracking/sync`,
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

/** trim + comparação case-insensitive (alinhado a J3ReconcileMatcher.CodesEqual). */
export function j3CodesEqual(a: string | null | undefined, b: string | null | undefined): boolean {
  const left = a?.trim() ?? "";
  const right = b?.trim() ?? "";
  if (!left || !right) return false;
  return left.toLowerCase() === right.toLowerCase();
}

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

export function j3TrackingActionUserMessage(reasonCode: string | undefined | null): string {
  switch (reasonCode) {
    case "J3_IDENTIFIER_HYDRATION_NOT_ELIGIBLE":
      return "Hidratação não elegível neste estado.";
    case "J3_IDENTIFIER_HYDRATION_LOCAL_CONFLICT":
      return "Identificadores J3 locais inconsistentes.";
    case "J3_IDENTIFIER_HYDRATION_LOOKUP_FAILED":
      return "Falha ao consultar detalhes do pedido na J3.";
    case "J3_IDENTIFIER_HYDRATION_NOT_FOUND":
      return "Pedido não encontrado na J3 pelo orderId.";
    case "J3_IDENTIFIER_HYDRATION_TRACKING_MISSING":
      return "Tracking ainda não disponível na J3.";
    case "J3_IDENTIFIER_HYDRATION_ID_MISMATCH":
      return "Identidade do pedido J3 divergente.";
    case "J3_IDENTIFIER_HYDRATION_ZIP_MISMATCH":
      return "CEP do pedido J3 não confere.";
    case "J3_IDENTIFIER_HYDRATION_DELIVERY_POINT_MISSING":
      return "Ponto de entrega J3 ausente na resposta.";
    case "TRACKING_SYNC_NOT_ELIGIBLE":
      return "Sincronização de status não elegível neste estado.";
    case "TRACKING_SYNC_LOCAL_CODE_MISMATCH":
      return "Código e tracking J3 locais divergem.";
    case "TRACKING_SYNC_LOOKUP_FAILED":
      return "Falha ao consultar status na J3.";
    case "TRACKING_SYNC_NOT_FOUND":
      return "Pedido não encontrado na J3 pelo código.";
    case "TRACKING_SYNC_ID_MISMATCH":
      return "Identidade do pedido J3 divergente no sync.";
    case "TRACKING_SYNC_TRACKING_MISMATCH":
      return "Tracking remoto não confere com o código local.";
    case "TRACKING_SYNC_ZIP_MISMATCH":
      return "CEP remoto não confere no sync.";
    case "TRACKING_SYNC_STATUS_MISSING":
      return "Status remoto ausente na resposta J3.";
    case "TRACKING_SYNC_DELIVERY_POINT_MISSING":
      return "Ponto de entrega ausente na resposta de sync.";
    case "TRACKING_SYNC_AMBIGUOUS":
      return "Resposta J3 ambígua para este pedido.";
    case "TRACKING_SYNC_MISSING_REMOTE_ID":
      return "ID remoto ausente na resposta J3.";
    default:
      return reasonCode
        ? `Não foi possível concluir a operação J3 (${reasonCode}).`
        : "Não foi possível concluir a operação J3.";
  }
}
