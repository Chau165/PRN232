import { useEffect, useState } from "react"
import { Link, useNavigate, useSearchParams } from "react-router"
import CustomerPageShell from "@/components/customer/CustomerPageShell"
import PaymentMethodPicker from "@/components/customer/PaymentMethodPicker"
import { useAuth } from "@/auth/AuthContext"
import { createBooking, getBooking, payBooking } from "@/services/bookingService"
import type { BookingDto, PaymentMethod } from "@/types/booking"
import { publicApi } from "@/services/publicBrowsingService"
import { usePublicApiResource } from "@/hooks/usePublicApiResource"
import { parsePositiveRouteId, routes } from "@/constants/routes"
import { bookingErrorMessage, formatDateTime, isBookingExpired } from "@/utils/bookingDisplay"
import { publicApiErrorMessage } from "@/utils/publicApiDisplay"
import { formatVnd } from "@/utils/publicFormatting"


// Bước 1 (?roomId=): giữ phòng bằng một booking Pending. Bước 2 (?bookingId=): thanh toán cọc
// trong thời gian giữ phòng. Tách theo URL để tải lại trang ở bước 2 không tạo thêm booking mới.
export default function DepositPage() {
  const [params] = useSearchParams()
  const bookingId = parsePositiveRouteId(params.get("bookingId") ?? undefined)
  const roomId = parsePositiveRouteId(params.get("roomId") ?? undefined)

  return (
    <CustomerPageShell title="Đặt cọc giữ phòng" description="Tiền cọc tương đương 1 tháng tiền thuê.">
      <div className="max-w-2xl rounded-2xl bg-white p-7 shadow-[0_5px_24px_rgba(16,65,67,.08)]">
        {bookingId ? (
          <PayDepositStep bookingId={bookingId} />
        ) : roomId ? (
          <HoldRoomStep roomId={roomId} />
        ) : (
          <Notice message="Không tìm thấy phòng cần đặt cọc." />
        )}
      </div>
    </CustomerPageShell>
  )
}

function HoldRoomStep({ roomId }: { roomId: number }) {
  const { accessToken, user } = useAuth()
  const navigate = useNavigate()
  const resource = usePublicApiResource(String(roomId), (signal) => publicApi.getRoom(roomId, signal))
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)

  if (resource.status === "loading") return <p role="status" className="text-sm text-slate-500">Đang tải thông tin phòng...</p>
  if (resource.status === "error") {
    return (
      <Notice
        message={resource.error.kind === "not-found" ? "Phòng hiện không còn trống để đặt cọc." : publicApiErrorMessage(resource.error)}
      />
    )
  }

  const room = resource.data
  const holdRoom = async () => {
    if (!accessToken || pending) return
    setPending(true)
    setError(null)
    try {
      const booking = await createBooking(accessToken, room.roomId)
      navigate(`${routes.deposit}?bookingId=${booking.bookingId}`, { replace: true })
    } catch (holdError) {
      setError(bookingErrorMessage(holdError, "Phòng vừa được người khác giữ hoặc không còn trống."))
      setPending(false)
    }
  }

  return (
    <div className="space-y-6">
      <Summary
        rows={[
          ["Khu trọ", room.property.propertyName],
          ["Phòng", `${room.roomCode} · ${room.roomType.roomTypeName}`],
          ["Người đặt", user ? `${user.name} · ${user.phone}` : ""],
        ]}
        amount={room.baseRent}
      />
      <p className="text-sm text-slate-500">Sau khi giữ phòng, bạn có 15 phút để thanh toán tiền cọc.</p>
      {error && <p role="alert" className="text-sm text-rose-600">{error}</p>}
      <button
        type="button"
        onClick={holdRoom}
        disabled={pending}
        className="w-full rounded-lg bg-[#087775] py-3.5 text-sm font-bold text-white hover:bg-[#066a68] disabled:opacity-60"
      >
        {pending ? "Đang giữ phòng..." : "Giữ phòng và thanh toán cọc"}
      </button>
    </div>
  )
}

function PayDepositStep({ bookingId }: { bookingId: number }) {
  const { accessToken } = useAuth()
  const navigate = useNavigate()
  const [booking, setBooking] = useState<BookingDto | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [method, setMethod] = useState<PaymentMethod | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)

  useEffect(() => {
    if (!accessToken) return
    const controller = new AbortController()
    getBooking(accessToken, bookingId, controller.signal)
      .then(setBooking)
      .catch((fetchError: unknown) => {
        if (!controller.signal.aborted) {
          setLoadError(fetchError instanceof Error ? fetchError.message : "Không thể tải thông tin đặt cọc.")
        }
      })
    return () => controller.abort()
  }, [accessToken, bookingId])

  if (loadError) return <Notice message={loadError} />
  if (!booking) return <p role="status" className="text-sm text-slate-500">Đang tải thông tin đặt cọc...</p>
  if (booking.status === "Deposited") return <Notice message="Booking này đã được thanh toán cọc." />
  if (booking.status === "Cancelled") return <Notice message="Booking này đã bị huỷ." />
  if (isBookingExpired(booking)) {
    return <Notice message="Đã quá thời gian giữ phòng. Vui lòng đặt lại nếu phòng vẫn còn trống." />
  }

  const pay = async (event: React.FormEvent) => {
    event.preventDefault()
    if (!accessToken || pending) return
    if (!method) {
      setError("Vui lòng chọn phương thức thanh toán.")
      return
    }
    setPending(true)
    setError(null)
    try {
      await payBooking(accessToken, booking.bookingId, method)
      navigate(routes.paymentSuccess, { replace: true })
    } catch (payError) {
      setError(bookingErrorMessage(payError, "Booking đã hết thời gian giữ phòng hoặc đã được thanh toán."))
      setPending(false)
    }
  }

  return (
    <form onSubmit={pay} className="space-y-6">
      <Summary
        rows={[
          ["Khu trọ", booking.propertyName],
          ["Phòng", booking.roomCode],
          ["Giữ phòng đến", formatDateTime(booking.expiryDate)],
        ]}
        amount={booking.depositAmount}
      />
      <PaymentMethodPicker value={method} onChange={setMethod} />
      {error && <p role="alert" className="text-sm text-rose-600">{error}</p>}
      <button
        type="submit"
        disabled={pending}
        className="w-full rounded-lg bg-[#087775] py-3.5 text-sm font-bold text-white hover:bg-[#066a68] disabled:opacity-60"
      >
        {pending ? "Đang thanh toán..." : `Thanh toán ${formatVnd(booking.depositAmount)}`}
      </button>
    </form>
  )
}

function Summary({ rows, amount }: { rows: Array<[string, string]>; amount: number }) {
  return (
    <div className="space-y-4">
      <dl className="grid gap-3 text-sm sm:grid-cols-2">
        {rows.filter(([, value]) => value).map(([label, value]) => (
          <div key={label}>
            <dt className="text-slate-400">{label}</dt>
            <dd className="mt-1 font-semibold text-slate-700">{value}</dd>
          </div>
        ))}
      </dl>
      <div className="rounded-xl bg-teal-50 p-4 text-sm text-[#087775]">
        Tiền cọc cần thanh toán: <strong>{formatVnd(amount)}</strong>
      </div>
    </div>
  )
}

function Notice({ message }: { message: string }) {
  return (
    <div className="text-center">
      <p role="alert" className="text-[15px] font-semibold text-slate-700">{message}</p>
      <Link to={routes.myBookings} className="mt-4 inline-block text-sm font-semibold text-[#087775] hover:underline">
        Xem booking của tôi
      </Link>
    </div>
  )
}
