import { useCallback, useEffect, useRef, useState, type FormEvent } from "react"
import { ApiError } from "@/api/apiClient"
import { useAuth } from "@/auth/AuthContext"
import StatusBadge from "@/components/customer/StatusBadge"
import { createBookingRefund, getBookingRefunds } from "@/services/bookingRefundService"
import type { BookingDto } from "@/types/booking"
import type { BookingRefundDto } from "@/types/bookingRefund"
import { formatDateTime } from "@/utils/bookingDisplay"
import { bankName, bookingRefundStatusDisplay, popularBanks, refundErrorMessage } from "@/utils/bookingRefundDisplay"
import { formatVnd } from "@/utils/publicFormatting"

type Props = {
  booking: BookingDto
  onClose: () => void
  // Gọi sau khi gửi đơn hoặc cập nhật trạng thái để trang tải lại danh sách booking từ BE.
  onChanged: () => void
}

const OTHER_BANK = "other"
const maxReasonLength = 2000
const inputClass =
  "mt-2 h-11 w-full rounded-xl border border-slate-200 bg-white px-3.5 text-[14px] text-slate-700 outline-none focus:border-[#087775] focus:ring-2 focus:ring-[#087775]/15"

// Lỗi mạng (không có mã HTTP) nghĩa là không biết đơn đã được tạo hay chưa: không được tự gửi lại.
function isUncertain(error: unknown) {
  return !(error instanceof ApiError) || error.status === undefined
}

export default function BookingRefundModal({ booking, onClose, onChanged }: Props) {
  const { accessToken } = useAuth()
  const [history, setHistory] = useState<BookingRefundDto[] | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [reason, setReason] = useState("")
  const [bank, setBank] = useState("")
  const [otherBin, setOtherBin] = useState("")
  // Số tài khoản giữ dạng chuỗi để không mất số 0 ở đầu.
  const [accountNumber, setAccountNumber] = useState("")
  const [accountName, setAccountName] = useState("")
  const [formError, setFormError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [uncertain, setUncertain] = useState(false)
  const locked = useRef(false)
  const loadController = useRef<AbortController | null>(null)

  const loadHistory = useCallback(async () => {
    if (!accessToken) return
    loadController.current?.abort()
    const controller = new AbortController()
    loadController.current = controller
    setLoading(true)
    setLoadError(null)
    try {
      const items = await getBookingRefunds(accessToken, booking.bookingId, controller.signal)
      if (controller.signal.aborted) return
      setHistory(items)
      setUncertain(false)
    } catch (error) {
      if (!controller.signal.aborted) setLoadError(refundErrorMessage(error))
    } finally {
      if (!controller.signal.aborted) setLoading(false)
    }
  }, [accessToken, booking.bookingId])

  useEffect(() => {
    void loadHistory()
    return () => loadController.current?.abort()
  }, [loadHistory])

  // Chỉ cho gửi đơn mới khi BE xác nhận đủ điều kiện và đơn gần nhất (nếu có) đã bị từ chối.
  const latest = history?.[0] ?? null
  const canSubmit = Boolean(booking.canRequestRefund) && history !== null && (latest === null || latest.status === "Rejected")

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!accessToken || locked.current || uncertain) return
    const bin = bank === OTHER_BANK ? otherBin.trim() : bank
    const trimmedReason = reason.trim()
    const trimmedAccount = accountNumber.trim()
    const trimmedName = accountName.trim()
    if (!trimmedReason) return setFormError("Vui lòng nhập lý do xin hoàn cọc.")
    if (!/^\d{6}$/.test(bin)) return setFormError("Vui lòng chọn ngân hàng hoặc nhập mã ngân hàng gồm 6 chữ số.")
    if (!/^\d{6,30}$/.test(trimmedAccount)) return setFormError("Số tài khoản chỉ gồm chữ số (6–30 số).")
    if (!trimmedName) return setFormError("Vui lòng nhập tên chủ tài khoản.")

    locked.current = true
    setSubmitting(true)
    setFormError(null)
    try {
      await createBookingRefund(accessToken, booking.bookingId, {
        reason: trimmedReason,
        recipientBankBin: bin,
        recipientAccountNumber: trimmedAccount,
        recipientAccountName: trimmedName,
      })
      setReason("")
      setAccountNumber("")
      setAccountName("")
      await loadHistory()
      onChanged()
    } catch (error) {
      if (isUncertain(error)) {
        setUncertain(true)
        setFormError("Chưa nhận được phản hồi từ máy chủ nên chưa biết yêu cầu đã được gửi hay chưa. Hãy bấm \"Cập nhật trạng thái\" trước khi gửi lại.")
      } else {
        setFormError(refundErrorMessage(error))
        if (error instanceof ApiError && error.status === 409) {
          await loadHistory()
          onChanged()
        }
      }
    } finally {
      locked.current = false
      setSubmitting(false)
    }
  }

  return (
    <div
      className="fixed inset-0 z-[70] grid place-items-center bg-slate-950/45 p-5"
      role="presentation"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget && !submitting) onClose()
      }}
    >
      <section
        role="dialog"
        aria-modal="true"
        aria-labelledby="booking-refund-title"
        className="max-h-[92vh] w-full max-w-[560px] overflow-y-auto rounded-2xl bg-white p-7 shadow-2xl"
      >
        <div className="flex items-start justify-between gap-5">
          <div>
            <p className="text-[11px] font-bold uppercase tracking-[.16em] text-[#087775]">
              Booking #{booking.bookingId} · Phòng {booking.roomCode}
            </p>
            <h2 id="booking-refund-title" className="mt-2 font-display text-[24px] font-bold tracking-[-.04em] text-slate-900">
              Hoàn cọc giữ phòng
            </h2>
            <p className="mt-2 text-[13px] leading-5 text-slate-500">
              Tiền cọc đã thanh toán: <strong className="text-[#087775]">{formatVnd(booking.depositAmount)}</strong>.
              Số tiền hoàn do Admin xét duyệt dựa trên khoản cọc thực nhận.
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            disabled={submitting}
            aria-label="Đóng"
            className="grid h-8 w-8 shrink-0 place-items-center rounded-full bg-slate-50 text-lg text-slate-500 hover:bg-slate-100 disabled:opacity-50"
          >
            ×
          </button>
        </div>

        <div className="mt-6">
          <div className="flex items-center justify-between gap-3">
            <h3 className="text-sm font-bold text-slate-800">Lịch sử yêu cầu</h3>
            <button
              type="button"
              onClick={() => void loadHistory().then(onChanged)}
              disabled={loading || submitting}
              className="text-[13px] font-semibold text-[#087775] hover:underline disabled:opacity-50"
            >
              {loading ? "Đang tải..." : "Cập nhật trạng thái"}
            </button>
          </div>
          {loadError && <p role="alert" className="mt-3 text-sm text-rose-600">{loadError}</p>}
          {!loadError && history?.length === 0 && (
            <p className="mt-3 text-[13px] text-slate-500">Bạn chưa gửi yêu cầu hoàn cọc nào cho booking này.</p>
          )}
          {history && history.length > 0 && (
            <ol className="mt-3 space-y-3">
              {history.map((item) => <RefundHistoryItem key={item.bookingRefundRequestId} item={item} />)}
            </ol>
          )}
        </div>

        {canSubmit && (
          <form onSubmit={submit} noValidate className="mt-6 space-y-4 border-t border-slate-100 pt-6">
            <h3 className="text-sm font-bold text-slate-800">{latest ? "Gửi lại yêu cầu hoàn cọc" : "Gửi yêu cầu hoàn cọc"}</h3>
            <label className="block text-[13px] font-semibold text-slate-700">
              Lý do
              <textarea
                value={reason}
                maxLength={maxReasonLength}
                rows={3}
                onChange={(event) => { setReason(event.target.value); setFormError(null) }}
                placeholder="Ví dụ: Tôi đổi kế hoạch, không thuê phòng này nữa."
                className="mt-2 w-full resize-none rounded-xl border border-slate-200 bg-white px-3.5 py-3 text-[14px] leading-5 text-slate-700 outline-none placeholder:text-slate-400 focus:border-[#087775] focus:ring-2 focus:ring-[#087775]/15"
              />
            </label>
            <label className="block text-[13px] font-semibold text-slate-700">
              Ngân hàng nhận
              <select value={bank} onChange={(event) => { setBank(event.target.value); setFormError(null) }} className={inputClass}>
                <option value="" disabled>Chọn ngân hàng</option>
                {popularBanks.map((item) => <option key={item.bin} value={item.bin}>{item.name}</option>)}
                <option value={OTHER_BANK}>Ngân hàng khác (nhập mã)</option>
              </select>
            </label>
            {bank === OTHER_BANK && (
              <label className="block text-[13px] font-semibold text-slate-700">
                Mã ngân hàng (BIN, 6 chữ số)
                <input
                  value={otherBin}
                  inputMode="numeric"
                  maxLength={6}
                  onChange={(event) => { setOtherBin(event.target.value.replace(/\D/g, "")); setFormError(null) }}
                  className={inputClass}
                />
              </label>
            )}
            <label className="block text-[13px] font-semibold text-slate-700">
              Số tài khoản
              <input
                value={accountNumber}
                inputMode="numeric"
                autoComplete="off"
                maxLength={30}
                onChange={(event) => { setAccountNumber(event.target.value.replace(/\D/g, "")); setFormError(null) }}
                className={inputClass}
              />
            </label>
            <label className="block text-[13px] font-semibold text-slate-700">
              Tên chủ tài khoản
              <input
                value={accountName}
                maxLength={256}
                autoComplete="off"
                onChange={(event) => { setAccountName(event.target.value.toUpperCase()); setFormError(null) }}
                placeholder="NGUYEN VAN A"
                className={inputClass}
              />
            </label>
            {formError && <p role="alert" className="text-sm text-rose-600">{formError}</p>}
            <p className="text-[12px] leading-5 text-slate-500">
              Sau khi gửi, booking sẽ không thể lập hợp đồng cho đến khi yêu cầu bị từ chối. Tiền chỉ được chuyển sau khi Admin duyệt.
            </p>
            <div className="grid grid-cols-2 gap-3">
              <button type="button" onClick={onClose} disabled={submitting} className="rounded-lg bg-slate-100 py-3 text-[13px] font-bold text-slate-600 hover:bg-slate-200 disabled:opacity-50">
                Đóng
              </button>
              <button type="submit" disabled={submitting || uncertain} className="rounded-lg bg-[#087775] py-3 text-[13px] font-bold text-white shadow-sm hover:bg-[#066a68] disabled:opacity-60">
                {submitting ? "Đang gửi..." : "Gửi yêu cầu"}
              </button>
            </div>
          </form>
        )}
        {!canSubmit && formError && <p role="alert" className="mt-4 text-sm text-rose-600">{formError}</p>}
      </section>
    </div>
  )
}

function RefundHistoryItem({ item }: { item: BookingRefundDto }) {
  const display = bookingRefundStatusDisplay(item.status)
  return (
    <li className="rounded-xl border border-slate-100 p-4 text-[13px] text-slate-600">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <span className="font-semibold text-slate-800">Yêu cầu #{item.bookingRefundRequestId}</span>
        <StatusBadge tone={display.tone}>{display.label}</StatusBadge>
      </div>
      <dl className="mt-3 grid gap-x-4 gap-y-1.5 sm:grid-cols-2">
        <div><dt className="inline text-slate-400">Yêu cầu: </dt><dd className="inline font-semibold">{formatVnd(item.requestedAmount)}</dd></div>
        {item.approvedAmount !== null && (
          <div><dt className="inline text-slate-400">Được duyệt: </dt><dd className="inline font-semibold text-[#087775]">{formatVnd(item.approvedAmount)}</dd></div>
        )}
        <div className="sm:col-span-2">
          <dt className="inline text-slate-400">Tài khoản nhận: </dt>
          <dd className="inline">{bankName(item.recipientBankBin)} · {item.maskedRecipientAccountNumber}{item.recipientAccountName ? ` · ${item.recipientAccountName}` : ""}</dd>
        </div>
        <div><dt className="inline text-slate-400">Gửi lúc: </dt><dd className="inline">{formatDateTime(item.createdAtUtc)}</dd></div>
        {item.reviewedAtUtc && <div><dt className="inline text-slate-400">Xét duyệt: </dt><dd className="inline">{formatDateTime(item.reviewedAtUtc)}</dd></div>}
        {item.refundedAtUtc && <div><dt className="inline text-slate-400">Hoàn tất: </dt><dd className="inline">{formatDateTime(item.refundedAtUtc)}</dd></div>}
      </dl>
      <p className="mt-2 whitespace-pre-wrap"><span className="text-slate-400">Lý do: </span>{item.reason}</p>
      {item.reviewNote && (
        <p className="mt-1 whitespace-pre-wrap"><span className="text-slate-400">{item.status === "Rejected" ? "Lý do từ chối: " : "Ghi chú Admin: "}</span>{item.reviewNote}</p>
      )}
      {item.status === "RequiresReview" && (
        <p className="mt-2 text-amber-700">Khoản hoàn cần kiểm tra thêm. Vui lòng không gửi yêu cầu mới; quản lý sẽ liên hệ bạn.</p>
      )}
    </li>
  )
}
