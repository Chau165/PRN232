import { apiRequest } from "@/api/apiClient"
import type { Rental } from "@/types/rental"

export function getCurrentRentals(accessToken: string, signal?: AbortSignal) {
  return apiRequest<Rental[]>("/me/rentals", { accessToken, signal, cache: "no-store" })
}

export function getRentalHistory(accessToken: string, signal?: AbortSignal) {
  return apiRequest<Rental[]>("/me/rentals/history", { accessToken, signal, cache: "no-store" })
}
