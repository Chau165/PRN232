import { useEffect, useRef, useState } from "react"
import { useAuth } from "@/auth/AuthContext"
import { ApiError } from "@/api/apiClient"
import { useBookingRefundHistory } from "@/hooks/useBookingRefundHistory"
import { createBookingRefund } from "@/services/bookingRefundService"
import type { BookingDto } from "@/types/booking"
import type { CreateBookingRefundDto } from "@/types/bookingRefund"
import {
  refundDisplay,
  refundError,
  validateRefund,
} from "@/utils/bookingRefund"
import { formatVnd } from "@/utils/publicFormatting"
import { formatDateTime } from "@/utils/bookingDisplay"
import StatusBadge from "@/components/customer/StatusBadge"
import { refundBanks, refundBankName } from "@/constants/refundBanks"

const field =
  "mt-1 w-full rounded-lg border border-slate-200 bg-white px-3 py-2.5 text-sm text-slate-800"
export default function BookingRefundPanel({
  booking,
  onChanged,
}: {
  booking: BookingDto
  onChanged: () => void
}) {
  const { user, accessToken } = useAuth()
  const history = useBookingRefundHistory(booking.bookingId)
  const latest = history.data?.[0]
  const notified = useRef<string | null>(null)
  useEffect(() => {
    const changed =
      latest &&
      ((booking.refundStatus !== undefined &&
        booking.refundStatus !== latest.status) ||
        (latest.status === "Refunded" && booking.status !== "Cancelled"))
    if (changed && notified.current !== latest.status) {
      notified.current = latest.status
      onChanged()
    }
  }, [latest?.status, booking.status, booking.refundStatus, onChanged])
  return (
    <section
      aria-label={`Hoàn cọc booking ${booking.bookingId}`}
      className="mt-4 space-y-4 rounded-xl border border-teal-100 bg-teal-50/40 p-4"
    >
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h3 className="font-semibold text-slate-800">Yêu cầu hoàn cọc</h3>
        <button
          type="button"
          onClick={history.refresh}
          className="text-sm font-semibold text-[#087775]"
        >
          Cập nhật hoàn cọc
        </button>
      </div>
      {history.loading && (
        <p role="status" className="text-sm text-slate-500">
          Đang tải yêu cầu hoàn cọc...
        </p>
      )}
      {history.error && (
        <p role="alert" className="text-sm text-rose-700">
          {history.error}
        </p>
      )}
      {history.paused && (
        <p className="text-xs text-slate-500">
          Đã tạm dừng cập nhật tự động. Bấm cập nhật để xem tiến độ mới.
        </p>
      )}
      {history.data?.map((item) => {
        const display = refundDisplay(item.status)
        return (
          <article
            key={item.bookingRefundRequestId}
            className="space-y-2 rounded-lg bg-white p-4 text-sm"
          >
            <div className="flex flex-wrap justify-between gap-2">
              <strong>Đơn #{item.bookingRefundRequestId}</strong>
              <StatusBadge tone={display.tone}>{display.label}</StatusBadge>
            </div>
            <p>
              Yêu cầu hoàn: <strong>{formatVnd(item.requestedAmount)}</strong>
              {item.approvedAmount !== null && (
                <>
                  {" "}
                  · Được duyệt:{" "}
                  <strong>{formatVnd(item.approvedAmount)}</strong>
                </>
              )}
            </p>
            <p className="text-slate-600">
              {refundBankName(item.recipientBankBin)} ·{" "}
              {item.maskedRecipientAccountNumber} ·{" "}
              {item.recipientAccountName ?? "Chưa có tên tài khoản"}
            </p>
            <p>Lý do: {item.reason}</p>
            {item.reviewNote && <p>Ý kiến Admin: {item.reviewNote}</p>}
            <p className="text-xs text-slate-500">
              Gửi {formatDateTime(item.createdAtUtc)}
              {item.reviewedAtUtc && (
                <> · Xét duyệt {formatDateTime(item.reviewedAtUtc)}</>
              )}
              {item.refundedAtUtc && (
                <> · Hoàn tất {formatDateTime(item.refundedAtUtc)}</>
              )}
            </p>
            {item.status === "Approved" && (
              <p className="text-amber-800">
                Đã được duyệt, chưa phải xác nhận đã chuyển tiền.
              </p>
            )}
            {item.status === "Refunding" && (
              <p className="text-amber-800">
                Đang chuyển và xác nhận kết quả. Phòng vẫn được giữ cho đến khi
                hoàn tất.
              </p>
            )}
            {item.status === "RequiresReview" && (
              <p className="text-amber-800">
                Admin đang kiểm tra kết quả chuyển tiền. Không gửi thêm đơn cho
                khoản cọc này.
              </p>
            )}
            {item.status === "Refunded" && (
              <p className="text-emerald-700">
                Đã ghi nhận hoàn cọc và hủy booking.
              </p>
            )}
          </article>
        )
      })}
      {!history.loading && !history.error && !history.data?.length && (
        <p className="text-sm text-slate-500">Chưa có yêu cầu hoàn cọc.</p>
      )}
      {user?.role === "CUSTOMER" &&
        accessToken &&
        history.data &&
        !history.error &&
        booking.status === "Deposited" &&
        booking.contractId === null &&
        booking.canRequestRefund === true &&
        (!latest || latest.status === "Rejected") && (
          <RefundForm
            key={`${accessToken}:${booking.bookingId}:${latest?.bookingRefundRequestId ?? 0}`}
            booking={booking}
            accessToken={accessToken}
            onSaved={() => {
              history.refresh()
              onChanged()
            }}
          />
        )}
      {user?.role === "CUSTOMER" &&
        !history.loading &&
        !history.error &&
        !latest &&
        booking.status === "Deposited" &&
        !booking.canRequestRefund && (
          <p className="text-sm text-slate-600">
            Khoản cọc này chưa đủ điều kiện gửi yêu cầu hoàn trực tuyến. Vui
            lòng liên hệ quản lý để kiểm tra.
          </p>
        )}
    </section>
  )
}

function RefundForm({
  booking,
  accessToken,
  onSaved,
}: {
  booking: BookingDto
  accessToken: string
  onSaved: () => void
}) {
  const [input, setInput] = useState<CreateBookingRefundDto>({
    reason: "",
    recipientBankBin: "",
    recipientAccountNumber: "",
    recipientAccountName: "",
  })
  const [error, setError] = useState("")
  const [busy, setBusy] = useState(false)
  const [otherBank, setOtherBank] = useState(false)
  const [uncertain, setUncertain] = useState(false)
  const lock = useRef(false)
  const mounted = useRef(true)
  useEffect(() => {
    mounted.current = true
    return () => {
      mounted.current = false
    }
  }, [])
  const submit = async (event: React.FormEvent) => {
    event.preventDefault()
    if (lock.current) return
    const value = Object.fromEntries(
      Object.entries(input).map(([key, item]) => [key, item.trim()]),
    ) as unknown as CreateBookingRefundDto
    const validation = validateRefund(value)
    if (validation) {
      setError(validation)
      return
    }
    lock.current = true
    setBusy(true)
    setError("")
    try {
      await createBookingRefund(accessToken, booking.bookingId, value)
      if (mounted.current) {
        setInput({
          reason: "",
          recipientBankBin: "",
          recipientAccountNumber: "",
          recipientAccountName: "",
        })
        onSaved()
      }
    } catch (failure) {
      if (mounted.current) {
        setError(refundError(failure))
        if (!(failure instanceof ApiError && failure.status === 400))
          setUncertain(true)
      }
    } finally {
      lock.current = false
      if (mounted.current) setBusy(false)
    }
  }
  return (
    <form onSubmit={submit} className="space-y-3">
      <p className="text-sm text-slate-600">
        Khoản cọc: <strong>{formatVnd(booking.depositAmount)}</strong>. Admin sẽ
        xét duyệt số tiền hoàn. Booking chỉ hủy khi hoàn tiền được xác nhận.
      </p>
      <label className="block text-sm font-medium text-slate-700">
        Lý do xin hoàn
        <input
          required
          maxLength={2000}
          value={input.reason}
          onChange={(event) =>
            setInput({ ...input, reason: event.target.value })
          }
          className={field}
        />
      </label>
      <div className="grid gap-3 sm:grid-cols-2">
        <label className="block text-sm font-medium text-slate-700">
          Ngân hàng nhận
          <select required value={otherBank ? "other" : input.recipientBankBin} className={field} onChange={event => {
            setOtherBank(event.target.value === "other"); setInput({ ...input, recipientBankBin: event.target.value === "other" ? "" : event.target.value })
          }}>
            <option value="">Chọn ngân hàng</option>{refundBanks.map(bank => <option key={bank.bin} value={bank.bin}>{bank.name}</option>)}
            <option value="other">Ngân hàng khác</option>
          </select>
        </label>
        <label className="block text-sm font-medium text-slate-700">
          Số tài khoản nhận
          <input
            required
            inputMode="numeric"
            pattern="[0-9]{1,64}"
            maxLength={64}
            value={input.recipientAccountNumber}
            onChange={(event) =>
              setInput({ ...input, recipientAccountNumber: event.target.value })
            }
            className={field}
          />
        </label>
      </div>
      {otherBank && <label className="block text-sm font-medium text-slate-700">Mã ngân hàng nhận (6 chữ số)
        <input required inputMode="numeric" pattern="[0-9]{6}" maxLength={6} value={input.recipientBankBin} className={field}
          onChange={event => setInput({ ...input, recipientBankBin: event.target.value })} />
        <span className="mt-1 block text-xs font-normal text-slate-500">Dùng mã ngân hàng của tài khoản nhận; liên hệ quản lý nếu bạn chưa xác định được mã.</span>
      </label>}
      <label className="block text-sm font-medium text-slate-700">
        Tên chủ tài khoản
        <input
          required
          maxLength={256}
          autoComplete="off"
          value={input.recipientAccountName}
          onChange={(event) =>
            setInput({ ...input, recipientAccountName: event.target.value })
          }
          className={field}
        />
      </label>
      <p className="text-xs text-slate-500">
        Kiểm tra chính xác ngân hàng, số và tên chủ tài khoản trước khi gửi.
        Thông tin này dùng để Admin đối chiếu khoản hoàn.
      </p>
      {error && (
        <p role="alert" className="text-sm text-rose-700">
          {error}
        </p>
      )}
      {uncertain && (
        <p className="text-xs text-amber-800">
          Bấm cập nhật hoàn cọc để kiểm tra kết quả trước khi gửi tiếp.
        </p>
      )}
      <button
        type="submit"
        disabled={busy || uncertain}
        className="rounded-lg bg-[#087775] px-4 py-2.5 text-sm font-bold text-white disabled:opacity-60"
      >
        {busy ? "Đang gửi yêu cầu..." : "Gửi yêu cầu hoàn cọc"}
      </button>
    </form>
  )
}
