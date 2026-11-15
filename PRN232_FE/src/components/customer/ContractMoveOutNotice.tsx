import { useEffect, useState, type FormEvent } from "react"
import { ApiError } from "@/api/apiClient"
import { useAuth } from "@/auth/AuthContext"
import {
  cancelContractMoveOutNotice,
  saveContractMoveOutNotice,
} from "@/services/contractMoveOutNoticeService"
import type { ContractDto } from "@/types/contract"
import { getVietnamTodayIsoDate } from "@/utils/equipmentValidation"
import { formatContractDate } from "@/utils/managementContracts"
import {
  evaluateMoveOutDepositWarning,
  getLatestNoticeDateToKeepDeposit,
  validatePlannedMoveOutDate,
} from "@/utils/moveOutNoticeValidation"

export default function ContractMoveOutNotice({
  contract,
  onContractUpdated,
}: {
  contract: ContractDto
  onContractUpdated?: (updated: ContractDto) => void
}) {
  const { accessToken, user } = useAuth()
  const normalizedRole = user?.role?.toUpperCase() ?? "CUSTOMER"
  const isAdmin = normalizedRole === "ADMIN"

  const [contractState, setContractState] = useState<ContractDto>(contract)
  const [isEditing, setIsEditing] = useState(false)
  const [plannedDate, setPlannedDate] = useState("")
  const [formError, setFormError] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const [successMessage, setSuccessMessage] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [canceling, setCanceling] = useState(false)

  useEffect(() => {
    setContractState(contract)
  }, [contract])

  if (!accessToken) return null

  const today = getVietnamTodayIsoDate()
  const isContractActive =
    contractState.status === "Active" &&
    contractState.displayStatus !== "Terminated" &&
    contractState.displayStatus !== "Ended"
  const canManageNotice = isContractActive && !isAdmin

  const hasMoveOutNotice = Boolean(contractState.plannedMoveOutDate)
  const savedLatestNoticeDate =
    contractState.plannedMoveOutDate &&
    contractState.plannedMoveOutDate < contractState.endDate
      ? getLatestNoticeDateToKeepDeposit(contractState.plannedMoveOutDate)
      : null

  const evaluation = isEditing
    ? evaluateMoveOutDepositWarning({
        plannedMoveOutDate: plannedDate,
        endDate: contractState.endDate,
        today,
      })
    : null

  function openForm() {
    const defaultDate =
      contractState.plannedMoveOutDate &&
      contractState.plannedMoveOutDate >= today
        ? contractState.plannedMoveOutDate
        : contractState.endDate >= today
          ? contractState.endDate
          : today
    setPlannedDate(defaultDate)
    setFormError(null)
    setActionError(null)
    setSuccessMessage(null)
    setIsEditing(true)
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    if (!accessToken) return

    const validationError = validatePlannedMoveOutDate({
      plannedMoveOutDate: plannedDate,
      endDate: contractState.endDate,
      today,
    })
    if (validationError) {
      setFormError(validationError)
      return
    }

    setSubmitting(true)
    setFormError(null)
    setActionError(null)
    setSuccessMessage(null)

    try {
      const updated = await saveContractMoveOutNotice(
        accessToken,
        contractState.contractId,
        { plannedMoveOutDate: plannedDate.trim() },
      )
      setContractState(updated)
      onContractUpdated?.(updated)
      setIsEditing(false)
      setSuccessMessage("Đã cập nhật thông báo trả phòng.")
    } catch (err) {
      const message =
        err instanceof ApiError || err instanceof Error
          ? err.message
          : "Không thể gửi thông báo trả phòng."
      setFormError(message)
    } finally {
      setSubmitting(false)
    }
  }

  async function handleCancelNotice() {
    if (!accessToken) return
    setCanceling(true)
    setActionError(null)
    setSuccessMessage(null)

    try {
      const updated = await cancelContractMoveOutNotice(
        accessToken,
        contractState.contractId,
      )
      setContractState(updated)
      onContractUpdated?.(updated)
      setIsEditing(false)
      setSuccessMessage("Đã hủy báo trả phòng.")
    } catch (err) {
      const message =
        err instanceof ApiError || err instanceof Error
          ? err.message
          : "Không thể hủy thông báo trả phòng."
      setActionError(message)
    } finally {
      setCanceling(false)
    }
  }

  return (
    <div
      className="mt-5 rounded-xl border border-slate-200 bg-slate-50/70 p-4"
      aria-label={`Thông báo trả phòng hợp đồng ${contractState.contractId}`}
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h4 className="text-sm font-bold text-slate-900">
            Thông báo trả phòng (quy định báo trước 30 ngày)
          </h4>
          <p className="mt-0.5 text-xs text-slate-600">
            Trả phòng trước ngày kết thúc hợp đồng (
            {formatContractDate(contractState.endDate)}) mà báo trước dưới 30
            ngày sẽ mất phần tiền cọc còn lại khi quyết toán.
          </p>
        </div>

        {canManageNotice && !hasMoveOutNotice && !isEditing && (
          <button
            type="button"
            onClick={openForm}
            className="rounded-lg bg-[#087775] px-3 py-1.5 text-xs font-bold text-white hover:bg-teal-800"
          >
            Báo trả phòng
          </button>
        )}
      </div>

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

      {hasMoveOutNotice && contractState.plannedMoveOutDate ? (
        <div className="mt-3 rounded-xl border border-slate-200 bg-white p-3.5 text-xs">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div>
              <p className="text-sm font-bold text-slate-900">
                Dự kiến trả phòng{" "}
                {formatContractDate(contractState.plannedMoveOutDate)} · báo
                trước {contractState.moveOutNoticeDays ?? 0} ngày{" "}
                <span className="text-xs font-normal text-slate-500">
                  ({contractState.plannedMoveOutDate})
                </span>
              </p>
              {savedLatestNoticeDate && (
                <p className="mt-1 text-xs text-slate-600">
                  Ngày báo muộn nhất để giữ cọc:{" "}
                  <strong className="font-semibold text-slate-800">
                    {formatContractDate(savedLatestNoticeDate)}
                  </strong>{" "}
                  ({savedLatestNoticeDate})
                </p>
              )}
            </div>

            {contractState.moveOutForfeitsDeposit && (
              <span className="inline-flex items-center rounded-full border border-rose-200 bg-rose-100 px-2.5 py-1 text-xs font-bold text-rose-800">
                Báo trước dưới 30 ngày — Sẽ mất phần cọc còn lại
              </span>
            )}
          </div>

          {canManageNotice && !isEditing && (
            <div className="mt-3 flex flex-wrap gap-2">
              <button
                type="button"
                disabled={canceling}
                onClick={openForm}
                className="rounded-lg border border-[#087775] px-3 py-1.5 text-xs font-bold text-[#087775] hover:bg-teal-50 disabled:opacity-50"
              >
                Đổi ngày
              </button>
              <button
                type="button"
                disabled={canceling}
                onClick={() => void handleCancelNotice()}
                className="rounded-lg border border-rose-200 bg-rose-50 px-3 py-1.5 text-xs font-bold text-rose-700 hover:bg-rose-100 disabled:opacity-50"
              >
                {canceling ? "Đang hủy..." : "Hủy báo trả phòng"}
              </button>
            </div>
          )}
        </div>
      ) : (
        !isEditing && (
          <p className="mt-3 text-xs text-slate-500">
            Chưa có thông báo trả phòng.
          </p>
        )
      )}

      {isEditing && (
        <form
          noValidate
          onSubmit={(e) => void handleSubmit(e)}
          aria-label="Biểu mẫu báo trả phòng"
          className="mt-3 space-y-3 rounded-xl border border-teal-200 bg-white p-4"
        >
          <div className="max-w-xs">
            <label className="block text-xs font-semibold text-slate-700">
              Ngày dự kiến trả phòng *
              <input
                type="date"
                min={today}
                max={contractState.endDate}
                value={plannedDate}
                aria-label="Ngày dự kiến trả phòng"
                onChange={(e) => {
                  setPlannedDate(e.target.value)
                  setFormError(null)
                }}
                className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-1.5 text-sm text-slate-900"
              />
            </label>
            <p className="mt-1 text-[11px] text-slate-500">
              Chọn từ hôm nay ({formatContractDate(today)}) đến hết ngày kết
              thúc hợp đồng ({formatContractDate(contractState.endDate)}).
            </p>
          </div>

          {evaluation && evaluation.forfeitsDeposit && evaluation.warningMessage && (
            <div
              role="alert"
              className="rounded-lg border border-rose-300 bg-rose-50 px-3.5 py-2.5 text-xs text-rose-900"
            >
              <p className="font-bold text-rose-900">
                {evaluation.warningMessage}
              </p>
              <p className="mt-1 text-rose-800">
                Ngày báo muộn nhất để giữ cọc:{" "}
                <strong>
                  {formatContractDate(evaluation.latestNoticeDate)}
                </strong>{" "}
                ({evaluation.latestNoticeDate}) · Hiện chỉ báo trước{" "}
                <strong>{evaluation.noticeDays} ngày</strong>.
              </p>
            </div>
          )}

          {evaluation &&
            !evaluation.forfeitsDeposit &&
            plannedDate < contractState.endDate && (
              <div className="rounded-lg border border-emerald-200 bg-emerald-50 px-3.5 py-2.5 text-xs text-emerald-900">
                <p className="font-semibold">
                  Báo trước {evaluation.noticeDays} ngày (đủ tối thiểu 30 ngày
                  để giữ quyền hoàn cọc).
                </p>
                <p className="mt-0.5 text-emerald-800">
                  Ngày báo muộn nhất để giữ cọc cho ngày trả phòng này:{" "}
                  <strong>
                    {formatContractDate(evaluation.latestNoticeDate)}
                  </strong>{" "}
                  ({evaluation.latestNoticeDate}).
                </p>
              </div>
            )}

          {evaluation && plannedDate === contractState.endDate && (
            <div className="rounded-lg border border-slate-200 bg-slate-50 px-3.5 py-2 text-xs text-slate-700">
              Trả phòng đúng ngày kết thúc hợp đồng (
              {formatContractDate(contractState.endDate)}) không cần báo trước
              30 ngày và vẫn giữ quyền hoàn cọc.
            </div>
          )}

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
              disabled={submitting}
              onClick={() => {
                setIsEditing(false)
                setFormError(null)
              }}
              className="rounded-lg border border-slate-300 px-3 py-1.5 text-xs font-semibold text-slate-700"
            >
              Đóng
            </button>
            <button
              type="submit"
              disabled={submitting}
              className="rounded-lg bg-[#087775] px-3.5 py-1.5 text-xs font-bold text-white disabled:opacity-50"
            >
              {submitting ? "Đang gửi..." : "Xác nhận báo trả phòng"}
            </button>
          </div>
        </form>
      )}
    </div>
  )
}
