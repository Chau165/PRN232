import CustomerPageShell from "@/components/customer/CustomerPageShell"
import StatusBadge from "@/components/customer/StatusBadge"
import { bookings } from "@/data/customer"

function toneForStatus(status: string) {
  if (status === "Converted to Rental" || status === "Confirmed")
    return "green" as const
  if (status === "Cancelled") return "rose" as const
  if (status === "Deposit Paid") return "teal" as const
  return "amber" as const
}

export default function MyBookingsPage() {
  return (
    <CustomerPageShell
      title="Lịch đặt lịch & đặt cọc"
      description="Theo dõi lịch xem phòng, tiền cọc và trạng thái các yêu cầu thuê trước đây."
    >
      <div className="space-y-4">
        {bookings.map((booking) => (
          <article
            key={booking.id}
            className="rounded-2xl bg-white p-6 shadow-[0_3px_18px_rgba(16,65,67,.07)]"
          >
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <p className="text-[12px] font-bold uppercase tracking-[.12em] text-[#087775]">
                  {booking.id}
                </p>
                <h2 className="mt-2 font-display text-[20px] font-bold text-slate-900">
                  {booking.propertyName}
                </h2>
                <p className="mt-1 text-[13px] text-slate-500">
                  Phòng {booking.roomCode}
                </p>
              </div>
              <StatusBadge tone={toneForStatus(booking.bookingStatus)}>
                {booking.bookingStatus}
              </StatusBadge>
            </div>
            <div className="mt-5 grid gap-4 border-t border-slate-100 pt-5 text-[14px] sm:grid-cols-3">
              <div>
                <p className="text-slate-400">Ngày xem phòng</p>
                <p className="mt-1 font-semibold text-slate-700">
                  {booking.viewingDate}
                </p>
              </div>
              <div>
                <p className="text-slate-400">Tiền cọc</p>
                <p className="mt-1 font-bold text-[#087775]">
                  {booking.depositAmount}
                </p>
              </div>
              <div>
                <p className="text-slate-400">Thanh toán</p>
                <p className="mt-1 font-semibold text-slate-700">
                  {booking.paymentStatus}
                </p>
              </div>
            </div>
          </article>
        ))}
      </div>
    </CustomerPageShell>
  )
}
