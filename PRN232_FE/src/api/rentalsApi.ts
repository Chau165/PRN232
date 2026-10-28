import { apiRequest } from "@/api/apiClient"

export interface RentalEquipment {
  equipmentId: number
  name: string
  status: string
}

export interface RentalAmenity {
  amenityId: number
  name: string
}

export interface Rental {
  contractId: number
  contractStatus: string
  startDate: string
  endDate: string
  monthlyRent: number
  depositAmount: number
  roomId: number
  roomCode: string
  roomStatus: string
  areaSqm: number
  roomTypeId: number
  roomTypeName: string
  propertyId: number
  propertyName: string
  propertyAddress: string
  primaryImageUrl: string | null
  images: string[]
  equipments: RentalEquipment[]
  amenities: RentalAmenity[]
}

export function getCurrentRentals(accessToken: string, signal?: AbortSignal) {
  return apiRequest<Rental[]>("/me/rentals", { accessToken, signal, cache: "no-store" })
}

export function getRentalHistory(accessToken: string, signal?: AbortSignal) {
  return apiRequest<Rental[]>("/me/rentals/history", { accessToken, signal, cache: "no-store" })
}
