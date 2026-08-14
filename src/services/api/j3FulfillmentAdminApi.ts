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
};
