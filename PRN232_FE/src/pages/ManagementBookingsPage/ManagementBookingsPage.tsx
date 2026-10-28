import { useState } from "react"
import { Link } from "react-router"
import ManagementDataTable from "@/components/management/ManagementDataTable"
import ManagementPage from "@/components/management/ManagementPage"
import StatusBadge from "@/components/customer/StatusBadge"
import Toast from "@/components/customer/Toast"
import type { BookingDto } from "@/api/bookingsApi"
import { routes } from "@/constants/routes"
import { useBookings } from "@/hooks/useBookings"
import { bookingErrorMessage, bookingStatusDisplay, formatDateTime } from "@/utils/bookingDisplay"
import { formatVnd } from "@/utils/publicFormatting"

export default function ManagementBookingsPage() {
  const { bookings, loading, error, retry, cancel: cancelById, cancellingId } = useBookings()
  const [toast, setToast] = useState("")

  const cancel = async (booking: BookingDto) => {
    if (cancellingId !== null) return
    const question = booking.status === "Deposited"
      ? `Khách ${booking.customerName} đã đặt cọc phòng ${booking.roomCode}. Huỷ sẽ nhả phòng và báo cho khách; việc hoàn cọc xử lý ngoài hệ thống. Tiếp tục?`
      : `Huỷ booking #${booking.bookingId} của khách ${booking.customerName}?`
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
      description="Theo dõi các phòng đang được giữ, tiền cọc và huỷ booking khi khách không tiếp tục thuê."
    >
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
                  <div key={booking.bookingId} className="grid gap-3 px-5 py-5 lg:grid-cols-7 lg:items-center">
                    <strong className="text-[13px] text-slate-700">#{booking.bookingId}</strong>
                    <span className="text-[13px] text-slate-600">{booking.customerName}</span>
                    <span className="text-[13px] text-slate-600">
                      {booking.propertyName}
                      <small className="block text-slate-400">Phòng {booking.roomCode}</small>
                    </span>
                    <span className="font-semibold text-[#087775]">{formatVnd(booking.depositAmount)}</span>
                    <span className="text-[13px] text-slate-600">
                      {booking.status === "Cancelled" ? "—" : formatDateTime(booking.expiryDate)}
                    </span>
                    <StatusBadge tone={display.tone}>{display.label}</StatusBadge>
                    {/* Booking đã lên hợp đồng thì không còn thao tác: BE cũng chặn huỷ trường hợp này. */}
                    <div className="flex flex-wrap gap-2">
                      {booking.status === "Deposited" && booking.contractId === null && (
                        <Link
                          to={`${routes.managementCreateContract}?bookingId=${booking.bookingId}`}
                          className="rounded-lg bg-[#087775] px-3 py-2 text-[12px] font-bold text-white hover:bg-[#066a68]"
                        >
                          Tạo hợp đồng
                        </Link>
                      )}
                      {booking.status !== "Cancelled" && booking.contractId === null && (
                        <button
                          type="button"
                          onClick={() => cancel(booking)}
                          disabled={cancellingId !== null}
                          className="rounded-lg bg-rose-50 px-3 py-2 text-[12px] font-bold text-rose-700 disabled:opacity-60"
                        >
                          {cancellingId === booking.bookingId ? "Đang huỷ..." : "Huỷ booking"}
                        </button>
                      )}
                    </div>
                  </div>
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
