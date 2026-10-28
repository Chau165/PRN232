import { useEffect, useState } from "react"
import { Link } from "react-router"
import CustomerPageShell from "@/components/customer/CustomerPageShell"
import StatusBadge from "@/components/customer/StatusBadge"
import Toast from "@/components/customer/Toast"
import { useAuth } from "@/auth/AuthContext"
import { cancelBooking, getBookings, type BookingDto } from "@/api/bookingsApi"
import { routes } from "@/constants/routes"
import { bookingErrorMessage, bookingStatusDisplay, formatDateTime, isBookingExpired } from "@/utils/bookingDisplay"
import { formatVnd } from "@/utils/publicFormatting"

export default function MyBookingsPage() {
  const { accessToken } = useAuth()
  const [bookings, setBookings] = useState<BookingDto[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [reload, setReload] = useState(0)
  const [cancellingId, setCancellingId] = useState<number | null>(null)
  const [toast, setToast] = useState("")

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

  const cancel = async (booking: BookingDto) => {
    if (!accessToken || cancellingId !== null) return
    if (!window.confirm(`Huỷ giữ phòng ${booking.roomCode}?`)) return
    setCancellingId(booking.bookingId)
    try {
      const cancelled = await cancelBooking(accessToken, booking.bookingId)
      setBookings((items) => items.map((item) => (item.bookingId === cancelled.bookingId ? cancelled : item)))
      setToast(`Đã huỷ giữ phòng ${cancelled.roomCode}.`)
    } catch (cancelError) {
      setToast(bookingErrorMessage(cancelError, "Booking vừa đổi trạng thái, vui lòng tải lại trang."))
    } finally {
      setCancellingId(null)
    }
  }

  return (
    <CustomerPageShell
      title="Lịch sử đặt cọc"
      description="Theo dõi các phòng bạn đang giữ, tiền cọc và trạng thái thanh toán."
    >
      {loading && <p role="status" className="text-sm text-slate-500">Đang tải danh sách booking...</p>}
      {!loading && error && (
        <div role="alert" className="rounded-2xl bg-white p-6 text-center shadow-[0_3px_18px_rgba(16,65,67,.07)]">
          <p className="text-sm text-slate-700">{error}</p>
          <button
            type="button"
            onClick={() => setReload((value) => value + 1)}
            className="mt-4 rounded-lg bg-[#087775] px-4 py-2 text-sm font-semibold text-white"
          >
            Thử lại
          </button>
        </div>
      )}
      {!loading && !error && bookings.length === 0 && (
        <p className="rounded-2xl bg-white p-6 text-center text-sm text-slate-500 shadow-[0_3px_18px_rgba(16,65,67,.07)]">
          Bạn chưa đặt cọc phòng nào.
        </p>
      )}
      {!loading && !error && bookings.length > 0 && (
        <div className="space-y-4">
          {bookings.map((booking) => {
            const display = bookingStatusDisplay(booking)
            const awaitingPayment = booking.status === "Pending" && !isBookingExpired(booking)
            return (
              <article
                key={booking.bookingId}
                className="rounded-2xl bg-white p-6 shadow-[0_3px_18px_rgba(16,65,67,.07)]"
              >
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <p className="text-[12px] font-bold uppercase tracking-[.12em] text-[#087775]">
                      Booking #{booking.bookingId}
                    </p>
                    <h2 className="mt-2 font-display text-[20px] font-bold text-slate-900">
                      {booking.propertyName}
                    </h2>
                    <p className="mt-1 text-[13px] text-slate-500">Phòng {booking.roomCode}</p>
                  </div>
                  <StatusBadge tone={display.tone}>{display.label}</StatusBadge>
                </div>
                <div className="mt-5 grid gap-4 border-t border-slate-100 pt-5 text-[14px] sm:grid-cols-2">
                  <div>
                    <p className="text-slate-400">Tiền cọc</p>
                    <p className="mt-1 font-bold text-[#087775]">{formatVnd(booking.depositAmount)}</p>
                  </div>
                  {booking.status !== "Cancelled" && (
                    <div>
                      <p className="text-slate-400">Giữ phòng đến</p>
                      <p className="mt-1 font-semibold text-slate-700">{formatDateTime(booking.expiryDate)}</p>
                    </div>
                  )}
                </div>
                {booking.status === "Deposited" && (
                  <p className="mt-4 text-[13px] text-slate-500">Chủ trọ sẽ liên hệ để lập hợp đồng thuê.</p>
                )}
                {awaitingPayment && (
                  <div className="mt-5 flex flex-wrap gap-3">
                    <Link
                      to={`${routes.deposit}?bookingId=${booking.bookingId}`}
                      className="rounded-lg bg-[#087775] px-4 py-2.5 text-[13px] font-bold text-white hover:bg-[#066a68]"
                    >
                      Thanh toán cọc
                    </Link>
                    <button
                      type="button"
                      onClick={() => cancel(booking)}
                      disabled={cancellingId !== null}
                      className="rounded-lg bg-rose-50 px-4 py-2.5 text-[13px] font-bold text-rose-700 disabled:opacity-60"
                    >
                      {cancellingId === booking.bookingId ? "Đang huỷ..." : "Huỷ giữ phòng"}
                    </button>
                  </div>
                )}
              </article>
            )
          })}
        </div>
      )}
      {toast && <Toast message={toast} onClose={() => setToast("")} />}
    </CustomerPageShell>
  )
}
