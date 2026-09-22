import { useRef, useState, type FormEvent } from "react"
import { Link, useNavigate, useSearchParams } from "react-router"
import ManagementPage from "@/components/management/ManagementPage"
import { routes } from "@/constants/routes"
import { getCurrentUser } from "@/utils/managementAuth"
import { getManagementScope } from "@/utils/managementScope"
import {
  addContractMonths,
  createBookingContract,
  formatContractDate,
  isContractDate,
} from "@/utils/managementContracts"

export default function CreateContractPage() {
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const user = getCurrentUser()
  const { bookings } = getManagementScope(user)
  const booking = bookings.find((item) => item.id === params.get("booking"))
  const [endDate, setEndDate] = useState("")
  const [error, setError] = useState("")
  const [saving, setSaving] = useState(false)
  const submitting = useRef(false)
  const startDate = booking?.moveInDate ?? ""
  const eligible = booking?.status === "Confirmed"
  const complete = !!booking?.customerPhone && isContractDate(startDate)

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!booking || submitting.current) return
    submitting.current = true
    setSaving(true)
    setError("")
    try {
      const contract = createBookingContract(getCurrentUser(), booking.id, endDate)
      navigate(routes.managementContracts, {
        replace: true,
        state: { createdContractId: contract.id },
      })
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Chưa thể lưu hợp đồng. Vui lòng thử lại.")
      submitting.current = false
      setSaving(false)
    }
  }

  return (
    <ManagementPage title="Tạo hợp đồng" description="Kiểm tra thông tin booking đã xác nhận và chọn thời hạn hợp đồng.">
      <Link to={routes.managementBookings} className="mb-5 inline-block text-sm font-semibold text-[#087775] hover:underline">
        ← Về Đặt phòng & Đặt cọc
      </Link>
      {!booking || !eligible ? (
        <div role="alert" className="rounded-2xl border border-amber-200 bg-amber-50 p-6 text-sm text-amber-800">
          {!booking
            ? "Không tìm thấy booking hoặc booking không thuộc khu trọ bạn quản lý."
            : "Booking phải ở trạng thái Confirmed và chưa tạo hợp đồng để thực hiện thao tác này."}
        </div>
      ) : (
        <form onSubmit={submit} className="max-w-3xl space-y-7 rounded-2xl border border-slate-100 bg-white p-5 shadow-sm sm:p-8">
          <div className="rounded-xl bg-teal-50 p-4">
            <p className="text-xs font-bold uppercase tracking-wider text-[#087775]">{booking.id} · Đã xác nhận</p>
            <h3 className="mt-2 text-lg font-bold text-slate-800">{booking.propertyName} · Phòng {booking.roomCode}</h3>
            <p className="mt-2 text-sm text-slate-500">Thông tin người thuê và ngày nhận phòng được lấy từ booking.</p>
          </div>

          <section aria-labelledby="tenant-heading">
            <h3 id="tenant-heading" className="mb-4 text-lg font-bold text-slate-800">Thông tin người thuê</h3>
            <div className="grid gap-4 sm:grid-cols-2">
              <ReadOnlyField label="Họ và tên người thuê" value={booking.customerName} />
              <ReadOnlyField label="Số điện thoại người thuê" value={booking.customerPhone ?? "Chưa có thông tin"} />
            </div>
          </section>

          <section aria-labelledby="companions-heading">
            <h3 id="companions-heading" className="mb-4 text-lg font-bold text-slate-800">Thông tin người ở cùng</h3>
            {booking.companions?.length ? (
              <div className="space-y-3">
                {booking.companions.map((companion, index) => (
                  <div key={index} className="rounded-xl border border-slate-100 p-4">
                    <p className="mb-3 text-xs font-bold text-[#087775]">Người ở cùng {index + 1}</p>
                    <div className="grid gap-4 sm:grid-cols-2">
                      <ReadOnlyField label="Họ và tên" value={companion.name} />
                      <ReadOnlyField label="Số điện thoại (không bắt buộc)" value={companion.phone || "Không cung cấp"} />
                    </div>
                  </div>
                ))}
              </div>
            ) : <p className="rounded-xl bg-slate-50 p-4 text-sm text-slate-500">Không có người ở cùng.</p>}
          </section>

          <ReadOnlyField label="Ngày nhận phòng" value={startDate ? formatContractDate(startDate) : "Chưa xác định"} />

          <section aria-labelledby="term-heading" className="border-t border-slate-100 pt-6">
            <h3 id="term-heading" className="mb-4 text-lg font-bold text-slate-800">Thời hạn hợp đồng</h3>
            <div className="grid gap-4 sm:grid-cols-2">
              <ReadOnlyField label="Ngày bắt đầu (ngày nhận phòng)" value={startDate ? formatContractDate(startDate) : "Chưa xác định"} />
              <label className="block text-sm font-semibold text-slate-700">
                Ngày kết thúc <span className="text-rose-600">*</span>
                <input
                  type="date"
                  required
                  min={startDate || undefined}
                  value={endDate}
                  disabled={!complete || saving}
                  onChange={(event) => { setEndDate(event.target.value); setError("") }}
                  className="mt-2 h-12 w-full rounded-xl border border-slate-200 px-3.5 outline-none focus:border-[#087775] focus:ring-2 focus:ring-teal-50"
                />
              </label>
            </div>
            <p className="mt-4 text-xs text-slate-500">Chọn nhanh thời hạn tính từ ngày bắt đầu:</p>
            <div className="mt-2 flex flex-wrap gap-2">
              {([{ label: "1 năm", months: 12 }, { label: "6 tháng", months: 6 }, { label: "2 năm", months: 24 }]).map(({ label, months }) => (
                <button
                  key={months}
                  type="button"
                  disabled={!complete || saving}
                  aria-pressed={!!endDate && endDate === addContractMonths(startDate, months)}
                  onClick={() => { setEndDate(addContractMonths(startDate, months)); setError("") }}
                  className="rounded-lg border border-teal-100 px-4 py-2 text-sm font-semibold text-[#087775] hover:bg-teal-50 aria-pressed:bg-teal-50 aria-pressed:border-[#087775] disabled:opacity-40"
                >{label}</button>
              ))}
            </div>
          </section>

          {!complete && <p role="alert" className="text-sm text-amber-700">Booking thiếu số điện thoại hoặc ngày nhận phòng. Cần bổ sung thông tin booking trước khi tạo hợp đồng.</p>}
          {error && <p role="alert" className="rounded-lg bg-rose-50 p-3 text-sm text-rose-600">{error}</p>}
          <div className="flex flex-wrap items-center justify-end gap-3 border-t border-slate-100 pt-6">
            <Link to={routes.managementBookings} className="rounded-lg bg-slate-100 px-5 py-3 text-sm font-semibold text-slate-600">Quay lại</Link>
            <button type="submit" disabled={!complete || saving} className="rounded-lg bg-[#087775] px-5 py-3 text-sm font-bold text-white hover:bg-[#066a68] disabled:cursor-not-allowed disabled:opacity-40">
              {saving ? "Đang lưu…" : "Xác nhận hợp đồng"}
            </button>
          </div>
        </form>
      )}
    </ManagementPage>
  )
}

function ReadOnlyField({ label, value }: { label: string; value: string }) {
  return (
    <label className="block text-sm font-semibold text-slate-700">
      {label}
      <input readOnly value={value} className="mt-2 h-12 w-full rounded-xl border border-transparent bg-slate-50 px-3.5 font-normal text-slate-600" />
    </label>
  )
}
