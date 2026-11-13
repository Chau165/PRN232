import { apiRequest } from "@/api/apiClient"
import type { CreatePropertyServiceRateInput, PropertyServiceRateDto, ServiceCatalogItem } from "@/types/serviceRate"

export function getServiceCatalog(signal?: AbortSignal) {
  return apiRequest<ServiceCatalogItem[]>("/services", { signal, cache: "no-store", requireJson: true })
}

// Admin xem mọi khu; Manager chỉ xem khu đang được phân công (BE trả 403 nếu không).
export function getPropertyServiceRates(accessToken: string, propertyId: number, signal?: AbortSignal) {
  return apiRequest<PropertyServiceRateDto[]>(`/properties/${propertyId}/service-rates`, {
    accessToken, signal, cache: "no-store", requireJson: true,
  })
}

// Chỉ Admin. Giá chỉ được thêm mới (không sửa/xoá) và kỳ áp dụng không được chồng lên giá cũ (409).
export function createPropertyServiceRate(accessToken: string, propertyId: number, input: CreatePropertyServiceRateInput) {
  return apiRequest<PropertyServiceRateDto>(`/properties/${propertyId}/service-rates`, {
    accessToken, method: "POST", json: input, cache: "no-store", requireJson: true,
  })
}
