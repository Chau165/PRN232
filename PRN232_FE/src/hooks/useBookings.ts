import { useEffect, useState } from "react"
import { useAuth } from "@/auth/AuthContext"
import { cancelBooking, getBookings, type BookingDto } from "@/api/bookingsApi"

// Dùng chung cho trang của khách và trang quản lý: BE tự lọc danh sách theo role của token.
export function useBookings() {
  const { accessToken } = useAuth()
  const [bookings, setBookings] = useState<BookingDto[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [reload, setReload] = useState(0)
  const [cancellingId, setCancellingId] = useState<number | null>(null)

  useEffect(() => {
    if (!accessToken) return
    const controller = new AbortController()
    setLoading(true)
    setError(null)
    getBookings(accessToken, controller.signal)
      .then(setBookings)
      .catch((fetchError: unknown) => {
        if (!controller.signal.aborted) {
          setError(fetchError instanceof Error ? fetchError.message : "Không thể tải danh sách booking.")
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })
    return () => controller.abort()
  }, [accessToken, reload])

  // Ném lỗi ra cho trang tự báo bằng câu phù hợp; chỉ thay đúng booking vừa huỷ trong danh sách.
  const cancel = async (bookingId: number) => {
    if (!accessToken) throw new Error("Bạn cần đăng nhập lại.")
    setCancellingId(bookingId)
    try {
      const cancelled = await cancelBooking(accessToken, bookingId)
      setBookings((items) => items.map((item) => (item.bookingId === cancelled.bookingId ? cancelled : item)))
      return cancelled
    } finally {
      setCancellingId(null)
    }
  }

  return { bookings, loading, error, retry: () => setReload((value) => value + 1), cancel, cancellingId }
}
