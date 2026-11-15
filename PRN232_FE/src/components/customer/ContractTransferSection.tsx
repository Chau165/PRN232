import { useEffect, useState, type FormEvent } from "react"
import { ApiError } from "@/api/apiClient"
import { useAuth } from "@/auth/AuthContext"
import { getContractOccupants } from "@/services/contractOccupantsService"
import {
  cancelContractTransfer,
  completeContractTransfer,
  confirmContractTransfer,
  createContractTransfer,
  getContractTransfers,
} from "@/services/contractTransferService"
import type {
  ContractDto,
  ContractOccupantDto,
  ContractTransferDto,
} from "@/types/contract"
import { formatEvidenceUploadedAt } from "@/utils/contractEvidenceValidation"
import {
  getAvailableTransferEffectiveDates,
  getOldContractEndDateOnTransfer,
  MAX_TRANSFER_CANCEL_REASON_LENGTH,
  transferCancelReasonError,
  transferEffectiveDateError,
  transferEmailError,
} from "@/utils/contractTransferValidation"
import { formatContractDate } from "@/utils/managementContracts"
import { formatVnd } from "@/utils/publicFormatting"

function pickContractTransfer(
  transfers: ContractTransferDto[],
  contractId: number,
): ContractTransferDto | null {
  const matches = transfers.filter((item) => item.fromContractId === contractId)
  if (matches.length === 0) return null
  return (
    matches.find((item) => item.status === "Pending") ??
    matches.find((item) => item.status === "Completed") ??
    matches[0]
  )
}

export default function ContractTransferSection({
  contract,
  onTransferCompleted,
}: {
  contract: ContractDto
  onTransferCompleted?: () => void
}) {
  const { accessToken, user } = useAuth()
  const normalizedRole = user?.role?.toUpperCase() ?? "CUSTOMER"
  const isAdmin = normalizedRole === "ADMIN"
  const isManager = normalizedRole === "MANAGER"
  const isActiveContract =
    contract.status === "Active" &&
    contract.displayStatus !== "Terminated" &&
    contract.displayStatus !== "Ended"

  const [transfer, setTransfer] = useState<ContractTransferDto | null>(null)
  const [occupants, setOccupants] = useState<ContractOccupantDto[]>([])
  const [loading, setLoading] = useState(false)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [retryCount, setRetryCount] = useState(0)

  const [isCreating, setIsCreating] = useState(false)
  const [newHolderEmail, setNewHolderEmail] = useState("")
  const [effectiveDate, setEffectiveDate] = useState("")
  const [selectedOccupantId, setSelectedOccupantId] = useState("")
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [submittingCreate, setSubmittingCreate] = useState(false)

  const [confirming, setConfirming] = useState(false)
  const [cancelOpen, setCancelOpen] = useState(false)
  const [cancelReason, setCancelReason] = useState("")
  const [cancelError, setCancelError] = useState<string | null>(null)
  const [cancelling, setCancelling] = useState(false)

  const [completeDialogOpen, setCompleteDialogOpen] = useState(false)
  const [completing, setCompleting] = useState(false)
  const [actionError, setActionError] = useState<string | null>(null)
  const [successMessage, setSuccessMessage] = useState<string | null>(null)

  useEffect(() => {
    if (!accessToken) return
    const controller = new AbortController()
    setLoading(true)
    setLoadError(null)

    Promise.all([
      Promise.resolve(
        getContractTransfers(accessToken, controller.signal),
      ).catch((err: unknown) => {
        if (controller.signal.aborted) throw err
        throw err
      }),
      Promise.resolve(
        getContractOccupants(
          accessToken,
          contract.contractId,
          controller.signal,
        ),
      ).catch(() => null),
    ])
      .then(([transfersRes, occupantsRes]) => {
        if (controller.signal.aborted) return
        if (Array.isArray(transfersRes)) {
          setTransfer(pickContractTransfer(transfersRes, contract.contractId))
        }
        if (occupantsRes?.occupants) {
          setOccupants(occupantsRes.occupants.filter((item) => item.isActive))
        }
      })
      .catch((err: unknown) => {
        if (!controller.signal.aborted) {
          setLoadError(
            err instanceof Error
              ? err.message
              : "Không thể tải thông tin chuyển người đứng tên.",
          )
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setLoading(false)
        }
      })

    return () => controller.abort()
  }, [accessToken, contract.contractId, retryCount])

  if (!accessToken) return null

  const effectiveDateOptions = getAvailableTransferEffectiveDates({
    contractEndDate: contract.endDate,
    contractStartDate: contract.startDate,
  })
  const carryDepositAmount =
    (contract.securityDepositAmount ?? 0) > 0
      ? contract.securityDepositAmount!
      : contract.depositAmount

  const canInitiateTransfer =
    isActiveContract &&
    !isAdmin &&
    !contract.transferredToContractId &&
    (!transfer || transfer.status === "Cancelled")

  function openCreateForm() {
    setNewHolderEmail("")
    setEffectiveDate(effectiveDateOptions[0] ?? "")
    setSelectedOccupantId("")
    setFieldErrors({})
    setFormError(null)
    setActionError(null)
    setSuccessMessage(null)
    setIsCreating(true)
  }

  async function handleCreateTransfer(event: FormEvent) {
    event.preventDefault()
    if (!accessToken || submittingCreate) return

    const nextErrors: Record<string, string> = {}
    const emailErr = transferEmailError(newHolderEmail)
    if (emailErr) nextErrors.newHolderEmail = emailErr

    const dateErr = transferEffectiveDateError({
      effectiveDate,
      contractEndDate: contract.endDate,
      contractStartDate: contract.startDate,
    })
    if (dateErr) nextErrors.effectiveDate = dateErr

    if (Object.keys(nextErrors).length > 0) {
      setFieldErrors(nextErrors)
      setFormError(null)
      return
    }

    setSubmittingCreate(true)
    setFieldErrors({})
    setFormError(null)

    try {
      const created = await createContractTransfer(accessToken, {
        contractId: contract.contractId,
        newHolderEmail: newHolderEmail.trim(),
        effectiveDate: effectiveDate.trim(),
        occupantId: selectedOccupantId ? Number(selectedOccupantId) : null,
      })
      setTransfer(created)
      setIsCreating(false)
      setSuccessMessage("Đã gửi đề nghị chuyển người đứng tên hợp đồng.")
    } catch (err: unknown) {
      if (err instanceof ApiError && err.validationErrors) {
        const mapped: Record<string, string> = {}
        for (const [key, messages] of Object.entries(err.validationErrors)) {
          const normalizedKey = key.charAt(0).toLowerCase() + key.slice(1)
          if (messages[0]) {
            mapped[normalizedKey] = messages[0]
          }
        }
        setFieldErrors(mapped)
        if (Object.keys(mapped).length === 0) {
          setFormError(err.message)
        }
      } else {
        setFormError(
          err instanceof Error
            ? err.message
            : "Không thể tạo đề nghị chuyển người đứng tên.",
        )
      }
    } finally {
      setSubmittingCreate(false)
    }
  }

  async function handleConfirm() {
    if (!accessToken || !transfer || confirming) return
    setConfirming(true)
    setActionError(null)
    setSuccessMessage(null)
    try {
      const updated = await confirmContractTransfer(
        accessToken,
        transfer.contractTransferId,
      )
      setTransfer(updated)
      setSuccessMessage("Đã ghi nhận xác nhận chuyển hợp đồng của bạn.")
    } catch (err: unknown) {
      setActionError(
        err instanceof Error
          ? err.message
          : "Không thể xác nhận đề nghị chuyển hợp đồng.",
      )
    } finally {
      setConfirming(false)
    }
  }

  async function handleCancelSubmit(event: FormEvent) {
    event.preventDefault()
    if (!accessToken || !transfer || cancelling) return
    const reasonErr = transferCancelReasonError(cancelReason)
    if (reasonErr) {
      setCancelError(reasonErr)
      return
    }

    setCancelling(true)
    setCancelError(null)
    setActionError(null)
    try {
      const updated = await cancelContractTransfer(
        accessToken,
        transfer.contractTransferId,
        { reason: cancelReason.trim() || null },
      )
      setTransfer(updated)
      setCancelOpen(false)
      setCancelReason("")
      setSuccessMessage("Đã hủy đề nghị chuyển người đứng tên.")
    } catch (err: unknown) {
      setCancelError(
        err instanceof Error
          ? err.message
          : "Không thể hủy đề nghị chuyển hợp đồng.",
      )
    } finally {
      setCancelling(false)
    }
  }

  async function handleCompleteTransfer() {
    if (!accessToken || !transfer || completing) return
    setCompleting(true)
    setActionError(null)
    try {
      const updated = await completeContractTransfer(
        accessToken,
        transfer.contractTransferId,
      )
      setTransfer(updated)
      setCompleteDialogOpen(false)
      setSuccessMessage(
        updated.newContractId
          ? `Đã hoàn tất chuyển sang hợp đồng #${updated.newContractId}.`
          : "Đã hoàn tất chuyển người đứng tên hợp đồng.",
      )
      onTransferCompleted?.()
    } catch (err: unknown) {
      setActionError(
        err instanceof Error
          ? err.message
          : "Không thể hoàn tất chuyển người đứng tên.",
      )
      setCompleteDialogOpen(false)
    } finally {
      setCompleting(false)
    }
  }

  return (
    <section
      className="mt-5 rounded-xl border border-slate-200 bg-slate-50/70 p-4 text-sm"
      aria-label={`Chuyển người đứng tên hợp đồng ${contract.contractId}`}
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h4 className="font-bold text-slate-900">Chuyển người đứng tên</h4>
          <p className="mt-0.5 text-xs text-slate-600">
            Hiệu lực từ ngày 1 của một tháng. Tiền cọc{" "}
            {formatVnd(carryDepositAmount)} chuyển nguyên sang hợp đồng mới; phần
            cọc giữa hai bạn tự thanh toán với nhau.
          </p>
        </div>

        {canInitiateTransfer && !isCreating && (
          <button
            type="button"
            disabled={effectiveDateOptions.length === 0}
            onClick={openCreateForm}
            className="rounded-lg bg-[#087775] px-3 py-1.5 text-xs font-bold text-white hover:bg-teal-800 disabled:cursor-not-allowed disabled:opacity-50"
          >
            Đề nghị chuyển
          </button>
        )}
      </div>

      {loading && (
        <p role="status" className="mt-3 text-xs text-slate-500">
          Đang tải thông tin chuyển người đứng tên...
        </p>
      )}

      {!loading && loadError && (
        <p role="alert" className="mt-3 text-xs text-rose-600">
          {loadError}{" "}
          <button
            type="button"
            onClick={() => setRetryCount((c) => c + 1)}
            className="font-semibold underline"
          >
            Thử lại
          </button>
        </p>
      )}

      {actionError && (
        <p
          role="alert"
          className="mt-3 rounded-lg bg-rose-50 px-3 py-2 text-xs font-medium text-rose-800"
        >
          {actionError}
        </p>
      )}

      {successMessage && (
        <p className="mt-3 rounded-lg bg-emerald-50 px-3 py-2 text-xs font-medium text-emerald-800">
          {successMessage}
        </p>
      )}

      {!loading && !loadError && transfer && (
        <div className="mt-3 space-y-3 rounded-xl border border-slate-200 bg-white p-4 text-xs">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div>
              <p className="text-sm font-bold text-slate-900">
                Đề nghị chuyển #{transfer.contractTransferId} · Hiệu lực từ{" "}
                {formatContractDate(transfer.effectiveDate)}
              </p>
              <p className="mt-0.5 text-slate-600">
                Người đứng tên mới:{" "}
                <strong className="text-slate-900">
                  {transfer.newHolderName}
                </strong>{" "}
                ({transfer.newHolderEmail})
                {transfer.becomingHolderOccupantName
                  ? ` · Từ người ở cùng: ${transfer.becomingHolderOccupantName}`
                  : ""}
              </p>
              <p className="mt-0.5 text-slate-600">
                Tiền cọc chuyển sang:{" "}
                <strong className="text-slate-900">
                  {formatVnd(transfer.depositCarriedOver)}
                </strong>{" "}
                · Người đề nghị: {transfer.requestedByName}
              </p>
            </div>

            <span
              className={`inline-flex items-center rounded-full px-2.5 py-0.5 font-bold ${
                transfer.status === "Completed"
                  ? "bg-emerald-100 text-emerald-800"
                  : transfer.status === "Cancelled"
                    ? "bg-slate-200 text-slate-700"
                    : "bg-amber-100 text-amber-900"
              }`}
            >
              {transfer.status === "Completed"
                ? "Đã hoàn tất"
                : transfer.status === "Cancelled"
                  ? "Đã hủy"
                  : "Đang chờ xác nhận"}
            </span>
          </div>

          <div className="grid gap-2 rounded-lg bg-slate-50 p-3 sm:grid-cols-2">
            <div className="flex items-center justify-between gap-2">
              <span className="font-semibold text-slate-700">
                Người đứng tên cũ ({transfer.oldHolderName}):
              </span>
              <span
                className={
                  transfer.oldHolderConfirmedAtUtc
                    ? "font-bold text-emerald-700"
                    : "font-semibold text-amber-700"
                }
              >
                {transfer.oldHolderConfirmedAtUtc
                  ? "✅ Đã đồng ý"
                  : "⏳ Chờ xác nhận"}
              </span>
            </div>
            <div className="flex items-center justify-between gap-2">
              <span className="font-semibold text-slate-700">
                Người nhận ({transfer.newHolderName}):
              </span>
              <span
                className={
                  transfer.newHolderConfirmedAtUtc
                    ? "font-bold text-emerald-700"
                    : "font-semibold text-amber-700"
                }
              >
                {transfer.newHolderConfirmedAtUtc
                  ? "✅ Đã đồng ý"
                  : "⏳ Chờ xác nhận"}
              </span>
            </div>
          </div>

          {transfer.status === "Completed" && transfer.newContractId && (
            <p className="rounded-lg bg-emerald-50 px-3 py-2 font-semibold text-emerald-900">
              Đã chuyển sang hợp đồng #{transfer.newContractId}
              {transfer.completedAtUtc
                ? ` lúc ${formatEvidenceUploadedAt(transfer.completedAtUtc)}`
                : ""}
              .
            </p>
          )}

          {transfer.status === "Cancelled" && (
            <p className="rounded-lg bg-slate-100 px-3 py-2 text-slate-700">
              Đề nghị đã bị hủy
              {transfer.cancelReason ? `: ${transfer.cancelReason}` : "."}
            </p>
          )}

          {(isManager || isAdmin) &&
            transfer.status === "Pending" &&
            transfer.blockers.length > 0 && (
              <div className="rounded-lg border border-amber-200 bg-amber-50/70 p-3">
                <p className="font-bold text-amber-950">
                  Điều kiện cần hoàn tất trước khi chuyển hợp đồng:
                </p>
                <ul className="mt-1.5 space-y-1 text-amber-900">
                  {transfer.blockers.map((blocker) => (
                    <li key={blocker} className="flex items-start gap-1.5">
                      <span aria-hidden="true">☐</span>
                      <span>{blocker}</span>
                    </li>
                  ))}
                </ul>
              </div>
            )}

          {(isManager || isAdmin) &&
            transfer.status === "Pending" &&
            transfer.blockers.length === 0 && (
              <p className="rounded-lg bg-emerald-50 px-3 py-2 font-semibold text-emerald-900">
                ☑ Đã đủ mọi điều kiện để hoàn tất chuyển người đứng tên.
              </p>
            )}

          {transfer.status === "Pending" && (
            <div className="flex flex-wrap justify-end gap-2 pt-1">
              {transfer.canConfirm && (
                <button
                  type="button"
                  disabled={confirming}
                  onClick={() => void handleConfirm()}
                  className="rounded-lg bg-[#087775] px-3 py-1.5 font-bold text-white hover:bg-teal-800 disabled:opacity-50"
                >
                  {confirming ? "Đang xác nhận..." : "Đồng ý"}
                </button>
              )}

              {transfer.canCancel && (
                <button
                  type="button"
                  onClick={() => {
                    setCancelOpen(true)
                    setCancelError(null)
                  }}
                  className="rounded-lg border border-rose-200 bg-rose-50 px-3 py-1.5 font-bold text-rose-700 hover:bg-rose-100"
                >
                  Hủy đề nghị
                </button>
              )}

              {isManager && (
                <button
                  type="button"
                  disabled={!transfer.canComplete || completing}
                  onClick={() => setCompleteDialogOpen(true)}
                  className="rounded-lg bg-emerald-700 px-3 py-1.5 font-bold text-white hover:bg-emerald-800 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  Hoàn tất chuyển
                </button>
              )}
            </div>
          )}
        </div>
      )}

      {!loading && !loadError && !transfer && !isCreating && (
        <p className="mt-3 text-xs text-slate-500">
          Chưa có đề nghị chuyển người đứng tên cho hợp đồng này.
        </p>
      )}

      {isCreating && (
        <form
          noValidate
          onSubmit={(e) => void handleCreateTransfer(e)}
          aria-label="Biểu mẫu đề nghị chuyển người đứng tên"
          className="mt-3 space-y-3 rounded-xl border border-teal-200 bg-white p-4 text-xs"
        >
          <h5 className="text-sm font-bold text-slate-900">
            Đề nghị chuyển người đứng tên hợp đồng #{contract.contractId}
          </h5>
          <p className="rounded-lg bg-amber-50 px-3 py-2 text-amber-900">
            Tiền cọc {formatVnd(carryDepositAmount)} chuyển nguyên sang hợp đồng
            mới; phần cọc giữa hai bạn tự thanh toán với nhau.
          </p>

          <div className="grid gap-3 sm:grid-cols-2">
            <label className="block font-semibold text-slate-700">
              Email người nhận *
              <input
                type="email"
                value={newHolderEmail}
                onChange={(e) => {
                  setNewHolderEmail(e.target.value)
                  setFieldErrors((prev) => ({ ...prev, newHolderEmail: "" }))
                }}
                placeholder="nguoi.nhan@example.com"
                className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-1.5 text-xs text-slate-900"
              />
              {fieldErrors.newHolderEmail && (
                <span className="mt-1 block font-normal text-rose-600">
                  {fieldErrors.newHolderEmail}
                </span>
              )}
            </label>

            <label className="block font-semibold text-slate-700">
              Ngày hiệu lực (ngày 1 của tháng) *
              <select
                value={effectiveDate}
                onChange={(e) => {
                  setEffectiveDate(e.target.value)
                  setFieldErrors((prev) => ({ ...prev, effectiveDate: "" }))
                }}
                className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-1.5 text-xs text-slate-900"
              >
                {effectiveDateOptions.map((dateOpt) => (
                  <option key={dateOpt} value={dateOpt}>
                    {formatContractDate(dateOpt)} ({dateOpt})
                  </option>
                ))}
              </select>
              {fieldErrors.effectiveDate && (
                <span className="mt-1 block font-normal text-rose-600">
                  {fieldErrors.effectiveDate}
                </span>
              )}
            </label>
          </div>

          <label className="block font-semibold text-slate-700">
            Người ở cùng sẽ đứng tên (không bắt buộc)
            <select
              value={selectedOccupantId}
              onChange={(e) => setSelectedOccupantId(e.target.value)}
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-1.5 text-xs text-slate-900"
            >
              <option value="">Không chọn từ danh sách người ở cùng</option>
              {occupants.map((occ) => (
                <option
                  key={occ.contractOccupantId}
                  value={occ.contractOccupantId}
                >
                  {occ.fullName}
                  {occ.phoneNumber ? ` · ${occ.phoneNumber}` : ""}
                </option>
              ))}
            </select>
          </label>

          {formError && (
            <p
              role="alert"
              className="rounded-lg bg-rose-50 px-3 py-2 text-xs font-medium text-rose-800"
            >
              {formError}
            </p>
          )}

          <div className="flex justify-end gap-2">
            <button
              type="button"
              disabled={submittingCreate}
              onClick={() => setIsCreating(false)}
              className="rounded-lg border border-slate-300 px-3 py-1.5 font-semibold text-slate-700"
            >
              Hủy
            </button>
            <button
              type="submit"
              disabled={submittingCreate}
              className="rounded-lg bg-[#087775] px-3.5 py-1.5 font-bold text-white disabled:opacity-50"
            >
              {submittingCreate ? "Đang gửi..." : "Gửi đề nghị chuyển"}
            </button>
          </div>
        </form>
      )}

      {cancelOpen && transfer && (
        <form
          noValidate
          onSubmit={(e) => void handleCancelSubmit(e)}
          aria-label="Biểu mẫu hủy đề nghị chuyển hợp đồng"
          className="mt-3 space-y-2.5 rounded-xl border border-rose-200 bg-white p-4 text-xs"
        >
          <h5 className="text-sm font-bold text-slate-900">
            Hủy đề nghị chuyển #{transfer.contractTransferId}
          </h5>
          <label className="block font-semibold text-slate-700">
            Lý do hủy đề nghị
            <textarea
              rows={2}
              maxLength={MAX_TRANSFER_CANCEL_REASON_LENGTH}
              value={cancelReason}
              onChange={(e) => {
                setCancelReason(e.target.value)
                setCancelError(null)
              }}
              placeholder="Nhập lý do hủy (không bắt buộc, tối đa 500 ký tự)"
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-1.5 text-xs text-slate-900"
            />
          </label>
          {cancelError && (
            <p
              role="alert"
              className="rounded-lg bg-rose-50 px-3 py-2 text-xs font-medium text-rose-800"
            >
              {cancelError}
            </p>
          )}
          <div className="flex justify-end gap-2">
            <button
              type="button"
              disabled={cancelling}
              onClick={() => setCancelOpen(false)}
              className="rounded-lg border border-slate-300 px-3 py-1.5 font-semibold text-slate-700"
            >
              Đóng
            </button>
            <button
              type="submit"
              disabled={cancelling}
              className="rounded-lg bg-rose-600 px-3 py-1.5 font-bold text-white disabled:opacity-50"
            >
              {cancelling ? "Đang hủy..." : "Xác nhận hủy đề nghị"}
            </button>
          </div>
        </form>
      )}

      {completeDialogOpen && transfer && (
        <div
          role="dialog"
          aria-modal="true"
          aria-label="Xác nhận hoàn tất chuyển người đứng tên"
          className="mt-3 rounded-xl border border-emerald-300 bg-white p-4 shadow-md"
        >
          <h5 className="text-sm font-bold text-slate-900">
            Xác nhận hoàn tất chuyển người đứng tên
          </h5>
          <p className="mt-1.5 text-xs text-slate-700">
            Hợp đồng #{transfer.fromContractId} (Phòng {transfer.roomCode} ·{" "}
            {transfer.propertyName}) sẽ chuyển từ{" "}
            <strong>{transfer.oldHolderName}</strong> sang{" "}
            <strong>{transfer.newHolderName}</strong>.
          </p>
          <dl className="mt-2.5 space-y-1 rounded-lg bg-slate-50 p-3 text-xs">
            <div className="flex justify-between gap-2">
              <dt className="text-slate-600">Ngày kết thúc hợp đồng cũ:</dt>
              <dd className="font-bold text-slate-900">
                {formatContractDate(
                  getOldContractEndDateOnTransfer(transfer.effectiveDate),
                )}
              </dd>
            </div>
            <div className="flex justify-between gap-2">
              <dt className="text-slate-600">Ngày bắt đầu hợp đồng mới:</dt>
              <dd className="font-bold text-emerald-800">
                {formatContractDate(transfer.effectiveDate)}
              </dd>
            </div>
            <div className="flex justify-between gap-2">
              <dt className="text-slate-600">Tiền cọc chuyển nguyên sang:</dt>
              <dd className="font-bold text-slate-900">
                {formatVnd(transfer.depositCarriedOver)}
              </dd>
            </div>
          </dl>
          <div className="mt-3 flex justify-end gap-2">
            <button
              type="button"
              disabled={completing}
              onClick={() => setCompleteDialogOpen(false)}
              className="rounded-lg border border-slate-300 px-3 py-1.5 text-xs font-semibold text-slate-700"
            >
              Hủy
            </button>
            <button
              type="button"
              disabled={completing}
              onClick={() => void handleCompleteTransfer()}
              className="rounded-lg bg-emerald-700 px-3.5 py-1.5 text-xs font-bold text-white disabled:opacity-50"
            >
              {completing ? "Đang hoàn tất..." : "Xác nhận hoàn tất chuyển"}
            </button>
          </div>
        </div>
      )}
    </section>
  )
}
