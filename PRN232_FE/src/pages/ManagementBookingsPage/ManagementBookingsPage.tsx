import { useState } from "react"
import { Link } from "react-router"
import { routes } from "@/constants/routes"
import ManagementDataTable from "@/components/management/ManagementDataTable"
import ManagementDetailDrawer from "@/components/management/ManagementDetailDrawer"
import ManagementPage from "@/components/management/ManagementPage"
import StatusBadge from "@/components/customer/StatusBadge"
import Toast from "@/components/customer/Toast"
import { getCurrentUser } from "@/utils/managementAuth"
import { getManagementScope } from "@/utils/managementScope"
import type { ManagedBooking } from "@/types/management"

function bookingTone(status: string) {
  return status === "Confirmed" || status === "Converted to Rental"
    ? "green" as const
    : status === "Cancelled"
      ? "rose" as const
      : "amber" as const
}
export default function ManagementBookingsPage() {
  const { bookings } = getManagementScope(getCurrentUser())
  const [selected, setSelected] = useState<ManagedBooking | null>(null)
  const [toast, setToast] = useState("")
  return (
    <ManagementPage
      title="Đặt phòng & Đặt cọc"
      description="Theo dõi lịch xem phòng, tiền cọc và trạng thái chuyển đổi hợp đồng."
    >
      <ManagementDataTable
        headers={[
          "Booking ID",
          "Khách hàng",
          "Khu trọ / Phòng",
          "Ngày xem",
          "Tiền cọc",
          "Trạng thái",
          "Thao tác",
        ]}
      >
        <div className="divide-y divide-slate-100">
          {bookings.map((booking) => (
            <div
              key={booking.id}
              className="grid gap-3 px-5 py-5 lg:grid-cols-[1fr_1fr_1.2fr_1.2fr_1fr_1fr_1fr] lg:items-center"
            >
              <strong className="text-[13px] text-slate-700">
                {booking.id}
              </strong>
              <span className="text-[13px] text-slate-600">
                {booking.customerName}
                <small className="block text-slate-400">
                  {booking.occupants}
                </small>
              </span>
              <span className="text-[13px] text-slate-600">
                {booking.propertyName}
                <small className="block text-slate-400">
                  Phòng {booking.roomCode}
                </small>
              </span>
              <span className="text-[13px] text-slate-600">
                {booking.viewingDate}
              </span>
              <span className="font-semibold text-[#087775]">
                {booking.deposit}
              </span>
              <StatusBadge tone={bookingTone(booking.status)}>
                {booking.status}
              </StatusBadge>
              <div className="flex flex-wrap gap-2">
                <button
                  onClick={() => setSelected(booking)}
                  className="rounded-lg bg-slate-100 px-3 py-2 text-[12px] font-bold text-slate-600"
                >
                  Xem
                </button>
                {booking.status === "Pending" && (
                  <button
                    onClick={() => setToast("Booking đã được xác nhận mock")}
                    className="rounded-lg bg-teal-50 px-3 py-2 text-[12px] font-bold text-[#087775]"
                  >
                    Duyệt
                  </button>
                )}
                {booking.status === "Confirmed" && (
                  <Link
                    to={`${routes.managementCreateContract}?booking=${encodeURIComponent(booking.id)}`}
                    className="rounded-lg bg-[#087775] px-3 py-2 text-[12px] font-bold text-white hover:bg-[#066a68]"
                  >
                    Tạo hợp đồng
                  </Link>
                )}
              </div>
            </div>
          ))}
        </div>
      </ManagementDataTable>
      {selected && (
        <ManagementDetailDrawer
          title={`Booking ${selected.id}`}
          onClose={() => setSelected(null)}
        >
          <div className="space-y-3 rounded-xl bg-slate-50 p-4 text-[14px]">
            <p>
              Khách hàng: <strong>{selected.customerName}</strong>
            </p>
            <p>
              Khu trọ: <strong>{selected.propertyName}</strong>
            </p>
            <p>
              Phòng: <strong>{selected.roomCode}</strong>
            </p>
            <p>
              Ngày xem: <strong>{selected.viewingDate}</strong>
            </p>
            <p>
              Thanh toán: <strong>{selected.paymentStatus}</strong>
            </p>
          </div>
        </ManagementDetailDrawer>
      )}
      {toast && <Toast message={toast} onClose={() => setToast("")} />}
    </ManagementPage>
  )
}
