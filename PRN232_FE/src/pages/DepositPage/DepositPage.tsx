import { bookingRoomLabel } from "@/utils/bookingDisplay"
import { useEffect, useRef, useState } from "react"
import { Link, useNavigate, useSearchParams } from "react-router"
import { ApiError } from "@/api/apiClient"
import CustomerPageShell from "@/components/customer/CustomerPageShell"
import { useAuth } from "@/auth/AuthContext"
import { createBooking, getBooking, createBookingPaymentLink } from "@/services/bookingService"
import type { BookingDto } from "@/types/booking"
import { bookingPaymentError, rememberBookingPayment, recalledBookingPayment } from "@/utils/bookingPayment"
import { openPayOSCheckout, positiveId, validPayOSCheckout } from "@/utils/invoicePayment"
import { publicApi } from "@/services/publicBrowsingService"
import { usePublicApiResource } from "@/hooks/usePublicApiResource"
import { parsePositiveRouteId, routes } from "@/constants/routes"
import {
  BOOKING_MAX_MOVE_IN_DAYS,
  BOOKING_OCCUPANTS_MAX,
  BOOKING_OCCUPANTS_MIN,
  bookingErrorMessage,
  bookingMoveInDateError,
  bookingOccupantCountError,
  formatBookingDepositTermText,
  formatBookingMoveInSummary,
  formatDateTime,
  formatMoveInHoldEndDate,
  getVietnamIsoDateOffset,
  isBookingExpired,
  pendingBookingIdFromError,
} from "@/utils/bookingDisplay"
import { publicApiErrorMessage } from "@/utils/publicApiDisplay"
import { formatVnd } from "@/utils/publicFormatting"
import { splitValidationErrors } from "@/utils/roomValidation"


// Bước 1 (?roomId=): giữ phòng bằng một booking Pending. Bước 2 (?bookingId=): thanh toán cọc
// trong thời gian giữ phòng. Tách theo URL để tải lại trang ở bước 2 không tạo thêm booking mới.
export default function DepositPage() {
  const [params] = useSearchParams()
  const bookingId = parsePositiveRouteId(params.get("bookingId") ?? undefined)
  const roomId = parsePositiveRouteId(params.get("roomId") ?? undefined)
  const propertyId = parsePositiveRouteId(params.get("propertyId") ?? undefined)
  const roomTypeId = parsePositiveRouteId(params.get("roomTypeId") ?? undefined)
  const viewingId = parsePositiveRouteId(params.get("viewingId") ?? undefined)

  return (
    <CustomerPageShell centerHeader title="Đặt cọc giữ phòng" description="Tiền cọc tương đương 1 tháng tiền thuê.">
      <div className="w-full rounded-2xl bg-white p-5 shadow-[0_5px_24px_rgba(16,65,67,.08)] sm:p-8">
        {bookingId ? (
          <PayDepositStep key={bookingId} bookingId={bookingId} />
        ) : roomId || propertyId && roomTypeId ? (
          <HoldRoomStep roomId={roomId} propertyId={propertyId} roomTypeId={roomTypeId} viewingId={viewingId} />
        ) : (
          <Notice message="Không tìm thấy phòng cần đặt cọc." />
        )}
      </div>
    </CustomerPageShell>
  )
}

function HoldRoomStep({ roomId, propertyId, roomTypeId, viewingId }: {
  roomId?: number | null; propertyId?: number | null; roomTypeId?: number | null; viewingId?: number | null
}) {
  const { accessToken, user } = useAuth()
  const navigate = useNavigate()
  const resource = usePublicApiResource(`${roomId}:${propertyId}:${roomTypeId}`, async (signal) => {
    if (propertyId && roomTypeId) {
      const types = await publicApi.listRoomTypes(propertyId, {}, signal)
      const type = types.find((item) => item.roomTypeId === roomTypeId)
      if (!type || type.availableRoomCount < 1) throw new Error("Loại phòng hiện không còn suất đặt cọc.")
      return { propertyId, roomTypeId, propertyName: type.propertyName, roomTypeName: type.roomTypeName,
        monthlyRent: type.monthlyRent, maxOccupants: undefined as number | undefined }
    }
    const room = await publicApi.getRoom(roomId!, signal)
    return { propertyId: room.property.propertyId, roomTypeId: room.roomType.roomTypeId,
      propertyName: room.property.propertyName, roomTypeName: room.roomType.roomTypeName,
      monthlyRent: room.baseRent, maxOccupants: room.maxOccupants }
  })
  const [moveInDate, setMoveInDate] = useState<string>(() => getVietnamIsoDateOffset(0))
  const [occupantCount, setOccupantCount] = useState<string>("1")
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<"moveInDate" | "occupantCount", string>>>({})
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)
  const [existingPendingBookingId, setExistingPendingBookingId] = useState<number | null>(null)

  if (resource.status === "loading") return <p role="status" className="text-sm text-slate-500">Đang tải thông tin phòng...</p>
  if (resource.status === "error") {
    return (
      <Notice
        message={resource.error.kind === "not-found" ? "Phòng hiện không còn trống để đặt cọc." : publicApiErrorMessage(resource.error)}
      />
    )
  }

  const room = resource.data
  const minMoveInDate = getVietnamIsoDateOffset(0)
  const maxMoveInDate = getVietnamIsoDateOffset(BOOKING_MAX_MOVE_IN_DAYS)
  const maxOccupantsLimit = room.maxOccupants ?? BOOKING_OCCUPANTS_MAX
  const holdEndDateLabel = formatMoveInHoldEndDate(moveInDate)

  const holdRoom = async () => {
    if (!accessToken || pending) return
    const nextFieldErrors: Partial<Record<"moveInDate" | "occupantCount", string>> = {}
    const moveInErr = bookingMoveInDateError(moveInDate)
    if (moveInErr) nextFieldErrors.moveInDate = moveInErr
    const occupantErr = bookingOccupantCountError(occupantCount, { roomCode: room.roomTypeName, maxOccupants: room.maxOccupants })
    if (occupantErr) nextFieldErrors.occupantCount = occupantErr

    if (Object.keys(nextFieldErrors).length > 0) {
      setFieldErrors(nextFieldErrors)
      setError(null)
      return
    }

    setPending(true)
    setFieldErrors({})
    setError(null)
    setExistingPendingBookingId(null)
    try {
      const booking = await createBooking(accessToken, {
        propertyId: room.propertyId,
        roomTypeId: room.roomTypeId,
        ...(viewingId ? { viewingId } : {}),
        moveInDate: moveInDate.trim() || undefined,
        occupantCount: occupantCount.trim() ? Number(occupantCount) : undefined,
      })
      navigate(`${routes.deposit}?bookingId=${booking.bookingId}`, { replace: true })
    } catch (holdError) {
      if (holdError instanceof ApiError && holdError.status === 400 && holdError.validationErrors) {
        const { fieldErrors: mapped, generalError } = splitValidationErrors(
          holdError.validationErrors,
          ["moveInDate", "occupantCount"] as const,
          holdError.message,
        )
        setFieldErrors(mapped)
        setError(generalError || null)
        setPending(false)
        return
      }
      const pendingBookingId = pendingBookingIdFromError(holdError)
      if (pendingBookingId !== null && holdError instanceof Error) {
        // B4: one unpaid booking at a time; show the backend message and a link to that booking.
        setExistingPendingBookingId(pendingBookingId)
        setError(holdError.message)
        setPending(false)
        return
      }
      setError(bookingErrorMessage(holdError, "Phòng vừa được người khác giữ hoặc không còn trống."))
      setPending(false)
    }
  }

  return (
    <div className="space-y-6">
      <Summary
        rows={[
          ["Khu trọ", room.propertyName],
          ["Loại phòng", `${room.roomTypeName} — Chưa phân phòng cụ thể`],
          ["Người đặt", user ? `${user.name} · ${user.phone}` : ""],
        ]}
        amount={room.monthlyRent}
      />

      <div className="grid grid-cols-1 gap-4 rounded-xl border border-slate-200 bg-slate-50/70 p-4 sm:grid-cols-2">
        <label className="block space-y-1.5 text-sm">
          <span className="font-medium text-slate-700">Ngày dọn vào dự kiến</span>
          <input
            type="date"
            aria-label="Ngày dọn vào"
            min={minMoveInDate}
            max={maxMoveInDate}
            value={moveInDate}
            onChange={(e) => {
              setMoveInDate(e.target.value)
              setFieldErrors((prev) => ({ ...prev, moveInDate: undefined }))
            }}
            disabled={pending}
            className={`w-full rounded-lg border bg-white px-3 py-2 text-sm text-slate-900 ${
              fieldErrors.moveInDate ? "border-rose-400" : "border-slate-300"
            }`}
          />
          <span className="block text-xs text-slate-500">Tối đa 14 ngày kể từ hôm nay</span>
          {fieldErrors.moveInDate && <span className="block text-xs text-rose-600">{fieldErrors.moveInDate}</span>}
        </label>

        <label className="block space-y-1.5 text-sm">
          <span className="font-medium text-slate-700">Số người ở</span>
          <input
            type="number"
            aria-label="Số người ở"
            min={BOOKING_OCCUPANTS_MIN}
            max={maxOccupantsLimit}
            step={1}
            value={occupantCount}
            onChange={(e) => {
              setOccupantCount(e.target.value)
              setFieldErrors((prev) => ({ ...prev, occupantCount: undefined }))
            }}
            disabled={pending}
            className={`w-full rounded-lg border bg-white px-3 py-2 text-sm text-slate-900 ${
              fieldErrors.occupantCount ? "border-rose-400" : "border-slate-300"
            }`}
          />
          <span className="block text-xs text-slate-500">
            {room.maxOccupants != null
              ? `Tối đa ${room.maxOccupants} người cho phòng ${room.roomTypeName}`
              : `Từ ${BOOKING_OCCUPANTS_MIN} đến ${BOOKING_OCCUPANTS_MAX} người`}
          </span>
          {fieldErrors.occupantCount && <span className="block text-xs text-rose-600">{fieldErrors.occupantCount}</span>}
        </label>
      </div>

      <p className="text-sm text-slate-500">Sau khi giữ phòng, bạn có 15 phút để thanh toán tiền cọc.</p>
      <p className="text-sm text-slate-500">
        Tiền cọc được giữ làm tiền đảm bảo trong suốt thời gian thuê và hoàn lại khi trả phòng, sau khi trừ tiền phòng còn
        nợ và hư hỏng thiết bị (nếu có). Tháng đầu tiên bạn đóng đủ tiền phòng.{" "}
        {holdEndDateLabel
          ? `Phòng được giữ đến hết ngày ${holdEndDateLabel}.`
          : "Phòng được giữ đến hết ngày dọn vào + 3 ngày."}{" "}
        Nếu bạn hủy hoặc không đến nhận phòng trước hạn đó, tiền cọc không được hoàn. Nếu chủ trọ hủy, bạn được hoàn
        100% (liên hệ trực tiếp quản lý để nhận lại tiền).
      </p>
      {error && <p role="alert" className="text-sm text-rose-600">{error}</p>}
      {existingPendingBookingId !== null && (
        <Link
          to={`${routes.deposit}?bookingId=${existingPendingBookingId}`}
          className="inline-block text-sm font-semibold text-[#087775] hover:underline"
        >
          Xem đặt phòng đang chờ thanh toán
        </Link>
      )}
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
  if (booking.status === "Deposited") return <Notice message={booking.roomId === null ? `Đã đặt cọc ${bookingRoomLabel(booking)}.` : "Booking này đã được thanh toán cọc."} />
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
          ["Phòng / Loại phòng", bookingRoomLabel(booking)],
          ["Dự kiến nhận phòng", formatBookingMoveInSummary(booking) ?? ""],
          ["Hạn thanh toán cọc", formatDateTime(booking.expiryDate)],
        ]}
        amount={booking.depositAmount}
      />
      <p className="text-sm text-slate-500">{formatBookingDepositTermText(booking)}</p>
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
