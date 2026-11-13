import { useCallback, useEffect, useRef, useState } from "react"
import { ApiError } from "@/api/apiClient"
import { useAuth } from "@/auth/AuthContext"
import ManagementPage from "@/components/management/ManagementPage"
import StatusBadge from "@/components/customer/StatusBadge"
import { getPaymentReview, getPaymentReviews, resolvePaymentReview } from "@/services/paymentReviewService"
import type { PaymentReviewDto, PaymentReviewResolution, PaymentReviewStatus, ResolvePaymentReviewInput } from "@/types/paymentReview"
import { formatVnd } from "@/utils/publicFormatting"

const pageSize = 20
const controlClass = "rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm focus:border-[#087775] focus:outline-none focus:ring-2 focus:ring-teal-100 disabled:opacity-50"
const buttonClass = "min-h-11 rounded-lg border border-slate-200 px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50"
const resolutionLabels: Record<PaymentReviewResolution, string> = {
  RefundedExternally: "Đã hoàn tiền ngoài hệ thống",
  NotDuplicate: "Không phải trả trùng",
}

function dateLabel(value: string | null) {
  return value ? new Date(value).toLocaleString("vi-VN") : "Chưa có"
}

function paymentReviewErrorMessage(error: unknown) {
  if (error instanceof ApiError) {
    if (error.status === 409) return "Mục này vừa được xử lý hoặc đã thay đổi. Vui lòng tải lại."
    if (error.status === 403) return "Bạn không có quyền thực hiện thao tác này."
    if (error.status === 404) return "Không tìm thấy mục đối soát thanh toán."
    if (error.status === 400) return error.message
  }
  return error instanceof Error ? error.message : "Yêu cầu không thành công. Vui lòng thử lại."
}

function ReviewStatus({ status }: { status: PaymentReviewStatus }) {
  return <StatusBadge tone={status === "Open" ? "amber" : "green"}>{status === "Open" ? "Cần xử lý" : "Đã xử lý"}</StatusBadge>
}

function ResolutionForm({ sending, onSubmit }: {
  sending: boolean
  onSubmit: (input: ResolvePaymentReviewInput) => Promise<void>
}) {
  const [resolution, setResolution] = useState<PaymentReviewResolution>("RefundedExternally")
  const [note, setNote] = useState("")
  const [evidence, setEvidence] = useState("")
  const [error, setError] = useState("")

  function submit() {
    if (sending) return
    const trimmedNote = note.trim()
    const trimmedEvidence = evidence.trim()
    if (trimmedNote.length < 5 || trimmedNote.length > 2000) {
      setError("Ghi chú phải dài từ 5 đến 2000 ký tự sau khi bỏ khoảng trắng đầu và cuối.")
      return
    }
    if (evidence.length > 256) {
      setError("Mã chứng từ hoàn tiền không được vượt quá 256 ký tự.")
      return
    }
    if (resolution === "RefundedExternally" && !trimmedEvidence) {
      setError("Vui lòng nhập mã chứng từ khi đã hoàn tiền ngoài hệ thống.")
      return
    }
    setError("")
    void onSubmit({ resolution, note: trimmedNote, evidenceReference: trimmedEvidence || null })
  }

  return (
    <form onSubmit={(event) => { event.preventDefault(); submit() }} noValidate className="mt-5 border-t border-slate-100 pt-5">
      <h4 className="mb-4 font-bold text-slate-900">Kết luận đối soát</h4>
      <fieldset disabled={sending} className="space-y-4">
        <fieldset className="space-y-2">
          <legend className="mb-2 text-sm font-semibold text-slate-700">Kết luận</legend>
          {(Object.keys(resolutionLabels) as PaymentReviewResolution[]).map((value) => (
            <label key={value} className="flex min-h-11 items-center gap-3 text-sm text-slate-700">
              <input type="radio" name="payment-review-resolution" value={value} checked={resolution === value} onChange={() => setResolution(value)} className="h-4 w-4 shrink-0 accent-[#087775]" />
              {resolutionLabels[value]}
            </label>
          ))}
        </fieldset>
        <div>
          <label htmlFor="payment-review-note" className="mb-1 block text-sm font-semibold text-slate-700">Ghi chú (bắt buộc)</label>
          <textarea id="payment-review-note" required rows={3} value={note} onChange={(event) => setNote(event.target.value)} className={`${controlClass} w-full`} aria-describedby="payment-review-note-hint" />
          <p id="payment-review-note-hint" className="mt-1 text-xs text-slate-500">Từ 5 đến 2000 ký tự sau khi bỏ khoảng trắng đầu và cuối.</p>
        </div>
        <div>
          <label htmlFor="payment-review-evidence" className="mb-1 block text-sm font-semibold text-slate-700">Mã chứng từ hoàn tiền</label>
          <input id="payment-review-evidence" required={resolution === "RefundedExternally"} maxLength={256} value={evidence} onChange={(event) => setEvidence(event.target.value)} className={`${controlClass} w-full`} aria-describedby="payment-review-evidence-hint" />
          <p id="payment-review-evidence-hint" className="mt-1 text-xs text-slate-500">Tối đa 256 ký tự. Bắt buộc khi đã hoàn tiền ngoài hệ thống.</p>
        </div>
        {error && <p role="alert" className="text-sm text-rose-700">{error}</p>}
        <button type="submit" disabled={sending} className="min-h-11 rounded-lg bg-[#087775] px-5 py-2 text-sm font-bold text-white disabled:opacity-50">{sending ? "Đang gửi..." : "Xác nhận xử lý"}</button>
      </fieldset>
    </form>
  )
}

function ReviewDetails({ accessToken, reviewId, onClose, onResolveStart, onReload, onSuccess }: {
  accessToken: string
  reviewId: number
  onClose: () => void
  onResolveStart: () => void
  onReload: () => void
  onSuccess: (message: string) => void
}) {
  const [detail, setDetail] = useState<PaymentReviewDto | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState("")
  const [sending, setSending] = useState(false)
  const request = useRef<AbortController | null>(null)
  const submitting = useRef(false)
  const mounted = useRef(false)

  const load = useCallback(async () => {
    request.current?.abort()
    const controller = new AbortController()
    request.current = controller
    setLoading(true)
    setDetail(null)
    const timeout = window.setTimeout(() => {
      controller.abort()
      if (mounted.current) {
        setError("Tải chi tiết quá lâu. Vui lòng cập nhật lại.")
        setLoading(false)
      }
    }, 15_000)
    controller.signal.addEventListener("abort", () => window.clearTimeout(timeout), { once: true })
    try {
      const result = await getPaymentReview(accessToken, reviewId, controller.signal)
      if (!controller.signal.aborted && mounted.current) setDetail(result)
    } catch (cause) {
      if (!controller.signal.aborted && mounted.current) setError(paymentReviewErrorMessage(cause))
    } finally {
      window.clearTimeout(timeout)
      if (!controller.signal.aborted && mounted.current) setLoading(false)
    }
  }, [accessToken, reviewId])

  useEffect(() => {
    mounted.current = true
    void load()
    return () => {
      mounted.current = false
      request.current?.abort()
    }
  }, [load])

  async function submit(input: ResolvePaymentReviewInput) {
    if (submitting.current || detail?.status !== "Open") return
    submitting.current = true
    setSending(true)
    request.current?.abort()
    onResolveStart()
    setError("")
    try {
      const updated = await resolvePaymentReview(accessToken, reviewId, input)
      if (!mounted.current) return
      setDetail(updated)
      onSuccess(`Đã xử lý mục đối soát #${reviewId}.`)
    } catch (cause) {
      if (!mounted.current) return
      setError(paymentReviewErrorMessage(cause))
      // The POST may have been recorded even if its response was lost.
      await load()
    } finally {
      submitting.current = false
      if (mounted.current) {
        setSending(false)
        onReload()
      }
    }
  }

  return (
    <section aria-label="Chi tiết đối soát thanh toán" aria-busy={loading || sending} className="mb-6 rounded-xl border border-slate-200 bg-white p-5">
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <h3 className="text-lg font-bold text-slate-900">Đối soát thanh toán #{reviewId}</h3>
        <div className="flex flex-wrap gap-2">
          <button type="button" className={buttonClass} disabled={loading || sending} onClick={() => { setError(""); void load() }}>Cập nhật chi tiết</button>
          <button type="button" className={buttonClass} disabled={sending} onClick={onClose}>Đóng chi tiết</button>
        </div>
      </div>
      {error && <p role="alert" className="mb-4 rounded-lg bg-rose-50 p-3 text-sm text-rose-700">{error}</p>}
      {loading && <p role="status" className="py-4 text-sm text-slate-500">Đang tải chi tiết...</p>}
      {detail && !loading && <>
        <div className="mb-4 flex flex-wrap items-center gap-3 text-sm text-slate-600">
          <span>Hoá đơn #{detail.invoiceId}</span><span>Yêu cầu thanh toán #{detail.paymentRequestId}</span>
          <ReviewStatus status={detail.status} />
        </div>
        <dl className="grid gap-x-8 gap-y-4 text-sm sm:grid-cols-2">
          <div><dt className="text-slate-500">Số tiền thừa</dt><dd className="mt-1 font-semibold tabular-nums text-slate-900">{formatVnd(detail.additionalAmount)}</dd></div>
          <div><dt className="text-slate-500">Nguồn</dt><dd className="mt-1 break-words text-slate-900">{detail.source}</dd></div>
          <div className="sm:col-span-2"><dt className="text-slate-500">Lý do đối soát</dt><dd className="mt-1 whitespace-pre-wrap break-words text-slate-900">{detail.reason}</dd></div>
          <div><dt className="text-slate-500">Mã giao dịch payOS</dt><dd className="mt-1 break-all text-slate-900">{detail.providerReference}</dd></div>
          <div><dt className="text-slate-500">Thời điểm giao dịch</dt><dd className="mt-1 text-slate-900">{dateLabel(detail.transactionDateTime)}</dd></div>
        </dl>
        <h4 className="mb-3 mt-5 font-bold text-slate-900">Khoản đã ghi nhận trước</h4>
        <dl className="grid gap-x-8 gap-y-4 text-sm sm:grid-cols-2">
          <div><dt className="text-slate-500">Mã thanh toán</dt><dd className="mt-1 text-slate-900">#{detail.existingPaymentId}</dd></div>
          <div><dt className="text-slate-500">Phương thức</dt><dd className="mt-1 text-slate-900">{detail.existingPaymentMethod}</dd></div>
          <div><dt className="text-slate-500">Số tiền đã ghi nhận</dt><dd className="mt-1 font-semibold tabular-nums text-slate-900">{formatVnd(detail.existingAmount)}</dd></div>
          <div><dt className="text-slate-500">Thời điểm ghi nhận</dt><dd className="mt-1 text-slate-900">{dateLabel(detail.existingPaidAtUtc)}</dd></div>
        </dl>
        {detail.status === "Resolved" && <>
          <h4 className="mb-3 mt-5 font-bold text-slate-900">Kết quả xử lý</h4>
          <dl className="grid gap-x-8 gap-y-4 text-sm sm:grid-cols-2">
            <div><dt className="text-slate-500">Kết luận</dt><dd className="mt-1 text-slate-900">{resolutionLabels[detail.resolution as PaymentReviewResolution] ?? detail.resolution ?? "Chưa có"}</dd></div>
            <div><dt className="text-slate-500">Thời điểm xử lý</dt><dd className="mt-1 text-slate-900">{dateLabel(detail.resolvedAtUtc)}</dd></div>
            <div className="sm:col-span-2"><dt className="text-slate-500">Ghi chú xử lý</dt><dd className="mt-1 whitespace-pre-wrap break-words text-slate-900">{detail.resolutionNote || "Chưa có"}</dd></div>
            <div className="sm:col-span-2"><dt className="text-slate-500">Mã chứng từ hoàn tiền</dt><dd className="mt-1 break-all text-slate-900">{detail.evidenceReference || "Chưa có"}</dd></div>
          </dl>
        </>}
        {detail.status === "Open" && <ResolutionForm sending={sending} onSubmit={submit} />}
      </>}
    </section>
  )
}

function ReviewQueue({ accessToken }: { accessToken: string }) {
  const [status, setStatus] = useState<PaymentReviewStatus>("Open")
  const [page, setPage] = useState(1)
  const [revision, setRevision] = useState(0)
  const [rows, setRows] = useState<PaymentReviewDto[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState("")
  const [success, setSuccess] = useState("")
  const [selected, setSelected] = useState<number | null>(null)
  const [resolving, setResolving] = useState(false)
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
    void getPaymentReviews(accessToken, { status, page, pageSize }, controller.signal)
      .then((result) => { if (!controller.signal.aborted) setRows(result) })
      .catch((cause: unknown) => { if (!controller.signal.aborted) setError(paymentReviewErrorMessage(cause)) })
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
        <select value={status} disabled={resolving} className={`${controlClass} mt-1 block min-h-11 w-full sm:w-72`} onChange={(event) => {
          request.current?.abort()
          setSelected(null)
          setSuccess("")
          setStatus(event.target.value as PaymentReviewStatus)
          setPage(1)
        }}>
          <option value="Open">Cần xử lý</option><option value="Resolved">Đã xử lý</option>
        </select>
      </label>
      <button type="button" className={buttonClass} disabled={loading || resolving} onClick={() => setRevision((value) => value + 1)}>Tải lại danh sách</button>
    </div>
    {selected !== null && <ReviewDetails key={selected} accessToken={accessToken} reviewId={selected}
      onClose={() => setSelected(null)}
      onResolveStart={() => { request.current?.abort(); setResolving(true); setSuccess("") }}
      onReload={() => { setResolving(false); setRevision((value) => value + 1) }}
      onSuccess={setSuccess} />}
    <div className="overflow-hidden rounded-xl border border-slate-200 bg-white" aria-busy={loading}>
      <div className="hidden grid-cols-[.5fr_1fr_1.2fr_1fr_1.3fr_1fr] gap-3 border-b border-slate-100 px-5 py-3 text-xs text-slate-500 xl:grid" aria-hidden="true">
        <span>Mục</span><span>Hoá đơn</span><span className="text-right">Số tiền thừa</span><span>Nguồn</span><span>Ngày tạo</span><span>Trạng thái</span>
      </div>
      {error ? <div className="p-5"><p role="alert" className="text-sm text-rose-700">{error}</p><button type="button" className={`${buttonClass} mt-3`} disabled={resolving} onClick={() => setRevision((value) => value + 1)}>Thử lại</button></div>
        : loading ? <p role="status" className="p-5 text-sm text-slate-500">Đang tải danh sách đối soát...</p>
        : rows.length === 0 ? <p className="p-5 text-sm text-slate-500">Không có mục đối soát trong bộ lọc này.</p>
        : <ul className="divide-y divide-slate-100">{rows.map((row) => <li key={row.id}>
          <button type="button" disabled={resolving} onClick={() => { setSuccess(""); setSelected(row.id) }} aria-label={`Xem mục đối soát #${row.id}`} aria-pressed={selected === row.id}
            className={`grid w-full gap-3 px-5 py-4 text-left text-sm text-slate-700 hover:bg-slate-50 focus-visible:outline-2 focus-visible:outline-[#087775] disabled:opacity-50 sm:grid-cols-2 xl:grid-cols-[.5fr_1fr_1.2fr_1fr_1.3fr_1fr] xl:items-center ${selected === row.id ? "bg-teal-50/50" : ""}`}>
            <span className="font-semibold text-[#087775]">#{row.id}</span><span>Hoá đơn #{row.invoiceId}</span>
            <span className="font-semibold tabular-nums xl:text-right">{formatVnd(row.additionalAmount)}</span>
            <span className="break-words">{row.source}</span><span className="text-xs">{dateLabel(row.createdAtUtc)}</span><span><ReviewStatus status={row.status} /></span>
          </button>
        </li>)}</ul>}
    </div>
    <div className="mt-4 flex items-center justify-end gap-3 text-sm text-slate-600">
      <button type="button" className={buttonClass} disabled={loading || resolving || page === 1} onClick={() => changePage(page - 1)}>Trước</button><span>Trang {page}</span>
      <button type="button" className={buttonClass} disabled={loading || resolving || !!error || rows.length < pageSize} onClick={() => changePage(page + 1)}>Sau</button>
    </div>
  </>
}

export default function ManagementPaymentReviewsPage() {
  const { accessToken, user } = useAuth()
  return <ManagementPage title="Đối soát thanh toán" description="Đối chiếu khoản tiền payOS báo thêm với khoản thanh toán hoá đơn đã ghi nhận.">
    {accessToken && user?.role === "ADMIN"
      ? <ReviewQueue key={`${user.id}:${accessToken}`} accessToken={accessToken} />
      : <p role="alert" className="text-sm text-rose-700">Bạn không có quyền xem đối soát thanh toán.</p>}
  </ManagementPage>
}
