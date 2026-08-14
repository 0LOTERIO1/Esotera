import { apiClient, ApiError } from "./apiClient";
import type { ShippingMethodId, ShippingOption } from "@/types";

export type ShippingQuoteRequest = {
  destinationCep: string;
  state: string;
  productsSubtotal: number;
};

export type ShippingQuoteOptionDto = {
  id: ShippingMethodId;
  provider: "J3" | "Melhor Envio";
  name: string;
  price: number;
  originalPrice: number;
  estimatedDays: string;
  estimatedDaysMin: number | null;
  estimatedDaysMax: number | null;
  description: string;
  freeShippingApplied: boolean;
  subsidyApplied: boolean;
};

export type ShippingQuoteResponseDto = {
  ok: boolean;
  options: ShippingQuoteOptionDto[];
  errorCode?: string | null;
  message?: string | null;
};

export function mapQuoteOption(dto: ShippingQuoteOptionDto): ShippingOption {
  return {
    id: dto.id,
    provider: dto.provider,
    name: dto.name,
    price: dto.price,
    originalPrice: dto.originalPrice,
    estimatedDays: dto.estimatedDays,
    description: dto.description,
    // null !== 0 → false; 0 legítimo do provider → true (nunca tratar null como same-day)
    isSameDay: dto.estimatedDaysMin != null && dto.estimatedDaysMin === 0,
  };
}

export const shippingQuoteApi = {
  async quote(request: ShippingQuoteRequest): Promise<ShippingQuoteResponseDto> {
    return apiClient.post<ShippingQuoteResponseDto>(
      "/api/shipping/quote",
      {
        destinationCep: request.destinationCep,
        state: request.state,
        productsSubtotal: request.productsSubtotal,
      },
      { auth: false },
    );
  },
};

export function quoteErrorMessage(err: unknown, fallback: string): string {
  if (err instanceof ApiError) {
    if (err.status === 400 && err.detail) return err.detail;
    return err.userMessage || fallback;
  }
  return fallback;
}
