import { useEffect, useRef, useState } from "react"
import { useAuth } from "@/auth/AuthContext"
import ManagementPage from "@/components/management/ManagementPage"
import StatusBadge from "@/components/customer/StatusBadge"
import {
  getRefundReview,
  getRefundReviews,
  reviewBookingRefund,
} from "@/services/bookingRefundService"
import type {
  BookingRefundReviewDto,
  RefundStatus,
} from "@/types/bookingRefund"
import {
  maskAccount,
  refundDisplay,
  refundError,
  refundIsActive,
  refundLabels,
  validNote,
} from "@/utils/bookingRefund"
import { formatDateTime } from "@/utils/bookingDisplay"
import { formatVnd } from "@/utils/publicFormatting"

const field =
  "mt-1 w-full rounded-lg border border-slate-200 bg-white px-3 py-2.5 text-sm text-slate-800"
export default function ManagementRefundsPage() {
  const { user, accessToken } = useAuth()
  const [status, setStatus] = useState<RefundStatus | "">("PendingReview")
  const [page, setPage] = useState(1)
  const [revision, setRevision] = useState(0)
  const [selected, setSelected] = useState<number | null>(null)
  const key = `${accessToken}:${status}:${page}:${revision}`
  const [state, setState] = useState<{
    key: string
    rows: BookingRefundReviewDto[]
    error: string
    loading: boolean
  }>({ key, rows: [], error: "", loading: true })
  useEffect(() => {
    if (user?.role !== "ADMIN" || !accessToken) return
    const controller = new AbortController()
    const timeout = setTimeout(() => controller.abort(), 15000)
    setState({ key, rows: [], error: "", loading: true })
    getRefundReviews(accessToken, status, page, controller.signal)
      .then((rows) => {
        if (!controller.signal.aborted)
          setState({ key, rows, error: "", loading: false })
      })
      .catch((error) => {
        if (!controller.signal.aborted)
          setState({ key, rows: [], error: refundError(error), loading: false })
      })
      .finally(() => clearTimeout(timeout))
    // An explicit timeout still needs an error; cleanup aborts must not update the next account's UI.
    const deadline = setTimeout(
      () =>
        setState((previous) =>
          previous.key === key && previous.loading
            ? {
                key,
                rows: [],
                error: "Không tải được danh sách. Vui lòng thử lại.",
                loading: false,
              }
            : previous,
        ),
      15010,
    )
    return () => {
      clearTimeout(timeout)
      clearTimeout(deadline)
      controller.abort()
    }
  }, [accessToken, user?.role, status, page, revision, key])
  if (user?.role !== "ADMIN")
    return (
      <ManagementPage title="Hoàn cọc" description="">
        <p role="alert">Chỉ Admin có quyền xét duyệt hoàn cọc.</p>
      </ManagementPage>
    )
  const current =
    state.key === key ? state : { rows: [], error: "", loading: true }
  return (
    <ManagementPage
      title="Xét duyệt hoàn cọc"
      description="Đối chiếu khoản cọc và tài khoản nhận, quyết định số tiền hoàn và theo dõi kết quả chuyển tiền."
    >
      <div className="mb-5 flex flex-wrap items-end gap-3">
        <label className="text-sm font-semibold text-slate-600">
          Trạng thái
          <select
            value={status}
            onChange={(event) => {
              setStatus(event.target.value as RefundStatus | "")
              setPage(1)
              setSelected(null)
            }}
            className={field}
          >
            <option value="">Tất cả trạng thái</option>
            {Object.entries(refundLabels).map(([value, label]) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </select>
        </label>
        <button
          type="button"
          onClick={() => setRevision((value) => value + 1)}
          className="rounded-lg bg-[#087775] px-4 py-2.5 text-sm font-bold text-white"
        >
          Tải lại danh sách
        </button>
      </div>
      {current.loading && <p role="status">Đang tải đơn hoàn cọc...</p>}
      {current.error && (
        <p
          role="alert"
          className="rounded-xl bg-rose-50 p-4 text-sm text-rose-700"
        >
          {current.error}
        </p>
      )}
      {!current.loading && !current.error && (
        <>
          {current.rows.length === 0 && (
            <p className="rounded-xl bg-white p-6 text-sm text-slate-500">
              Không có đơn phù hợp.
            </p>
          )}
          <div className="space-y-3">
            {current.rows.map((item) => {
              const display = refundDisplay(item.status)
              return (
                <article
                  key={item.bookingRefundRequestId}
                  className="flex flex-wrap items-center justify-between gap-4 rounded-xl bg-white p-5 shadow-sm"
                >
                  <div>
                    <strong className="text-slate-800">
                      Đơn #{item.bookingRefundRequestId} · Booking #
                      {item.bookingId}
                    </strong>
                    <p className="mt-1 text-sm text-slate-600">
                      Cọc đã nhận: {formatVnd(item.depositAmount)} · Yêu cầu:{" "}
                      {formatVnd(item.requestedAmount)}
                    </p>
                    <p className="mt-1 text-xs text-slate-500">
                      Tài khoản {maskAccount(item.recipientAccountNumber)} · Gửi{" "}
                      {formatDateTime(item.createdAtUtc)}
                    </p>
                  </div>
                  <div className="flex flex-wrap items-center gap-3">
                    <StatusBadge tone={display.tone}>
                      {display.label}
                    </StatusBadge>
                    <button
                      type="button"
                      onClick={() => setSelected(item.bookingRefundRequestId)}
                      className="rounded-lg border border-[#087775] px-3 py-2 text-sm font-bold text-[#087775]"
                    >
                      Xem đơn #{item.bookingRefundRequestId}
                    </button>
                  </div>
                </article>
              )
            })}
          </div>
          <nav
            aria-label="Phân trang đơn hoàn"
            className="my-5 flex items-center gap-4 text-sm"
          >
            <button
              disabled={page === 1}
              onClick={() => {
                setPage((value) => value - 1)
                setSelected(null)
              }}
              className="rounded-lg border px-3 py-2 disabled:opacity-40"
            >
              Trang trước
            </button>
            <span>Trang {page}</span>
            <button
              disabled={current.rows.length < 20}
              onClick={() => {
                setPage((value) => value + 1)
                setSelected(null)
              }}
              className="rounded-lg border px-3 py-2 disabled:opacity-40"
            >
              Trang sau
            </button>
          </nav>
        </>
      )}
      {selected !== null && accessToken && (
        <ReviewDetail
          key={`${accessToken}:${selected}`}
          id={selected}
          accessToken={accessToken}
          onReviewed={() => setRevision((value) => value + 1)}
          onClose={() => setSelected(null)}
        />
      )}
    </ManagementPage>
  )
}

function ReviewDetail({
  id,
  accessToken,
  onReviewed,
  onClose,
}: {
  id: number
  accessToken: string
  onReviewed: () => void
  onClose: () => void
}) {
  const [detail, setDetail] = useState<BookingRefundReviewDto | null>(null)
  const [error, setError] = useState("")
  const [actionError, setActionError] = useState("")
  const [paused, setPaused] = useState(false)
  const [amount, setAmount] = useState("")
  const [note, setNote] = useState("")
  const [confirmed, setConfirmed] = useState(false)
  const [busy, setBusy] = useState(false)
  const [revision, setRevision] = useState(0)
  const lock = useRef(false)
  const generation = useRef(0)
  const active = useRef(true)
  useEffect(() => {
    active.current = true
    return () => {
      active.current = false
    }
  }, [])
  useEffect(() => {
    setPaused(false)
    const controller = new AbortController()
    let timer: ReturnType<typeof setTimeout> | undefined
    let count = 0
    const check = async () => {
      if (lock.current) {
        timer = setTimeout(check, 15000)
        return
      }
      const currentGeneration = generation.current
      const request = new AbortController()
      const abort = () => request.abort()
      controller.signal.addEventListener("abort", abort)
      const timeout = setTimeout(abort, 15000)
      try {
        const result = await getRefundReview(accessToken, id, request.signal)
        if (
          controller.signal.aborted ||
          currentGeneration !== generation.current
        )
          return
        if (result.bookingRefundRequestId !== id)
          throw new Error("Refund mismatch")
        setDetail(result)
        setError("")
        setAmount((previous) => previous || String(result.requestedAmount))
        count++
        if (refundIsActive(result.status) && count < 20)
          timer = setTimeout(check, 15000)
        else if (refundIsActive(result.status)) setPaused(true)
      } catch (failure) {
        if (
          !controller.signal.aborted &&
          currentGeneration === generation.current
        ) {
          setDetail(null)
          setError(refundError(failure))
        }
      } finally {
        clearTimeout(timeout)
        controller.signal.removeEventListener("abort", abort)
      }
    }
    void check()
    return () => {
      controller.abort()
      clearTimeout(timer)
    }
  }, [accessToken, id, revision])
  const decide = async (approve: boolean) => {
    if (lock.current || !detail || detail.status !== "PendingReview") return
    if (!validNote(note)) {
      setActionError(
        "Nhập lý do xét duyệt từ 1 đến 2000 ký tự, không xuống dòng.",
      )
      return
    }
    const value = Number(amount)
    if (
      approve &&
      (!Number.isSafeInteger(value) ||
        value <= 0 ||
        value > detail.requestedAmount)
    ) {
      setActionError(
        "Tiền hoàn phải là số nguyên dương, không vượt khoản yêu cầu.",
      )
      return
    }
    if (approve && (!confirmed || !detail.recipientAccountName?.trim())) {
      setActionError(
        "Cần có tên tài khoản và xác nhận đã đối chiếu trước khi duyệt.",
      )
      return
    }
    generation.current++
    lock.current = true
    setBusy(true)
    setError("")
    setActionError("")
    try {
      const result = await reviewBookingRefund(accessToken, id, {
        approve,
        approvedAmount: approve ? value : null,
        reviewNote: note.trim(),
      })
      if (!active.current) return
      setDetail(result)
      setNote("")
      setConfirmed(false)
      setRevision((value) => value + 1)
      onReviewed()
    } catch (failure) {
      if (active.current) {
        setDetail(null)
        setConfirmed(false)
        setActionError(refundError(failure))
        setRevision((value) => value + 1)
      }
    } finally {
      lock.current = false
      if (active.current) setBusy(false)
    }
  }
  return (
    <section
      aria-label={`Chi tiết đơn hoàn ${id}`}
      className="mt-6 rounded-2xl border border-teal-200 bg-white p-5 shadow-sm sm:p-7"
    >
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-xl font-bold text-slate-900">Chi tiết đơn #{id}</h2>
        <div className="flex gap-3">
          <button
            type="button"
            disabled={busy}
            onClick={() => setRevision((value) => value + 1)}
            className="text-sm font-semibold text-[#087775]"
          >
            Cập nhật đơn
          </button>
          <button
            type="button"
            disabled={busy}
            onClick={onClose}
            className="text-sm font-semibold text-slate-500"
          >
            Đóng chi tiết
          </button>
        </div>
      </div>
      {!detail && !error && (
        <p role="status" className="mt-4">
          Đang tải chi tiết...
        </p>
      )}
      {detail && (
        <div className="mt-5 space-y-4 text-sm">
          <StatusBadge tone={refundDisplay(detail.status).tone}>
            {refundDisplay(detail.status).label}
          </StatusBadge>
          <dl className="grid gap-4 sm:grid-cols-2">
            {[
              [
                "Booking / Customer",
                `#${detail.bookingId} / #${detail.customerId}`,
              ],
              ["Payment gốc", `#${detail.originalPaymentId}`],
              ["Cọc thực nhận", formatVnd(detail.depositAmount)],
              ["Số tiền yêu cầu", formatVnd(detail.requestedAmount)],
              ["Ngân hàng (BIN)", detail.recipientBankBin],
              ["Số tài khoản nhận", detail.recipientAccountNumber],
              [
                "Tên chủ tài khoản",
                detail.recipientAccountName ?? "Chưa được cung cấp",
              ],
              ["Lý do xin hoàn", detail.reason],
            ].map(([label, value]) => (
              <div key={label}>
                <dt className="text-slate-400">{label}</dt>
                <dd className="mt-1 break-words font-semibold text-slate-700">
                  {value}
                </dd>
              </div>
            ))}
          </dl>
          <p className="rounded-lg bg-amber-50 p-3 text-amber-900">
            Tên tài khoản do Customer cung cấp, chưa được ngân hàng xác minh tự
            động. Đối chiếu ngân hàng, số và tên chủ tài khoản trước khi duyệt.
          </p>
          {detail.approvedAmount !== null && (
            <p>
              Số tiền đã duyệt:{" "}
              <strong>{formatVnd(detail.approvedAmount)}</strong>
            </p>
          )}
          {detail.reviewNote && <p>Ý kiến xét duyệt: {detail.reviewNote}</p>}
          {detail.refundedAtUtc && (
            <p className="text-emerald-700">
              Đã hoàn tất hoàn cọc: {formatDateTime(detail.refundedAtUtc)}
            </p>
          )}
          {detail.status === "PendingReview" ? (
            <div className="space-y-4 border-t border-slate-100 pt-4">
              <label className="block font-medium text-slate-700">
                Số tiền duyệt hoàn (VND)
                <input
                  type="number"
                  min={1}
                  step={1}
                  max={detail.requestedAmount}
                  disabled={busy}
                  value={amount}
                  onChange={(event) => setAmount(event.target.value)}
                  className={field}
                />
              </label>
              <label className="block font-medium text-slate-700">
                Lý do xét duyệt
                <input
                  maxLength={2000}
                  disabled={busy}
                  value={note}
                  onChange={(event) => setNote(event.target.value)}
                  className={field}
                />
              </label>
              <label className="flex items-start gap-2 text-slate-600">
                <input
                  type="checkbox"
                  disabled={busy}
                  checked={confirmed}
                  onChange={(event) => setConfirmed(event.target.checked)}
                  className="mt-0.5"
                />
                Tôi đã đối chiếu khoản cọc, số tiền hoàn và tài khoản nhận.
              </label>
              <p className="text-xs text-slate-500">
                Duyệt sẽ cho phép hệ thống xử lý chuyển tiền theo cấu hình kênh
                chi. Nếu giữ lại một phần cọc, giải thích rõ trong lý do.
              </p>
              <div className="flex flex-wrap gap-3">
                <button
                  type="button"
                  disabled={busy || !confirmed || !detail.recipientAccountName}
                  onClick={() => void decide(true)}
                  className="rounded-lg bg-[#087775] px-4 py-2.5 font-bold text-white disabled:opacity-40"
                >
                  {busy ? "Đang lưu..." : "Duyệt hoàn cọc"}
                </button>
                <button
                  type="button"
                  disabled={busy}
                  onClick={() => void decide(false)}
                  className="rounded-lg bg-rose-50 px-4 py-2.5 font-bold text-rose-700 disabled:opacity-40"
                >
                  Từ chối yêu cầu
                </button>
              </div>
            </div>
          ) : (
            <p className="text-slate-500">
              Đơn đã được xét duyệt, không thể ghi đè quyết định. Tiến độ sẽ
              được cập nhật theo kết quả chuyển tiền.
            </p>
          )}
        </div>
      )}
      {error && (
        <p role="alert" className="mt-4 text-sm text-rose-700">
          {error}
        </p>
      )}
      {actionError && (
        <p role="alert" className="mt-4 text-sm text-rose-700">
          {actionError}
        </p>
      )}
    {paused && <p className="mt-4 text-sm text-slate-500">Đã tạm dừng cập nhật tự động; bấm Cập nhật đơn để xem tiến độ mới.</p>}
    </section>
  )
}
