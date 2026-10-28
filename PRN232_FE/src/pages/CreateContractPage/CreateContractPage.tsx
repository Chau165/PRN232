import { useEffect, useState, type FormEvent } from "react"
import { Link, useNavigate, useSearchParams } from "react-router"
import ManagementPage from "@/components/management/ManagementPage"
import { useAuth } from "@/auth/AuthContext"
import { getBooking, type BookingDto } from "@/api/bookingsApi"
import { createContract } from "@/api/contractsApi"
import { parsePositiveRouteId, routes } from "@/constants/routes"
import { bookingErrorMessage } from "@/utils/bookingDisplay"
import { addContractMonths, isContractDate, localToday } from "@/utils/managementContracts"
import { formatVnd } from "@/utils/publicFormatting"

const quickTerms = [
  { label: "6 tháng", months: 6 },
  { label: "1 năm", months: 12 },
  { label: "2 năm", months: 24 },
]

export default function CreateContractPage() {
  const [params] = useSearchParams()
  const bookingId = parsePositiveRouteId(params.get("bookingId") ?? undefined)

  return (
    <ManagementPage title="Tạo hợp đồng" description="Kiểm tra thông tin booking đã đặt cọc và chọn thời hạn hợp đồng.">
      <Link to={routes.managementBookings} className="mb-5 inline-block text-sm font-semibold text-[#087775] hover:underline">
        ← Về Đặt phòng & Đặt cọc
      </Link>
      {bookingId ? <CreateContractForm bookingId={bookingId} /> : <Warning message="Không tìm thấy booking cần lập hợp đồng." />}
    </ManagementPage>
  )
}

function CreateContractForm({ bookingId }: { bookingId: number }) {
  const { accessToken } = useAuth()
  const navigate = useNavigate()
  const today = localToday()
  const [booking, setBooking] = useState<BookingDto | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [startDate, setStartDate] = useState(today)
  const [endDate, setEndDate] = useState("")
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    if (!accessToken) return
    const controller = new AbortController()
    getBooking(accessToken, bookingId, controller.signal)
      .then(setBooking)
      .catch((fetchError: unknown) => {
        if (!controller.signal.aborted) {
          setLoadError(fetchError instanceof Error ? fetchError.message : "Không thể tải booking.")
        }
      })
    return () => controller.abort()
  }, [accessToken, bookingId])

  if (loadError) return <Warning message={loadError} />
  if (!booking) return <p role="status" className="text-sm text-slate-500">Đang tải booking...</p>
  if (booking.contractId !== null) return <Warning message="Booking này đã được lập hợp đồng." />
  if (booking.status !== "Deposited") return <Warning message="Chỉ booking đã đặt cọc mới lập được hợp đồng." />

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!accessToken || saving) return
    if (!isContractDate(startDate) || startDate < today) {
      setError("Ngày bắt đầu phải từ hôm nay trở đi.")
      return
    }
    if (!isContractDate(endDate) || endDate <= startDate) {
      setError("Ngày kết thúc phải sau ngày bắt đầu.")
      return
    }
    setSaving(true)
    setError(null)
    try {
      const contract = await createContract(accessToken, { bookingId: booking.bookingId, startDate, endDate })
      navigate(routes.managementContracts, { replace: true, state: { createdContractId: contract.contractId } })
    } catch (createError) {
      setError(bookingErrorMessage(createError, "Booking vừa thay đổi hoặc phòng đã có hợp đồng khác. Vui lòng tải lại trang."))
      setSaving(false)
    }
  }

  return (
    <form onSubmit={submit} className="max-w-3xl space-y-7 rounded-2xl border border-slate-100 bg-white p-5 shadow-sm sm:p-8">
      <div className="rounded-xl bg-teal-50 p-4">
        <p className="text-xs font-bold uppercase tracking-wider text-[#087775]">Booking #{booking.bookingId} · Đã đặt cọc</p>
        <h3 className="mt-2 text-lg font-bold text-slate-800">{booking.propertyName} · Phòng {booking.roomCode}</h3>
        <p className="mt-2 text-sm text-slate-500">
          Tiền cọc đã nhận: {formatVnd(booking.depositAmount)}. Giá thuê lấy theo giá phòng hiện tại khi lập hợp đồng.
        </p>
      </div>

      <section aria-labelledby="tenant-heading">
        <h3 id="tenant-heading" className="mb-4 text-lg font-bold text-slate-800">Thông tin người thuê</h3>
        <div className="grid gap-4 sm:grid-cols-2">
          <ReadOnlyField label="Họ và tên người thuê" value={booking.customerName} />
          <ReadOnlyField label="Số điện thoại người thuê" value={booking.customerPhone || "Chưa có thông tin"} />
        </div>
      </section>

      <section aria-labelledby="term-heading" className="border-t border-slate-100 pt-6">
        <h3 id="term-heading" className="mb-4 text-lg font-bold text-slate-800">Thời hạn hợp đồng</h3>
        <div className="grid gap-4 sm:grid-cols-2">
          <DateField label="Ngày bắt đầu" value={startDate} min={today} disabled={saving}
            onChange={(value) => { setStartDate(value); setError(null) }} />
          <DateField label="Ngày kết thúc" value={endDate} min={startDate} disabled={saving}
            onChange={(value) => { setEndDate(value); setError(null) }} />
        </div>
        <p className="mt-4 text-xs text-slate-500">Chọn nhanh thời hạn tính từ ngày bắt đầu:</p>
        <div className="mt-2 flex flex-wrap gap-2">
          {quickTerms.map(({ label, months }) => (
            <button
              key={months}
              type="button"
              disabled={saving}
              aria-pressed={!!endDate && endDate === addContractMonths(startDate, months)}
              onClick={() => { setEndDate(addContractMonths(startDate, months)); setError(null) }}
              className="rounded-lg border border-teal-100 px-4 py-2 text-sm font-semibold text-[#087775] hover:bg-teal-50 aria-pressed:border-[#087775] aria-pressed:bg-teal-50 disabled:opacity-40"
            >
              {label}
            </button>
          ))}
        </div>
      </section>

      {error && <p role="alert" className="rounded-lg bg-rose-50 p-3 text-sm text-rose-600">{error}</p>}
      <div className="flex flex-wrap items-center justify-end gap-3 border-t border-slate-100 pt-6">
        <Link to={routes.managementBookings} className="rounded-lg bg-slate-100 px-5 py-3 text-sm font-semibold text-slate-600">Quay lại</Link>
        <button type="submit" disabled={saving} className="rounded-lg bg-[#087775] px-5 py-3 text-sm font-bold text-white hover:bg-[#066a68] disabled:cursor-not-allowed disabled:opacity-40">
          {saving ? "Đang lưu…" : "Xác nhận hợp đồng"}
        </button>
      </div>
    </form>
  )
}

function DateField({ label, value, min, disabled, onChange }: {
  label: string
  value: string
  min: string
  disabled: boolean
  onChange: (value: string) => void
}) {
  return (
    <label className="block text-sm font-semibold text-slate-700">
      {label} <span className="text-rose-600">*</span>
      <input
        type="date"
        required
        min={min}
        value={value}
        disabled={disabled}
        onChange={(event) => onChange(event.target.value)}
        className="mt-2 h-12 w-full rounded-xl border border-slate-200 px-3.5 outline-none focus:border-[#087775] focus:ring-2 focus:ring-teal-50"
      />
    </label>
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

function Warning({ message }: { message: string }) {
  return <div role="alert" className="rounded-2xl border border-amber-200 bg-amber-50 p-6 text-sm text-amber-800">{message}</div>
}
