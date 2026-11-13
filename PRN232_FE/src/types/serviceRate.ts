// GET /api/services: danh mục dịch vụ dùng chung (điện, nước, wifi...).
export interface ServiceCatalogItem {
  serviceId: number
  name: string
  price: number
}

// GET/POST /api/properties/{id}/service-rates. Ngày là đầu tháng (yyyy-MM-01);
// effectiveTo là mốc LOẠI TRỪ: giá áp dụng cho các tháng < effectiveTo. null = không thời hạn.
export interface PropertyServiceRateDto {
  rateId: number
  propertyId: number
  serviceId: number
  serviceName: string
  price: number
  unitCode: string
  billingBasis: string
  effectiveFrom: string
  effectiveTo: string | null
}

export interface CreatePropertyServiceRateInput {
  serviceId: number
  price: number
  unitCode: string
  billingBasis: string
  effectiveFrom: string
  effectiveTo: string | null
}
