import { useEffect, useState, type FormEvent } from "react"
import { useAuth } from "@/auth/AuthContext"
import {
  cancelContractTransfer,
  confirmContractTransfer,
  getContractTransfers,
} from "@/services/contractTransferService"
import type { ContractDto, ContractTransferDto } from "@/types/contract"
import {
  MAX_TRANSFER_CANCEL_REASON_LENGTH,
  transferCancelReasonError,
} from "@/utils/contractTransferValidation"
import { formatContractDate } from "@/utils/managementContracts"
import { formatVnd } from "@/utils/publicFormatting"

export default function IncomingContractTransfers({
  contracts,
  onUpdated,
}: {
  contracts: ContractDto[]
  onUpdated?: () => void
}) {
  const { accessToken, user } = useAuth()
  const [transfers, setTransfers] = useState<ContractTransferDto[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [confirmingId, setConfirmingId] = useState<number | null>(null)
  const [rejectingTransfer, setRejectingTransfer] =
    useState<ContractTransferDto | null>(null)
  const [rejectReason, setRejectReason] = useState("")
  const [rejectError, setRejectError] = useState<string | null>(null)
  const [submittingReject, setSubmittingReject] = useState(false)

  useEffect(() => {
    if (!accessToken) return
    const controller = new AbortController()
    setLoading(true)
    setError(null)
    Promise.resolve(getContractTransfers(accessToken, controller.signal))
      .then((res) => {
        if (!controller.signal.aborted && Array.isArray(res)) {
          setTransfers(res)
        }
      })
      .catch(() => {
        // Không làm hỏng trang Hợp đồng của tôi nếu API đề nghị chuyển chưa trả về
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setLoading(false)
        }
      })
    return () => controller.abort()
  }, [accessToken])

  if (!accessToken || loading) return null

  const ownedContractIds = new Set(contracts.map((item) => item.contractId))
  const incomingPending = transfers.filter((item) => {
    if (item.status !== "Pending") return false
    if (user?.id) {
      return String(item.newHolderId) === String(user.id)
    }
    return !ownedContractIds.has(item.fromContractId)
  })

  if (incomingPending.length === 0 && !notice && !error) return null

  async function handleAccept(transfer: ContractTransferDto) {
    if (!accessToken || confirmingId !== null) return
    setConfirmingId(transfer.contractTransferId)
    setError(null)
    setNotice(null)
    try {
      const updated = await confirmContractTransfer(
        accessToken,
        transfer.contractTransferId,
      )
      setTransfers((prev) =>
        prev.map((item) =>
          item.contractTransferId === updated.contractTransferId
            ? updated
            : item,
        ),
      )
      setNotice(
        `Đã đồng ý nhận chuyển hợp đồng phòng ${transfer.roomCode} (${transfer.propertyName}).`,
      )
      onUpdated?.()
    } catch (err: unknown) {
      setError(
        err instanceof Error
          ? err.message
          : "Không thể đồng ý nhận chuyển hợp đồng.",
      )
    } finally {
      setConfirmingId(null)
    }
  }

  async function handleRejectSubmit(event: FormEvent) {
    event.preventDefault()
    if (!accessToken || !rejectingTransfer || submittingReject) return
    const reasonErr = transferCancelReasonError(rejectReason)
    if (reasonErr) {
      setRejectError(reasonErr)
      return
    }

    setSubmittingReject(true)
    setRejectError(null)
    setError(null)
    setNotice(null)
    try {
      const updated = await cancelContractTransfer(
        accessToken,
        rejectingTransfer.contractTransferId,
        { reason: rejectReason.trim() || null },
      )
      setTransfers((prev) =>
        prev.map((item) =>
          item.contractTransferId === updated.contractTransferId
            ? updated
            : item,
        ),
      )
      setRejectingTransfer(null)
      setRejectReason("")
      setNotice(
        `Đã từ chối đề nghị chuyển hợp đồng phòng ${updated.roomCode}.`,
      )
      onUpdated?.()
    } catch (err: unknown) {
      setRejectError(
        err instanceof Error
          ? err.message
          : "Không thể từ chối đề nghị chuyển hợp đồng.",
      )
    } finally {
      setSubmittingReject(false)
    }
  }

  return (
    <section
      aria-label="Đề nghị chuyển hợp đồng dành cho bạn"
      className="mb-6 rounded-2xl border border-teal-200 bg-teal-50/60 p-5 shadow-[0_3px_18px_rgba(16,65,67,.05)]"
    >
      <h2 className="font-display text-lg font-bold text-slate-900">
        Đề nghị chuyển hợp đồng dành cho bạn
      </h2>
      <p className="mt-0.5 text-xs text-slate-600">
        Khi bạn đồng ý nhận và quản lý hoàn tất chuyển hợp đồng, tiền cọc sẽ
        được chuyển nguyên sang hợp đồng mới đứng tên bạn.
      </p>

      {error && (
        <p
          role="alert"
          className="mt-3 rounded-lg bg-rose-50 px-3 py-2 text-xs font-medium text-rose-800"
        >
          {error}
        </p>
      )}

      {notice && (
        <p className="mt-3 rounded-lg bg-emerald-50 px-3 py-2 text-xs font-medium text-emerald-800">
          {notice}
        </p>
      )}

      <div className="mt-4 space-y-3">
        {incomingPending.map((item) => (
          <article
            key={item.contractTransferId}
            className="rounded-xl border border-teal-200 bg-white p-4 text-xs"
          >
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <p className="text-sm font-bold text-slate-900">
                  {item.propertyName} · Phòng {item.roomCode}
                </p>
                <p className="mt-0.5 text-slate-600">
                  Người đề nghị: <strong>{item.requestedByName}</strong> (Người
                  đứng tên cũ: {item.oldHolderName})
                </p>
              </div>
              <span className="rounded-full bg-amber-100 px-2.5 py-0.5 font-bold text-amber-900">
                {item.newHolderConfirmedAtUtc
                  ? "Bạn đã đồng ý · Chờ hoàn tất"
                  : "Chờ bạn xác nhận"}
              </span>
            </div>

            <dl className="mt-3 grid gap-2 rounded-lg bg-slate-50 p-3 sm:grid-cols-2 lg:grid-cols-4">
              <div>
                <dt className="text-slate-500">Tiền phòng / tháng</dt>
                <dd className="font-bold text-slate-900">
                  {formatVnd(item.monthlyRent)}
                </dd>
              </div>
              <div>
                <dt className="text-slate-500">Ngày hiệu lực</dt>
                <dd className="font-bold text-slate-900">
                  {formatContractDate(item.effectiveDate)}
                </dd>
              </div>
              <div>
                <dt className="text-slate-500">Ngày kết thúc</dt>
                <dd className="font-bold text-slate-900">
                  {formatContractDate(item.contractEndDate)}
                </dd>
              </div>
              <div>
                <dt className="text-slate-500">Tiền cọc được chuyển</dt>
                <dd className="font-bold text-[#087775]">
                  {formatVnd(item.depositCarriedOver)}
                </dd>
              </div>
            </dl>

            <div className="mt-3 flex flex-wrap justify-end gap-2">
              {item.canConfirm && (
                <button
                  type="button"
                  disabled={confirmingId === item.contractTransferId}
                  onClick={() => void handleAccept(item)}
                  className="rounded-lg bg-[#087775] px-3.5 py-1.5 font-bold text-white hover:bg-teal-800 disabled:opacity-50"
                >
                  {confirmingId === item.contractTransferId
                    ? "Đang xác nhận..."
                    : "Đồng ý nhận"}
                </button>
              )}

              {item.canCancel && (
                <button
                  type="button"
                  onClick={() => {
                    setRejectingTransfer(item)
                    setRejectReason("")
                    setRejectError(null)
                  }}
                  className="rounded-lg border border-rose-200 bg-rose-50 px-3.5 py-1.5 font-bold text-rose-700 hover:bg-rose-100"
                >
                  Từ chối
                </button>
              )}
            </div>
          </article>
        ))}
      </div>

      {rejectingTransfer && (
        <form
          noValidate
          onSubmit={(e) => void handleRejectSubmit(e)}
          aria-label="Biểu mẫu từ chối nhận chuyển hợp đồng"
          className="mt-4 space-y-2.5 rounded-xl border border-rose-200 bg-white p-4 text-xs"
        >
          <h3 className="text-sm font-bold text-slate-900">
            Từ chối đề nghị chuyển phòng {rejectingTransfer.roomCode}
          </h3>
          <label className="block font-semibold text-slate-700">
            Lý do từ chối
            <textarea
              rows={2}
              maxLength={MAX_TRANSFER_CANCEL_REASON_LENGTH}
              value={rejectReason}
              onChange={(e) => {
                setRejectReason(e.target.value)
                setRejectError(null)
              }}
              placeholder="Nhập lý do từ chối (không bắt buộc, tối đa 500 ký tự)"
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-1.5 text-xs text-slate-900"
            />
          </label>
          {rejectError && (
            <p
              role="alert"
              className="rounded-lg bg-rose-50 px-3 py-2 text-xs font-medium text-rose-800"
            >
              {rejectError}
            </p>
          )}
          <div className="flex justify-end gap-2">
            <button
              type="button"
              disabled={submittingReject}
              onClick={() => setRejectingTransfer(null)}
              className="rounded-lg border border-slate-300 px-3 py-1.5 font-semibold text-slate-700"
            >
              Hủy
            </button>
            <button
              type="submit"
              disabled={submittingReject}
              className="rounded-lg bg-rose-600 px-3.5 py-1.5 font-bold text-white disabled:opacity-50"
            >
              {submittingReject ? "Đang gửi..." : "Xác nhận từ chối"}
            </button>
          </div>
        </form>
      )}
    </section>
  )
}
