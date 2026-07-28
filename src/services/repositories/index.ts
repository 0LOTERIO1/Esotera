import { getDataMode } from "@/config/dataMode";
import { MockAuthRepository } from "./MockAuthRepository";
import { ApiAuthRepository } from "./ApiAuthRepository";
import { MockProductRepository } from "./MockProductRepository";
import { ApiProductRepository } from "./ApiProductRepository";
import { MockOrderRepository } from "./MockOrderRepository";
import { ApiOrderRepository } from "./ApiOrderRepository";
import { MockCouponRepository } from "./MockCouponRepository";
import { ApiCouponRepository } from "./ApiCouponRepository";
import { MockAddressRepository } from "./MockAddressRepository";
import { ApiAddressRepository } from "./ApiAddressRepository";
import { MockAdminRepository } from "./MockAdminRepository";
import { ApiAdminRepository } from "./ApiAdminRepository";
import { MockSettingsRepository } from "./MockSettingsRepository";
import { ApiSettingsRepository } from "./ApiSettingsRepository";
import type { IAuthRepository } from "./IAuthRepository";
import type { IProductRepository } from "./IProductRepository";
import type { IOrderRepository } from "./IOrderRepository";
import type { ICouponRepository } from "./ICouponRepository";
import type { IAddressRepository } from "./IAddressRepository";
import type { IAdminRepository } from "./IAdminRepository";
import type { ISettingsRepository } from "./ISettingsRepository";

/**
 * Integração Fase 2F:
 * - Auth, catálogo, endereços, pedidos, admin, cupons e settings: API quando DATA_MODE=api
 * - Em mock: repositories simulados; sem chamadas HTTP
 */

let authRepository: IAuthRepository | null = null;
let productRepository: IProductRepository | null = null;
let orderRepository: IOrderRepository | null = null;
let couponRepository: ICouponRepository | null = null;
let addressRepository: IAddressRepository | null = null;
let adminRepository: IAdminRepository | null = null;
let settingsRepository: ISettingsRepository | null = null;

export function getAuthRepository(): IAuthRepository {
  if (!authRepository) {
    authRepository =
      getDataMode() === "api"
        ? new ApiAuthRepository()
        : new MockAuthRepository();
  }
  return authRepository;
}

export function getProductRepository(): IProductRepository {
  if (!productRepository) {
    productRepository =
      getDataMode() === "api"
        ? new ApiProductRepository()
        : new MockProductRepository();
  }
  return productRepository;
}

export function getOrderRepository(): IOrderRepository {
  if (!orderRepository) {
    orderRepository =
      getDataMode() === "api"
        ? new ApiOrderRepository()
        : new MockOrderRepository();
  }
  return orderRepository;
}

export function getCouponRepository(): ICouponRepository {
  if (!couponRepository) {
    couponRepository =
      getDataMode() === "api"
        ? new ApiCouponRepository()
        : new MockCouponRepository();
  }
  return couponRepository;
}

export function getAddressRepository(): IAddressRepository {
  if (!addressRepository) {
    addressRepository =
      getDataMode() === "api"
        ? new ApiAddressRepository()
        : new MockAddressRepository();
  }
  return addressRepository;
}

export function getAdminRepository(): IAdminRepository {
  if (!adminRepository) {
    adminRepository =
      getDataMode() === "api"
        ? new ApiAdminRepository()
        : new MockAdminRepository();
  }
  return adminRepository;
}

export function getSettingsRepository(): ISettingsRepository {
  if (!settingsRepository) {
    settingsRepository =
      getDataMode() === "api"
        ? new ApiSettingsRepository()
        : new MockSettingsRepository();
  }
  return settingsRepository;
}

export function resetRepositories() {
  authRepository = null;
  productRepository = null;
  orderRepository = null;
  couponRepository = null;
  addressRepository = null;
  adminRepository = null;
  settingsRepository = null;
}
