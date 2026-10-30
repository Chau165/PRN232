import { useEffect, useState } from "react"
import { useAuth } from "@/auth/AuthContext"
import { getViewingSchedules } from "@/services/viewingScheduleService"
import type { ViewingScheduleDto } from "@/types/viewingSchedule"

// Giống useBookings: BE tự lọc danh sách theo role của token (khách / chủ trọ / admin).
export function useViewingSchedules() {
  const { accessToken } = useAuth()
  const [viewings, setViewings] = useState<ViewingScheduleDto[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [reload, setReload] = useState(0)

  useEffect(() => {
    if (!accessToken) return
    const controller = new AbortController()
    setLoading(true)
    setError(null)
    getViewingSchedules(accessToken, controller.signal)
      .then(setViewings)
      .catch((fetchError: unknown) => {
        if (!controller.signal.aborted) {
          setError(fetchError instanceof Error ? fetchError.message : "Không thể tải lịch xem phòng.")
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })
    return () => controller.abort()
  }, [accessToken, reload])

  const replace = (updated: ViewingScheduleDto) =>
    setViewings((items) => items.map((item) => (item.viewingId === updated.viewingId ? updated : item)))

  return { viewings, loading, error, retry: () => setReload((value) => value + 1), replace }
}
