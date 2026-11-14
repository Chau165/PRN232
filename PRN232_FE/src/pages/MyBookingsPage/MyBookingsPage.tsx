import { useEffect, useState } from "react"
import { Link, useLocation, useNavigate } from "react-router"
import CustomerPageShell from "@/components/customer/CustomerPageShell"
import StatusBadge from "@/components/customer/StatusBadge"
import Toast from "@/components/customer/Toast"
import BookingRefundPanel from "@/components/customer/BookingRefundPanel"
import { refundBlocksContract, refundDisplay } from "@/utils/bookingRefund"
import type { BookingDto } from "@/types/booking"
import { availableRoomsPath, routes } from "@/constants/routes"
import { useBookings } from "@/hooks/useBookings"
import { useViewingSchedules } from "@/hooks/useViewingSchedules"
import { useAuth } from "@/auth/AuthContext"
import { createBooking } from "@/services/bookingService"
import { bookingErrorMessage, bookingStatusDisplay, formatDateTime, isBookingExpired } from "@/utils/bookingDisplay"
import { formatVnd } from "@/utils/publicFormatting"
import { viewingStatusDisplay, viewingTimeLabel } from "@/utils/viewingDisplay"

export default function MyBookingsPage() {
  const { bookings, loading, error, retry, cancel: cancelById, cancellingId } = useBookings()
  const [toast, setToast] = useState("")
  const [openedRefund, setOpenedRefund] = useState<number | null>(null)

  const cancel = async (booking: BookingDto) => {
    if (cancellingId !== null) return
    if (!window.confirm(`Huỷ giữ phòng ${booking.roomCode}?`)) return
    try {
      const cancelled = await cancelById(booking.bookingId)
      setToast(`Đã huỷ giữ phòng ${cancelled.roomCode}.`)
    } catch (cancelError) {
      setToast(bookingErrorMessage(cancelError, "Booking vừa đổi trạng thái, vui lòng tải lại trang."))
    }
  }

  return (
    <CustomerPageShell
      title="Lịch xem & đặt cọc"
      description="Theo dõi lịch hẹn xem phòng, các phòng bạn đang giữ, tiền cọc và trạng thái thanh toán."
    >
      <MyViewings />
      <h2 className="mb-4 mt-10 font-display text-[22px] font-bold text-slate-900">Đặt cọc giữ phòng</h2>
      {loading && <p role="status" className="text-sm text-slate-500">Đang tải danh sách booking...</p>}
      {!loading && error && (
        <div role="alert" className="rounded-2xl bg-white p-6 text-center shadow-[0_3px_18px_rgba(16,65,67,.07)]">
          <p className="text-sm text-slate-700">{error}</p>
          <button
            type="button"
            onClick={retry}
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
                  {booking.status === "Pending" && (
                    <div>
                      <p className="text-slate-400">Hạn thanh toán cọc</p>
                      <p className="mt-1 font-semibold text-slate-700">{formatDateTime(booking.expiryDate)}</p>
                    </div>
                  )}
                </div>
                {booking.status === "Deposited" && booking.contractId === null && !refundBlocksContract(booking.refundStatus) && (
                  <p className="mt-4 text-[13px] text-slate-500">Chủ trọ sẽ liên hệ để lập hợp đồng thuê.</p>
                )}
                {booking.status === "Deposited" && booking.contractId === null && (
                  <p className="mt-3 text-[13px] text-slate-500">
                    {booking.holdUntilUtc ? `Phòng được giữ đến ${formatDateTime(booking.holdUntilUtc)}` : "Phòng được giữ"} (7 ngày kể từ khi đặt cọc).
                    Nếu chưa lập hợp đồng khi hết hạn, chủ trọ có thể mở lại phòng và tiền cọc không được hoàn.
                  </p>
                )}
                {booking.refundStatus && <div className="mt-3"><StatusBadge tone={refundDisplay(booking.refundStatus).tone}>{refundDisplay(booking.refundStatus).label}</StatusBadge></div>}
                {(booking.status === "Deposited" && booking.contractId === null || booking.refundStatus) && <button type="button"
                  onClick={() => setOpenedRefund(value => value === booking.bookingId ? null : booking.bookingId)}
                  aria-expanded={openedRefund === booking.bookingId} className="mt-4 rounded-lg border border-[#087775] px-4 py-2.5 text-sm font-semibold text-[#087775]">
                  {openedRefund === booking.bookingId ? "Đóng hoàn cọc" : booking.canRequestRefund ? "Xin hoàn cọc" : "Theo dõi hoàn cọc"}
                </button>}
                {openedRefund === booking.bookingId && <BookingRefundPanel booking={booking} onChanged={retry} />}
                {booking.contractId !== null && (
                  <Link to={routes.myContracts} className="mt-4 inline-block text-[13px] font-semibold text-[#087775] hover:underline">
                    Đã lập hợp đồng — xem trong Hợp đồng của tôi
                  </Link>
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
                {booking.status === "Pending" && booking.latestPaymentRequestId && <Link to={`${routes.paymentResult}?requestId=${booking.latestPaymentRequestId}`}
                  className="mt-3 inline-block text-sm font-semibold text-[#087775]">Kiểm tra giao dịch cọc #{booking.latestPaymentRequestId}</Link>}
              </article>
            )
          })}
        </div>
      )}
      {toast && <Toast message={toast} onClose={() => setToast("")} />}
    </CustomerPageShell>
  )
}

function MyViewings() {
  const { accessToken } = useAuth()
  const navigate = useNavigate()
  const { viewings, loading, error, retry } = useViewingSchedules()
  const [bookingViewingId, setBookingViewingId] = useState<number | null>(null)
  const [bookingError, setBookingError] = useState("")
  const location = useLocation()
  const requestedViewingId = new URLSearchParams(location.search).get("viewingId")
  const selectedViewingId = requestedViewingId && /^[1-9]\d*$/.test(requestedViewingId) && Number.isSafeInteger(Number(requestedViewingId))
    ? Number(requestedViewingId)
    : null

  async function bookSelectedRoom(viewing: (typeof viewings)[number]) {
    if (!accessToken || viewing.roomId === null || bookingViewingId !== null) return
    setBookingViewingId(viewing.viewingId)
    setBookingError("")
    try {
      const booking = await createBooking(accessToken, {
        roomId: viewing.roomId,
        viewingId: viewing.viewingId,
      })
      navigate(`${routes.deposit}?bookingId=${booking.bookingId}`)
    } catch (cause) {
      setBookingError(bookingErrorMessage(cause, "Phòng vừa thay đổi trạng thái; hãy tải lại lịch trước khi đặt cọc."))
    } finally {
      setBookingViewingId(null)
    }
  }

  useEffect(() => {
    if (selectedViewingId === null || loading) return
    document.getElementById(`viewing-${selectedViewingId}`)?.scrollIntoView?.({ behavior: "smooth", block: "center" })
  }, [loading, selectedViewingId, viewings])

  return (
    <section>
      <h2 className="mb-4 font-display text-[22px] font-bold text-slate-900">Lịch xem phòng</h2>
      {loading && <p role="status" className="text-sm text-slate-500">Đang tải lịch xem phòng...</p>}
      {!loading && error && (
        <div role="alert" className="rounded-2xl bg-white p-6 text-center shadow-[0_3px_18px_rgba(16,65,67,.07)]">
          <p className="text-sm text-slate-700">{error}</p>
          <button type="button" onClick={retry} className="mt-4 rounded-lg bg-[#087775] px-4 py-2 text-sm font-semibold text-white">
            Tải lại lịch xem
          </button>
        </div>
      )}
      {!loading && !error && viewings.length === 0 && (
        <p className="rounded-2xl bg-white p-6 text-center text-sm text-slate-500 shadow-[0_3px_18px_rgba(16,65,67,.07)]">
          Bạn chưa đặt lịch xem phòng nào.
        </p>
      )}
      {!loading && !error && viewings.length > 0 && (
        <div className="space-y-3">
          {viewings.map((viewing) => {
            const display = viewingStatusDisplay(viewing)
            return (
              <article
                id={`viewing-${viewing.viewingId}`}
                key={viewing.viewingId}
                className={`flex flex-wrap items-center justify-between gap-3 rounded-2xl p-5 shadow-[0_3px_18px_rgba(16,65,67,.07)] ${viewing.viewingId === selectedViewingId ? "bg-teal-50 ring-2 ring-teal-300" : "bg-white"}`}
              >
                <div>
                  <p className="font-semibold text-slate-800">{viewing.propertyName} · {viewing.roomTypeName}</p>
                  {viewing.currentMonthlyRent !== null && <p className="mt-1 text-[13px] text-slate-500">Giá tham khảo hiện tại: {formatVnd(viewing.currentMonthlyRent)}/tháng</p>}
                  <p className="mt-1 text-[13px] text-slate-500">Thời gian: {viewingTimeLabel(viewing)}</p>
                  {viewing.status === "Approved" && viewing.assignedManagerName && (
                    <p className="mt-1 text-[13px] text-slate-600">
                      Manager phụ trách: {viewing.assignedManagerName}
                      {viewing.assignedManagerPhone && <> · <a className="text-[#087775]" href={`tel:${viewing.assignedManagerPhone}`}>{viewing.assignedManagerPhone}</a></>}
                    </p>
                  )}
                  {viewing.managerNote && <p className="mt-1 whitespace-pre-wrap text-[13px] text-slate-600">{viewing.status === "Rejected" ? "Lý do từ chối" : "Ghi chú"}: {viewing.managerNote}</p>}
                  {viewing.roomId === null ? (
                    <p className="mt-2 text-[13px] text-slate-500">Manager chưa chọn phòng cụ thể.</p>
                  ) : (
                    <>
                      <Link to={`${availableRoomsPath(viewing.propertyId)}?roomId=${viewing.roomId}`} className="mt-2 inline-block text-[13px] font-semibold text-[#087775] underline underline-offset-2">Phòng đã xác nhận: {viewing.roomCode}</Link>
                      {viewing.status === "Approved" && <button type="button" disabled={bookingViewingId !== null} onClick={() => void bookSelectedRoom(viewing)} className="ml-4 rounded-lg bg-[#087775] px-3 py-2 text-xs font-bold text-white disabled:opacity-50">{bookingViewingId === viewing.viewingId ? "Đang đặt..." : "Đặt cọc phòng này"}</button>}
                    </>
                  )}
                </div>
                <StatusBadge tone={display.tone}>{display.label}</StatusBadge>
              </article>
            )
          })}
        </div>
      )}
      {bookingError && <p role="alert" className="mt-3 rounded-lg bg-rose-50 px-4 py-3 text-sm text-rose-700">{bookingError}</p>}
    </section>
  )
}
