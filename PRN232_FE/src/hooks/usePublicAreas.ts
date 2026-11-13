import { usePublicApiResource } from "@/hooks/usePublicApiResource"
import { publicApi } from "@/services/publicBrowsingService"

export function usePublicAreas() {
  return usePublicApiResource("areas", (signal) => publicApi.listAreas(signal))
}
