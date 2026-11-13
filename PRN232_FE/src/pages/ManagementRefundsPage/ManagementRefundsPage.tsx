import { useCallback, useEffect, useRef, useState } from "react"
import { useAuth } from "@/auth/AuthContext"
import ManagementPage from "@/components/management/ManagementPage"
import StatusBadge from "@/components/customer/StatusBadge"
import { getRefundReview, getRefundReviews, reviewRefund } from "@/services/bookingRefundService"
import type { BookingRefundReviewDto, BookingRefundStatus } from "@/types/bookingRefund"
import { bankName, bookingRefundStatuses, bookingRefundStatusDisplay, refundErrorMessage } from "@/utils/bookingRefundDisplay"
import { formatVnd } from "@/utils/publicFormatting"

const pageSize = 20
const controlClass = "rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm focus:border-[#087775] focus:outline-none focus:ring-2 focus:ring-teal-100 disabled:opacity-50"
const buttonClass = "min-h-11 rounded-lg border border-slate-200 px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50"

function dateLabel(value: string) {
  return new Date(value).toLocaleString("vi-VN")
}

function RefundStatus({ status }: { status: BookingRefundStatus }) {
  const display = bookingRefundStatusDisplay(status)
  return <StatusBadge tone={display.tone}>{display.label}</StatusBadge>
}

type RefundRow = Pick<BookingRefundReviewDto, "bookingRefundRequestId" | "bookingId" | "requestedAmount" | "recipientBankBin" | "createdAtUtc" | "status"> & { maskedAccount: string }

function RefundDetails({ accessToken, requestId, onClose, onReviewStart, onReload, onSuccess }: {
  accessToken: string
  requestId: number
  onClose: () => void
  onReviewStart: () => void
  onReload: () => void
  onSuccess: (message: string) => void
}) {
  const [detail, setDetail] = useState<BookingRefundReviewDto | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState("")
  const [sending, setSending] = useState(false)
  const [paused, setPaused] = useState(false)
  const request = useRef<AbortController | null>(null)
  const submitting = useRef(false)
  const mounted = useRef(false)

  const load = useCallback(async (hideForm: boolean) => {
    request.current?.abort()
    const controller = new AbortController()
    request.current = controller
    if (hideForm) {
      setLoading(true)
      setDetail(null)
    }
    const timeout = window.setTimeout(() => {
      controller.abort()
      if (mounted.current) {
        setDetail(null)
        setError("Tải chi tiết quá lâu. Vui lòng cập nhật lại.")
        setLoading(false)
      }
    }, 15_000)
    controller.signal.addEventListener("abort", () => window.clearTimeout(timeout), { once: true })
    try {
      const result = await getRefundReview(accessToken, requestId, controller.signal)
      if (!controller.signal.aborted && mounted.current) setDetail(result)
    } catch (cause) {
      if (!controller.signal.aborted && mounted.current) {
        setDetail(null)
        setError(refundErrorMessage(cause))
      }
    } finally {
      window.clearTimeout(timeout)
      if (!controller.signal.aborted && mounted.current) setLoading(false)
    }
  }, [accessToken, requestId])

  useEffect(() => {
    mounted.current = true
    void load(true)
    let polls = 0
    const timer = window.setInterval(() => {
      if (submitting.current) return
      polls += 1
      void load(false)
      if (polls === 20) {
        window.clearInterval(timer)
        setPaused(true)
      }
    }, 15_000)
    return () => {
      mounted.current = false
      request.current?.abort()
      window.clearInterval(timer)
    }
  }, [load])

  async function submit(approve: boolean, approvedAmount: number | null, reviewNote: string) {
    if (submitting.current || detail?.status !== "PendingReview") return
    submitting.current = true
    setSending(true)
    request.current?.abort()
    onReviewStart()
    setError("")
    try {
      const updated = await reviewRefund(accessToken, requestId, { approve, approvedAmount, reviewNote })
      if (!mounted.current) return
      setDetail(updated)
      onSuccess(approve ? `Đã duyệt yêu cầu #${requestId}.` : `Đã từ chối yêu cầu #${requestId}.`)
    } catch (cause) {
      if (!mounted.current) return
      setError(refundErrorMessage(cause))
      // A failed POST may already have been recorded. Read again before allowing another decision.
      await load(true)
    } finally {
      submitting.current = false
      if (mounted.current) {
        setSending(false)
        onReload()
      }
    }
  }

  return (
    <section aria-label="Chi tiết yêu cầu hoàn cọc" aria-busy={loading || sending} className="mb-6 rounded-xl border border-slate-200 bg-white p-5">
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <h3 className="text-lg font-bold text-slate-900">Yêu cầu hoàn cọc #{requestId}</h3>
        <div className="flex gap-2">
          <button type="button" className={buttonClass} disabled={loading || sending} onClick={() => { setError(""); void load(true) }}>Cập nhật chi tiết</button>
          <button type="button" className={buttonClass} disabled={sending} onClick={onClose}>Đóng chi tiết</button>
        </div>
      </div>
      {error && <p role="alert" className="mb-4 rounded-lg bg-rose-50 p-3 text-sm text-rose-700">{error}</p>}
      {loading && <p role="status" className="py-4 text-sm text-slate-500">Đang tải chi tiết...</p>}
      {paused && <p className="mb-4 text-sm text-slate-600">Đã tạm dừng cập nhật tự động sau 20 lượt. Bạn có thể cập nhật chi tiết thủ công.</p>}
      {detail && !loading && <>
        <div className="mb-4 flex flex-wrap items-center gap-3 text-sm text-slate-600">
          <span>Booking #{detail.bookingId}</span>
          <span>Khoản thu gốc #{detail.originalPaymentId}</span>
          <RefundStatus status={detail.status} />
        </div>
        <dl className="grid gap-x-8 gap-y-4 text-sm sm:grid-cols-2">
          <div><dt className="text-slate-500">Cọc thực nhận</dt><dd className="mt-1 font-semibold tabular-nums text-slate-900">{formatVnd(detail.depositAmount)}</dd></div>
          <div><dt className="text-slate-500">Số tiền yêu cầu</dt><dd className="mt-1 font-semibold tabular-nums text-slate-900">{formatVnd(detail.requestedAmount)}</dd></div>
          <div className="sm:col-span-2"><dt className="text-slate-500">Lý do hoàn cọc</dt><dd className="mt-1 whitespace-pre-wrap break-words text-slate-900">{detail.reason}</dd></div>
          <div><dt className="text-slate-500">Ngân hàng nhận (BIN)</dt><dd className="mt-1 text-slate-900">{bankName(detail.recipientBankBin)} · {detail.recipientBankBin}</dd></div>
          <div><dt className="text-slate-500">Số tài khoản đầy đủ</dt><dd className="mt-1 break-all font-semibold tabular-nums text-slate-900">{detail.recipientAccountNumber}</dd></div>
          <div className="sm:col-span-2"><dt className="text-slate-500">Tên chủ tài khoản</dt><dd className="mt-1 text-slate-900">{detail.recipientAccountName || "Chưa cung cấp"}</dd><dd className="mt-1 text-xs text-amber-800">Tên do khách cung cấp, chưa được ngân hàng xác minh</dd></div>
          <div><dt className="text-slate-500">Ngày gửi</dt><dd className="mt-1 text-slate-900">{dateLabel(detail.createdAtUtc)}</dd></div>
          {detail.approvedAmount !== null && <div><dt className="text-slate-500">Số tiền được duyệt</dt><dd className="mt-1 tabular-nums text-slate-900">{formatVnd(detail.approvedAmount)}</dd></div>}
          {detail.reviewNote && <div className="sm:col-span-2"><dt className="text-slate-500">Ghi chú xét duyệt</dt><dd className="mt-1 whitespace-pre-wrap break-words text-slate-900">{detail.reviewNote}</dd></div>}
        </dl>
        {detail.status === "PendingReview" && <ReviewForm requestedAmount={detail.requestedAmount} sending={sending} onSubmit={submit} />}
      </>}
    </section>
  )
}

function ReviewForm({ requestedAmount, sending, onSubmit }: {
  requestedAmount: number
  sending: boolean
  onSubmit: (approve: boolean, amount: number | null, note: string) => Promise<void>
}) {
  const [amount, setAmount] = useState(String(requestedAmount))
  const [note, setNote] = useState("")
  const [confirmed, setConfirmed] = useState(false)
  const [error, setError] = useState("")

  function decide(approve: boolean) {
    if (sending) return
    const value = Number(amount)
    if (!note.trim() || note.length > 2000) {
      setError("Vui lòng nhập ghi chú xét duyệt, tối đa 2000 ký tự.")
      return
    }
    if (approve && (!Number.isSafeInteger(value) || value <= 0 || value > requestedAmount)) {
      setError("Số tiền duyệt phải là số nguyên dương, không vượt số tiền yêu cầu.")
      return
    }
    if (approve && !confirmed) {
      setError("Vui lòng xác nhận đã đối chiếu khoản cọc và thông tin tài khoản.")
      return
    }
    setError("")
    void onSubmit(approve, approve ? value : null, note.trim())
  }

  return (
    <form onSubmit={(event) => { event.preventDefault(); decide(true) }} noValidate className="mt-5 border-t border-slate-100 pt-5">
      <h4 className="mb-4 font-bold text-slate-900">Xét duyệt yêu cầu</h4>
      <fieldset disabled={sending} className="space-y-4">
        <div>
          <label htmlFor="refund-amount" className="mb-1 block text-sm font-semibold text-slate-700">Số tiền duyệt (VND)</label>
          <input id="refund-amount" type="number" min="1" max={requestedAmount} step="1" value={amount} onChange={(event) => setAmount(event.target.value)} className={`${controlClass} w-full sm:max-w-xs`} aria-describedby="refund-amount-hint" />
          <p id="refund-amount-hint" className="mt-1 text-xs text-slate-500">Số nguyên dương, tối đa {formatVnd(requestedAmount)}.</p>
        </div>
        <div>
          <label htmlFor="refund-note" className="mb-1 block text-sm font-semibold text-slate-700">Ghi chú xét duyệt (bắt buộc)</label>
          <textarea id="refund-note" required maxLength={2000} rows={3} value={note} onChange={(event) => setNote(event.target.value)} className={`${controlClass} w-full`} aria-describedby="refund-note-hint" />
          <p id="refund-note-hint" className="mt-1 text-xs text-slate-500">Tối đa 2000 ký tự.</p>
        </div>
        <label className="flex min-h-11 items-center gap-3 text-sm text-slate-700">
          <input type="checkbox" checked={confirmed} onChange={(event) => setConfirmed(event.target.checked)} className="h-4 w-4 shrink-0 accent-[#087775]" />
          Tôi đã đối chiếu khoản cọc và thông tin tài khoản
        </label>
        {error && <p role="alert" className="text-sm text-rose-700">{error}</p>}
        <div className="flex flex-wrap gap-3">
          <button type="submit" className="min-h-11 rounded-lg bg-[#087775] px-5 py-2 text-sm font-bold text-white disabled:opacity-50">{sending ? "Đang gửi..." : "Duyệt"}</button>
          <button type="button" onClick={() => decide(false)} className={`${buttonClass} text-rose-700`}>Từ chối</button>
        </div>
      </fieldset>
      <p className="mt-4 text-sm text-slate-600">Duyệt không có nghĩa là đã chuyển tiền. Hệ thống sẽ chuyển tiền khi kênh chi payOS được bật.</p>
    </form>
  )
}

function RefundQueue({ accessToken }: { accessToken: string }) {
  const [status, setStatus] = useState<BookingRefundStatus | "">("PendingReview")
  const [page, setPage] = useState(1)
  const [revision, setRevision] = useState(0)
  const [rows, setRows] = useState<RefundRow[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState("")
  const [success, setSuccess] = useState("")
  const [selected, setSelected] = useState<number | null>(null)
  const [reviewing, setReviewing] = useState(false)
  const request = useRef<AbortController | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    request.current = controller
    setLoading(true)
    setRows([])
    setError("")
    const timeout = window.setTimeout(() => {
      controller.abort()
      setError("Tải danh sách quá lâu. Vui lòng thử lại.")
      setLoading(false)
    }, 15_000)
    controller.signal.addEventListener("abort", () => window.clearTimeout(timeout), { once: true })
    void getRefundReviews(accessToken, { status: status || undefined, page, pageSize }, controller.signal)
      .then((result) => {
        if (!controller.signal.aborted) setRows(result.map((row) => ({
          bookingRefundRequestId: row.bookingRefundRequestId,
          bookingId: row.bookingId,
          requestedAmount: row.requestedAmount,
          recipientBankBin: row.recipientBankBin,
          createdAtUtc: row.createdAtUtc,
          status: row.status,
          maskedAccount: `****${row.recipientAccountNumber.slice(-4)}`,
        })))
      })
      .catch((cause: unknown) => {
        if (!controller.signal.aborted) setError(refundErrorMessage(cause))
      })
      .finally(() => {
        window.clearTimeout(timeout)
        if (!controller.signal.aborted) setLoading(false)
      })
    return () => { controller.abort(); window.clearTimeout(timeout) }
  }, [accessToken, status, page, revision])

  function changePage(nextPage: number) {
    request.current?.abort()
    setSelected(null)
    setSuccess("")
    setPage(nextPage)
  }

  return <>
    {success && <p role="status" className="mb-4 rounded-lg bg-emerald-50 p-3 text-sm text-emerald-800">{success}</p>}
    <div className="mb-5 flex flex-wrap items-end justify-between gap-3">
      <label className="text-sm font-semibold text-slate-700">Trạng thái
        <select value={status} disabled={reviewing} className={`${controlClass} mt-1 block min-h-11 w-full sm:w-72`} onChange={(event) => {
          request.current?.abort()
          setSelected(null)
          setSuccess("")
          setStatus(event.target.value as BookingRefundStatus | "")
          setPage(1)
        }}>
          <option value="">Tất cả</option>
          {bookingRefundStatuses.map((value) => <option key={value} value={value}>{bookingRefundStatusDisplay(value).label}</option>)}
        </select>
      </label>
      <button type="button" className={buttonClass} disabled={loading || reviewing} onClick={() => setRevision((value) => value + 1)}>Tải lại danh sách</button>
    </div>
    {selected !== null && <RefundDetails key={selected} accessToken={accessToken} requestId={selected}
      onClose={() => setSelected(null)}
      onReviewStart={() => { request.current?.abort(); setReviewing(true); setSuccess("") }}
      onReload={() => { setReviewing(false); setRevision((value) => value + 1) }}
      onSuccess={setSuccess} />}
    <div className="overflow-hidden rounded-xl border border-slate-200 bg-white" aria-busy={loading}>
      <div className="hidden grid-cols-[.5fr_.8fr_1fr_1fr_1fr_1.1fr_1.4fr] gap-3 border-b border-slate-100 px-5 py-3 text-xs text-slate-500 xl:grid" aria-hidden="true">
        <span>Yêu cầu</span><span>Booking</span><span className="text-right">Số tiền yêu cầu</span><span>Ngân hàng</span><span>Số tài khoản</span><span>Ngày gửi</span><span>Trạng thái</span>
      </div>
      {error ? <div className="p-5"><p role="alert" className="text-sm text-rose-700">{error}</p><button type="button" className={`${buttonClass} mt-3`} onClick={() => setRevision((value) => value + 1)}>Thử lại</button></div>
        : loading ? <p role="status" className="p-5 text-sm text-slate-500">Đang tải yêu cầu hoàn cọc...</p>
        : rows.length === 0 ? <p className="p-5 text-sm text-slate-500">Không có yêu cầu hoàn cọc trong bộ lọc này.</p>
        : <ul className="divide-y divide-slate-100">{rows.map((row) => <li key={row.bookingRefundRequestId}>
          <button type="button" disabled={reviewing} onClick={() => { setSuccess(""); setSelected(row.bookingRefundRequestId) }} aria-label={`Xem yêu cầu #${row.bookingRefundRequestId}`} aria-pressed={selected === row.bookingRefundRequestId}
            className={`grid w-full gap-3 px-5 py-4 text-left text-sm text-slate-700 hover:bg-slate-50 focus-visible:outline-2 focus-visible:outline-[#087775] disabled:opacity-50 sm:grid-cols-2 xl:grid-cols-[.5fr_.8fr_1fr_1fr_1fr_1.1fr_1.4fr] xl:items-center ${selected === row.bookingRefundRequestId ? "bg-teal-50/50" : ""}`}>
            <span className="font-semibold text-[#087775]">#{row.bookingRefundRequestId}</span>
            <span>Booking #{row.bookingId}</span>
            <span className="font-semibold tabular-nums xl:text-right">{formatVnd(row.requestedAmount)}</span>
            <span>{bankName(row.recipientBankBin)}</span><span className="tabular-nums">{row.maskedAccount}</span>
            <span className="text-xs">{dateLabel(row.createdAtUtc)}</span><span><RefundStatus status={row.status} /></span>
          </button>
        </li>)}</ul>}
    </div>
    <div className="mt-4 flex items-center justify-end gap-3 text-sm text-slate-600">
      <button type="button" className={buttonClass} disabled={loading || reviewing || page === 1} onClick={() => changePage(page - 1)}>Trước</button>
      <span>Trang {page}</span>
      <button type="button" className={buttonClass} disabled={loading || reviewing || !!error || rows.length < pageSize} onClick={() => changePage(page + 1)}>Sau</button>
    </div>
  </>
}

export default function ManagementRefundsPage() {
  const { accessToken, user } = useAuth()
  return <ManagementPage title="Xét duyệt hoàn cọc" description="Đối chiếu khoản cọc và tài khoản nhận trước khi ghi quyết định xét duyệt.">
    {accessToken && user?.role === "ADMIN"
      ? <RefundQueue key={`${user.id}:${accessToken}`} accessToken={accessToken} />
      : <p role="alert" className="text-sm text-rose-700">Bạn không có quyền xem yêu cầu hoàn cọc.</p>}
  </ManagementPage>
}
