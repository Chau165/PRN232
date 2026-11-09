import { usePublicApiResource } from "@/hooks/usePublicApiResource"
import { publicApi } from "@/services/publicBrowsingService"

export function usePublicAmenities() {
  return usePublicApiResource("amenities", (signal) => publicApi.listAmenities(signal))
}
