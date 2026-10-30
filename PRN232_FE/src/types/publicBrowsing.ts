export interface PublicAmenitySummary {
  amenityId: number
  name: string
}

export interface PublicPage<TItem> {
  items: TItem[]
  totalCount: number
  page: number
  pageSize: number
}

export interface PublicPropertyCard {
  propertyId: number
  propertyName: string
  addressText: string
  ward: string
  district: string
  city: string
  primaryImageUrl: string | null
  amenitiesSummary: PublicAmenitySummary[]
  availableRoomCount: number
  startingPrice: number | null
}

export interface PublicPropertyAreaCard {
  propertyId: number
  propertyName: string
  addressText: string
  ward: string
  district: string
  city: string
  latitude: number
  longitude: number
  primaryImageUrl: string | null
  availableRoomCount: number
  startingPrice: number | null
}

export interface PublicPropertyAreaQuery {
  city?: string
  district?: string
  ward?: string
}

export interface PublicPropertyDetail {
  propertyId: number
  propertyName: string
  addressText: string
  ward: string
  district: string
  city: string
  primaryImageUrl: string | null
  availableRoomCount: number
  startingPrice: number | null
  description?: string | null
  commonAmenities?: PublicAmenitySummary[]
  roomAmenities?: PublicPropertyRoomAmenity[]
  livingCosts?: PublicPropertyServiceRate[]
}

export interface PublicPropertyServiceRate {
  rateId?: number
  serviceId: number
  serviceName: string
  price: number
  unitCode: string
  billingBasis: string
  effectiveFrom: string
  effectiveTo?: string | null
}

export interface PublicPropertyRoomAmenity extends PublicAmenitySummary {
  rentableRoomCount: number
}

export interface PublicPropertyRoomCard {
  roomId: number
  roomCode: string
  roomTypeId: number
  roomTypeName: string
  baseRent: number
  areaSqm: number
  primaryImageUrl: string | null
  amenitiesSummary: PublicAmenitySummary[]
}

export interface PublicRoomDetail {
  roomId: number
  roomCode: string
  areaSqm: number
  roomType: {
    roomTypeId: number
    roomTypeName: string
  }
  baseRent: number
  property: {
    propertyId: number
    propertyName: string
    addressText: string
    ward: string
    district: string
    city: string
  }
  images: string[]
  amenities: PublicAmenitySummary[]
  services: Array<{
    serviceId: number
    name: string
    price: number
  }>
  equipments: Array<{
    equipmentId: number
    name: string
  }>
}

export type PublicPropertySort = "propertyId" | "price_asc" | "price_desc"
export type PublicRoomSort = "roomId" | "price_asc" | "price_desc"
type PriceInput = string | number
type PageInput = string | number

interface PublicListQuery {
  minPrice?: PriceInput
  maxPrice?: PriceInput
  roomTypeName?: string
  amenityIds?: readonly (string | number)[]
  sortBy?: string
  page?: PageInput
  pageSize?: PageInput
}

export interface PublicPropertyListQuery extends PublicListQuery {
  keyword?: string
  city?: string
  district?: string
  ward?: string
}

export type PublicRoomListQuery = PublicListQuery
