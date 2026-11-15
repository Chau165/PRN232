import { useState } from "react"
import { Link } from "react-router"
import { useAuth } from "@/auth/AuthContext"
import ManagementDataTable from "@/components/management/ManagementDataTable"
import ManagementPage from "@/components/management/ManagementPage"
import StatusBadge from "@/components/customer/StatusBadge"
import Toast from "@/components/customer/Toast"
import BookingRefundPanel from "@/components/customer/BookingRefundPanel"
import { refundBlocksContract, refundDisplay } from "@/utils/bookingRefund"
import type { BookingDto } from "@/types/booking"
import { routes } from "@/constants/routes"
import { useBookings } from "@/hooks/useBookings"
import {
  bookingErrorMessage,
  bookingStatusDisplay,
  formatBookingCancellationReason,
  formatBookingMoveInSummary,
  formatDateTime,
  formatHoldUntilDate,
} from "@/utils/bookingDisplay"
import { formatVnd } from "@/utils/publicFormatting"

const ONLINE_REFUND_REQUESTS_ENABLED = false

export default function ManagementBookingsPage() {
  const { user } = useAuth()
  const {
    bookings,
    loading,
    error,
    retry,
    cancel: cancelById,
    cancellingId,
    markDepositReturned,
    markingReturnedId,
  } = useBookings()
  const [toast, setToast] = useState("")
  const [openedRefund, setOpenedRefund] = useState<number | null>(null)
  const [confirmingReturnBooking, setConfirmingReturnBooking] = useState<BookingDto | null>(null)
  const [confirmReturnError, setConfirmReturnError] = useState<string | null>(null)

  const cancel = async (booking: BookingDto) => {
    if (cancellingId !== null) return
    const question = `Huỷ booking #${booking.bookingId} của khách ${booking.customerName}?`
    if (!window.confirm(question)) return
    try {
      await cancelById(booking.bookingId)
      setToast(`Đã huỷ booking #${booking.bookingId} và thông báo cho khách.`)
    } catch (cancelError) {
      setToast(bookingErrorMessage(cancelError, "Booking vừa đổi trạng thái, vui lòng tải lại trang."))
    }
  }

  // C10: quản lý hủy khi còn trong hạn giữ phòng (canReleaseRoom = false) -> khách được hoàn 100% cọc ngoài hệ thống.
  const cancelWithFullRefund = async (booking: BookingDto) => {
    if (cancellingId !== null) return
    const question = `Khách sẽ được hoàn ${formatVnd(booking.depositAmount)}. Bạn cần tự chuyển tiền cho khách.`
    if (!window.confirm(question)) return
    try {
      await cancelById(booking.bookingId)
      setToast(`Đã huỷ booking #${booking.bookingId} và thông báo cho khách.`)
    } catch (cancelError) {
      setToast(bookingErrorMessage(cancelError, "Booking vừa đổi trạng thái, vui lòng tải lại trang."))
    }
  }

  // C10: hết hạn giữ phòng (canReleaseRoom = true), khách không nhận phòng -> nhả phòng, khách mất cọc.
  const release = async (booking: BookingDto) => {
    if (cancellingId !== null) return
    const holdLabel = formatHoldUntilDate(booking.holdUntilUtc)
    const question = `Giải phóng phòng ${booking.roomCode} (booking #${booking.bookingId}) vì khách ${booking.customerName} quá hạn nhận phòng${holdLabel ? ` (${holdLabel})` : ""}? Phòng sẽ mở cho người khác thuê và tiền cọc không được hoàn.`
    if (!window.confirm(question)) return
    try {
      await cancelById(booking.bookingId)
      setToast(`Đã giải phóng phòng ${booking.roomCode} (booking #${booking.bookingId}) và thông báo cho khách.`)
    } catch (releaseError) {
      setToast(bookingErrorMessage(releaseError, "Booking vừa đổi trạng thái hoặc khách vừa xin hoàn cọc, vui lòng tải lại trang."))
    }
  }

  const submitDepositReturned = async () => {
    if (!confirmingReturnBooking || markingReturnedId !== null) return
    setConfirmReturnError(null)
    try {
      const updated = await markDepositReturned(confirmingReturnBooking.bookingId)
      setConfirmingReturnBooking(null)
      setToast(`Đã xác nhận hoàn cọc cho booking #${updated.bookingId}.`)
    } catch (returnError) {
      setConfirmReturnError(
        bookingErrorMessage(
          returnError,
          "Booking không có khoản phải hoàn hoặc đã được xác nhận hoàn cọc trước đó.",
        ),
      )
    }
  }

  return (
    <ManagementPage
      title="Đặt phòng & Đặt cọc"
      description="Theo dõi giữ phòng, đặt cọc và tiến độ hoàn cọc. Khách gửi yêu cầu hoàn, Admin xét duyệt."
    >
      <div className="mb-4 flex flex-wrap gap-3"><button type="button" onClick={retry} className="rounded-lg border border-[#087775] px-4 py-2 text-sm font-semibold text-[#087775]">Cập nhật booking</button>
        {user?.role === "ADMIN" && <Link to={routes.managementRefunds} className="rounded-lg bg-[#087775] px-4 py-2 text-sm font-bold text-white">Xét duyệt hoàn cọc</Link>}</div>
      {loading && <p role="status" className="text-[14px] text-slate-500">Đang tải danh sách booking...</p>}
      {!loading && error && (
        <div role="alert" className="rounded-2xl bg-white p-6 text-center shadow-[0_3px_18px_rgba(16,65,67,.05)]">
          <p className="text-[14px] text-slate-700">{error}</p>
          <button
            type="button"
            onClick={retry}
            className="mt-4 rounded-lg bg-[#087775] px-4 py-2 text-[13px] font-bold text-white"
          >
            Thử lại
          </button>
        </div>
      )}
      {!loading && !error && (
        <ManagementDataTable
          headers={["Booking", "Khách hàng", "Khu trọ / Phòng", "Tiền cọc", "Giữ phòng đến", "Trạng thái", "Thao tác"]}
        >
          {bookings.length === 0 ? (
            <p className="px-5 py-8 text-center text-[13px] text-slate-500">Chưa có booking nào.</p>
          ) : (
            <div className="divide-y divide-slate-100">
              {bookings.map((booking) => {
                const display = bookingStatusDisplay(booking)
                return (
                  <div key={booking.bookingId}><div className="grid gap-3 px-5 py-5 lg:grid-cols-7 lg:items-center">
                    <strong className="text-[13px] text-slate-700">#{booking.bookingId}</strong>
                    <span className="text-[13px] text-slate-600">{booking.customerName}</span>
                    <span className="text-[13px] text-slate-600">
                      {booking.propertyName}
                      <small className="block text-slate-400">Phòng {booking.roomCode}</small>
                      {formatBookingMoveInSummary(booking) && (
                        <small className="block text-slate-500">{formatBookingMoveInSummary(booking)}</small>
                      )}
                    </span>
                    <span className="font-semibold text-[#087775]">{formatVnd(booking.depositAmount)}</span>
                    <span className="text-[13px] text-slate-600">
                      {booking.status === "Pending" ? formatDateTime(booking.expiryDate)
                        : booking.status === "Deposited" && booking.contractId === null
                          ? booking.holdUntilUtc ? formatHoldUntilDate(booking.holdUntilUtc) : "Không rõ (dữ liệu cũ)"
                          : "—"}
                      {booking.canReleaseRoom && <small className="block font-semibold text-rose-700">Quá hạn nhận phòng</small>}
                    </span>
                    <div className="space-y-2">
                      <StatusBadge tone={display.tone}>{display.label}</StatusBadge>
                      {booking.refundStatus && <StatusBadge tone={refundDisplay(booking.refundStatus).tone}>{refundDisplay(booking.refundStatus).label}</StatusBadge>}
                      {booking.status === "Cancelled" && formatBookingCancellationReason(booking.cancellationReason) && (
                        <small className="block text-[12px] text-slate-600">
                          Lý do hủy: {formatBookingCancellationReason(booking.cancellationReason)}
                        </small>
                      )}
                      {booking.depositReturnedAtUtc ? (
                        <small className="block text-[12px] font-semibold text-emerald-700">
                          Đã hoàn cọc lúc {formatDateTime(booking.depositReturnedAtUtc)}
                          {booking.depositReturnedByName ? ` — ${booking.depositReturnedByName}` : ""}
                        </small>
                      ) : booking.depositRefundDue ? (
                        <small className="block text-[12px] font-semibold text-amber-700">
                          Chờ hoàn cọc {formatVnd(booking.depositAmount)}
                        </small>
                      ) : null}
                    </div>
                    {/* Booking đã lên hợp đồng thì không còn thao tác: BE cũng chặn huỷ trường hợp này. */}
                    <div className="flex flex-wrap gap-2">
                      {user?.role === "MANAGER" && booking.status === "Deposited" && booking.contractId === null && !refundBlocksContract(booking.refundStatus) && (
                        <Link
                          to={`${routes.managementCreateContract}?bookingId=${booking.bookingId}`}
                          className="rounded-lg bg-[#087775] px-3 py-2 text-[12px] font-bold text-white hover:bg-[#066a68]"
                        >
                          Tạo hợp đồng
                        </Link>
                      )}
                      {booking.status === "Pending" && booking.contractId === null && (
                        <button
                          type="button"
                          onClick={() => cancel(booking)}
                          disabled={cancellingId !== null}
                          className="rounded-lg bg-rose-50 px-3 py-2 text-[12px] font-bold text-rose-700 disabled:opacity-60"
                        >
                          {cancellingId === booking.bookingId ? "Đang huỷ..." : "Huỷ booking"}
                        </button>
                      )}
                      {booking.status === "Deposited" && booking.contractId === null && !refundBlocksContract(booking.refundStatus) && !booking.canReleaseRoom && (
                        <button
                          type="button"
                          onClick={() => cancelWithFullRefund(booking)}
                          disabled={cancellingId !== null}
                          aria-label={`Hủy (hoàn 100% cọc) #${booking.bookingId}`}
                          className="rounded-lg bg-amber-50 px-3 py-2 text-[12px] font-bold text-amber-800 disabled:opacity-60"
                        >
                          {cancellingId === booking.bookingId ? "Đang huỷ..." : "Hủy (hoàn 100% cọc)"}
                        </button>
                      )}
                      {booking.canReleaseRoom && (
                        <button
                          type="button"
                          onClick={() => release(booking)}
                          disabled={cancellingId !== null}
                          aria-label={`Nhả phòng (khách mất cọc) #${booking.bookingId}`}
                          className="rounded-lg bg-rose-50 px-3 py-2 text-[12px] font-bold text-rose-700 disabled:opacity-60"
                        >
                          {cancellingId === booking.bookingId ? "Đang giải phóng..." : "Nhả phòng (khách mất cọc)"}
                        </button>
                      )}
                      {booking.depositRefundDue && !booking.depositReturnedAtUtc && (
                        <button
                          type="button"
                          onClick={() => {
                            setConfirmReturnError(null)
                            setConfirmingReturnBooking(booking)
                          }}
                          disabled={cancellingId !== null || markingReturnedId !== null}
                          className="rounded-lg bg-emerald-600 px-3 py-2 text-[12px] font-bold text-white hover:bg-emerald-700 disabled:opacity-60"
                        >
                          Đã hoàn cọc
                        </button>
                      )}
                      {(Boolean(booking.refundStatus) || (ONLINE_REFUND_REQUESTS_ENABLED && booking.status === "Deposited" && booking.contractId === null)) && <button type="button"
                        aria-expanded={openedRefund === booking.bookingId} onClick={() => setOpenedRefund(value => value === booking.bookingId ? null : booking.bookingId)}
                        className="rounded-lg border border-[#087775] px-3 py-2 text-xs font-bold text-[#087775]">Theo dõi hoàn cọc #{booking.bookingId}</button>}
                    </div>
                  </div>{openedRefund === booking.bookingId && <div className="px-5 pb-5"><BookingRefundPanel booking={booking} onChanged={retry} /></div>}</div>
                )
              })}
            </div>
          )}
        </ManagementDataTable>
      )}
      {confirmingReturnBooking && (
        <div
          role="dialog"
          aria-modal="true"
          aria-labelledby="confirm-deposit-returned-title"
          className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4"
        >
          <div className="w-full max-w-md rounded-2xl bg-white p-6 shadow-xl">
            <h2 id="confirm-deposit-returned-title" className="font-display text-lg font-bold text-slate-900">
              Xác nhận đã hoàn cọc
            </h2>
            <p className="mt-3 text-sm text-slate-700">
              Khách <strong>{confirmingReturnBooking.customerName}</strong> ({confirmingReturnBooking.customerPhone}) · Booking #
              {confirmingReturnBooking.bookingId} · Phòng {confirmingReturnBooking.roomCode}
            </p>
            <p className="mt-3 text-2xl font-extrabold text-[#087775]">
              {formatVnd(confirmingReturnBooking.depositAmount)}
            </p>
            <p className="mt-3 rounded-lg bg-amber-50 p-3 text-xs font-medium text-amber-800">
              Chỉ xác nhận sau khi bạn ĐÃ chuyển tiền cho khách. Hệ thống không chuyển tiền.
            </p>
            {confirmReturnError && (
              <p role="alert" className="mt-3 text-xs font-medium text-rose-600">
                {confirmReturnError}
              </p>
            )}
            <div className="mt-5 flex justify-end gap-3">
              <button
                type="button"
                onClick={() => {
                  if (markingReturnedId !== null) return
                  setConfirmingReturnBooking(null)
                  setConfirmReturnError(null)
                }}
                disabled={markingReturnedId !== null}
                className="rounded-lg border border-slate-300 px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-60"
              >
                Hủy
              </button>
              <button
                type="button"
                onClick={() => void submitDepositReturned()}
                disabled={markingReturnedId !== null}
                className="rounded-lg bg-[#087775] px-4 py-2 text-sm font-bold text-white hover:bg-[#066a68] disabled:opacity-60"
              >
                {markingReturnedId === confirmingReturnBooking.bookingId ? "Đang xác nhận..." : "Xác nhận đã hoàn"}
              </button>
            </div>
          </div>
        </div>
      )}
      {toast && <Toast message={toast} onClose={() => setToast("")} />}
    </ManagementPage>
  )
}
