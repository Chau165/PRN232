import { useEffect, useRef, useState } from "react"
import { Link, useNavigate, useSearchParams } from "react-router"
import CustomerPageShell from "@/components/customer/CustomerPageShell"
import { useAuth } from "@/auth/AuthContext"
import { createBooking, getBooking, createBookingPaymentLink } from "@/services/bookingService"
import type { BookingDto } from "@/types/booking"
import { bookingPaymentError, rememberBookingPayment, recalledBookingPayment } from "@/utils/bookingPayment"
import { openPayOSCheckout, positiveId, validPayOSCheckout } from "@/utils/invoicePayment"
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
    <CustomerPageShell centerHeader title="Đặt cọc giữ phòng" description="Tiền cọc tương đương 1 tháng tiền thuê.">
      <div className="w-full rounded-2xl bg-white p-5 shadow-[0_5px_24px_rgba(16,65,67,.08)] sm:p-8">
        {bookingId ? (
          <PayDepositStep key={bookingId} bookingId={bookingId} />
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
  const { accessToken, user } = useAuth()
  const navigate = useNavigate()
  const [booking, setBooking] = useState<BookingDto | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)
  const lock = useRef(false)
  const mounted = useRef(true)
  useEffect(() => { mounted.current = true; return () => { mounted.current = false } }, [])
  const recent = user ? recalledBookingPayment(user.id) : null

  useEffect(() => {
    if (!accessToken) return
    const controller = new AbortController()
    setBooking(null); setLoadError(null)
    getBooking(accessToken, bookingId, controller.signal)
      .then(value => { if (!controller.signal.aborted) setBooking(value) })
      .catch((fetchError: unknown) => {
        if (!controller.signal.aborted) {
          setLoadError(fetchError instanceof Error ? fetchError.message : "Không thể tải thông tin đặt cọc.")
        }
      })
    return () => controller.abort()
  }, [accessToken, bookingId])

  if (loadError) return <Notice message={loadError} />
  if (!booking) return <p role="status" className="text-sm text-slate-500">Đang tải thông tin đặt cọc...</p>
  if (booking.customerId !== Number(user?.id)) return <Notice message="Bạn không có quyền thanh toán booking này." />
  if (booking.status === "Deposited") return <Notice message="Booking này đã được thanh toán cọc." />
  if (booking.status === "Cancelled") return <Notice message="Booking này đã bị huỷ." />
  if (isBookingExpired(booking)) {
    return <Notice message="Đã quá thời gian giữ phòng. Vui lòng đặt lại nếu phòng vẫn còn trống." />
  }

  const pay = async (event: React.FormEvent) => {
    event.preventDefault()
    if (!accessToken || !user || lock.current) return
    lock.current = true
    setPending(true)
    setError(null)
    try {
      const result = await createBookingPaymentLink(accessToken, booking.bookingId)
      if (!mounted.current) return
      if (result.bookingId !== booking.bookingId || !positiveId(String(result.paymentRequestId)) || result.currency !== "VND" ||
        !Number.isSafeInteger(result.amount) || result.amount !== booking.depositAmount) throw new Error("Booking checkout mismatch")
      const checkout = validPayOSCheckout(result.checkoutUrl)
      rememberBookingPayment(user.id, result.paymentRequestId, booking.bookingId, checkout ? new URL(checkout).pathname.split("/")[2] : undefined)
      if (result.status === "Pending" && checkout && result.expiresAtUtc && Date.parse(result.expiresAtUtc) > Date.now()) openPayOSCheckout(checkout)
      else navigate(`${routes.paymentResult}?requestId=${result.paymentRequestId}`)
    } catch (payError) {
      if (mounted.current) setError(bookingPaymentError(payError))
    } finally { lock.current = false; if (mounted.current) setPending(false) }
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
      <div className="rounded-xl border border-teal-100 bg-teal-50/50 p-4 text-sm text-slate-700"><strong>Thanh toán cọc qua payOS</strong>
        <p className="mt-2">Mở trang thanh toán để quét mã hoặc chuyển khoản. Booking chỉ được xác nhận sau khi hệ thống nhận kết quả thanh toán.</p></div>
      {recent?.bookingId === bookingId && <Link to={`${routes.paymentResult}?requestId=${recent.requestId}`} className="block text-sm font-semibold text-[#087775]">Kiểm tra giao dịch cọc gần nhất</Link>}
      {error && <p role="alert" className="text-sm text-rose-600">{error}</p>}
      <button
        type="submit"
        disabled={pending}
        className="w-full rounded-lg bg-[#087775] py-3.5 text-sm font-bold text-white hover:bg-[#066a68] disabled:opacity-60"
      >
        {pending ? "Đang mở thanh toán..." : `Thanh toán ${formatVnd(booking.depositAmount)} qua payOS`}
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
