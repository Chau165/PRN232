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
import { bookingErrorMessage, bookingStatusDisplay, formatDateTime } from "@/utils/bookingDisplay"
import { formatVnd } from "@/utils/publicFormatting"

export default function ManagementBookingsPage() {
  const { user } = useAuth()
  const { bookings, loading, error, retry, cancel: cancelById, cancellingId } = useBookings()
  const [toast, setToast] = useState("")
  const [openedRefund, setOpenedRefund] = useState<number | null>(null)

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
                    </span>
                    <span className="font-semibold text-[#087775]">{formatVnd(booking.depositAmount)}</span>
                    <span className="text-[13px] text-slate-600">
                      {booking.status === "Cancelled" ? "—" : booking.status === "Deposited" ? "Đã cọc, giữ đến khi chốt hợp đồng hoặc hoàn cọc" : formatDateTime(booking.expiryDate)}
                    </span>
                    <div className="space-y-2"><StatusBadge tone={display.tone}>{display.label}</StatusBadge>
                      {booking.refundStatus && <StatusBadge tone={refundDisplay(booking.refundStatus).tone}>{refundDisplay(booking.refundStatus).label}</StatusBadge>}</div>
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
                      {(booking.status === "Deposited" && booking.contractId === null || booking.refundStatus) && <button type="button"
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
      {toast && <Toast message={toast} onClose={() => setToast("")} />}
    </ManagementPage>
  )
}
