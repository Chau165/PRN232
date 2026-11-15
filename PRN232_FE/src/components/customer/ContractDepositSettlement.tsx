import { useEffect, useState } from "react"
import { ApiError } from "@/api/apiClient"
import { useAuth } from "@/auth/AuthContext"
import {
  confirmContractDepositSettlement,
  getContractDepositSettlement,
} from "@/services/contractSettlementService"
import type { ContractDto, DepositSettlementDto } from "@/types/contract"
import { formatEvidenceUploadedAt } from "@/utils/contractEvidenceValidation"
import {
  formatForfeitedDepositReason,
  requiresDepositSettlement,
  validateDepositSettlementConfirmation,
} from "@/utils/depositSettlementValidation"
import { formatContractDate } from "@/utils/managementContracts"
import { formatVnd } from "@/utils/publicFormatting"

export default function ContractDepositSettlement({
  contract,
  refreshTrigger = 0,
  onSettlementStatusChange,
}: {
  contract: ContractDto
  refreshTrigger?: number
  onSettlementStatusChange?: (isSettled: boolean) => void
}) {
  const { accessToken, user } = useAuth()
  const isManager = user?.role?.toUpperCase() === "MANAGER"

  const [settlement, setSettlement] = useState<DepositSettlementDto | null>(
    null,
  )
  const [loading, setLoading] = useState(false)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [isPrerequisiteMissing, setIsPrerequisiteMissing] = useState(false)
  const [retryCount, setRetryCount] = useState(0)

  const [outstandingDebtAcknowledged, setOutstandingDebtAcknowledged] =
    useState(false)
  const [note, setNote] = useState("")
  const [confirmOpen, setConfirmOpen] = useState(false)
  const [submitError, setSubmitError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  const hasSecurityDeposit = requiresDepositSettlement(contract)

  useEffect(() => {
    if (!accessToken || !hasSecurityDeposit) return
    const controller = new AbortController()
    setLoading(true)
    setLoadError(null)
    setIsPrerequisiteMissing(false)

    Promise.resolve(
      getContractDepositSettlement(
        accessToken,
        contract.contractId,
        controller.signal,
      ),
    )
      .then((res) => {
        if (controller.signal.aborted || !res) return
        setSettlement(res)
        setNote(res.note ?? "")
        onSettlementStatusChange?.(res.isSettled)
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return
        if (err instanceof ApiError && err.status === 409) {
          setIsPrerequisiteMissing(true)
          setLoadError(
            err.message ||
              "Cần lập biên bản trả phòng trước khi quyết toán cọc.",
          )
          onSettlementStatusChange?.(false)
          return
        }
        setLoadError(
          err instanceof Error
            ? err.message
            : "Không thể tải thông tin quyết toán cọc.",
        )
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setLoading(false)
        }
      })

    return () => controller.abort()
  }, [
    accessToken,
    contract.contractId,
    hasSecurityDeposit,
    refreshTrigger,
    retryCount,
  ])

  if (!accessToken || !hasSecurityDeposit) return null

  function handleOpenConfirm() {
    if (!settlement) return
    const validationErr = validateDepositSettlementConfirmation({
      outstandingDebt: settlement.outstandingDebt,
      outstandingDebtAcknowledged,
    })
    if (validationErr) {
      setSubmitError(validationErr)
      return
    }
    setSubmitError(null)
    setConfirmOpen(true)
  }

  async function handleConfirmSettlement() {
    if (!accessToken || !settlement || submitting) return
    const validationErr = validateDepositSettlementConfirmation({
      outstandingDebt: settlement.outstandingDebt,
      outstandingDebtAcknowledged,
    })
    if (validationErr) {
      setSubmitError(validationErr)
      setConfirmOpen(false)
      return
    }

    setSubmitting(true)
    setSubmitError(null)
    try {
      const updated = await confirmContractDepositSettlement(
        accessToken,
        contract.contractId,
        {
          outstandingDebtAcknowledged,
          note: note.trim() || null,
        },
      )
      setSettlement(updated)
      setConfirmOpen(false)
      onSettlementStatusChange?.(updated.isSettled)
    } catch (err: unknown) {
      setSubmitError(
        err instanceof Error
          ? err.message
          : "Không thể xác nhận quyết toán cọc.",
      )
      setConfirmOpen(false)
    } finally {
      setSubmitting(false)
    }
  }

  const forfeitedReason = settlement
    ? formatForfeitedDepositReason(settlement)
    : null

  return (
    <section
      className="mt-5 rounded-xl border border-slate-200 bg-slate-50/70 p-4 text-sm"
      aria-label={`Quyết toán cọc hợp đồng ${contract.contractId}`}
    >
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div>
          <h4 className="font-bold text-slate-900">Quyết toán cọc</h4>
          <p className="mt-0.5 text-xs text-slate-600">
            Thứ tự trả phòng bắt buộc: (1) Lập biên bản trả phòng → (2) Quyết
            toán cọc → (3) Xác nhận trả phòng.
          </p>
        </div>
        {settlement && (
          <span
            className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-bold ${
              settlement.isSettled
                ? "bg-emerald-100 text-emerald-800"
                : "bg-amber-100 text-amber-900"
            }`}
          >
            {settlement.isSettled
              ? "Đã quyết toán"
              : "Bản xem trước (Chưa quyết toán)"}
          </span>
        )}
      </div>

      {loading && (
        <p role="status" className="mt-3 text-xs text-slate-500">
          Đang tải bảng quyết toán cọc...
        </p>
      )}

      {!loading && loadError && (
        <div
          role={isPrerequisiteMissing ? "status" : "alert"}
          className={`mt-3 rounded-lg px-3.5 py-2.5 text-xs ${
            isPrerequisiteMissing
              ? "border border-amber-200 bg-amber-50 text-amber-900"
              : "bg-rose-50 text-rose-700"
          }`}
        >
          <p className="font-medium">{loadError}</p>
          {!isPrerequisiteMissing && (
            <button
              type="button"
              onClick={() => setRetryCount((c) => c + 1)}
              className="mt-1.5 font-semibold underline"
            >
              Thử lại
            </button>
          )}
        </div>
      )}

      {!loading && !loadError && settlement && (
        <div className="mt-3 space-y-4">
          <div className="rounded-xl border border-slate-200 bg-white p-4">
            <div className="mb-3 flex flex-wrap items-center justify-between gap-2 text-xs text-slate-600">
              <span>
                Ngày trả phòng:{" "}
                <strong className="text-slate-900">
                  {formatContractDate(settlement.moveOutDate)}
                </strong>{" "}
                · Ngày kết thúc HĐ:{" "}
                <strong className="text-slate-900">
                  {formatContractDate(settlement.contractEndDate)}
                </strong>
              </span>
              {settlement.isSettled && settlement.settledByName && (
                <span>
                  Người quyết toán: <strong>{settlement.settledByName}</strong>
                  {settlement.settledAtUtc
                    ? ` · ${formatEvidenceUploadedAt(settlement.settledAtUtc)}`
                    : ""}
                </span>
              )}
            </div>

            <div className="overflow-x-auto">
              <table
                className="w-full border-collapse text-left text-xs"
                aria-label="Bảng quyết toán cọc"
              >
                <tbody className="divide-y divide-slate-100">
                  <tr>
                    <th
                      scope="row"
                      className="py-2 pr-4 font-semibold text-slate-700"
                    >
                      Tiền cọc
                    </th>
                    <td className="py-2 text-right font-semibold text-slate-900">
                      {formatVnd(settlement.depositAmount)}
                    </td>
                  </tr>
                  <tr>
                    <th
                      scope="row"
                      className="py-2 pr-4 font-semibold text-slate-700"
                    >
                      Bồi thường thiết bị
                      {settlement.damageCoveredByDeposit > 0 && (
                        <span className="ml-1 font-normal text-slate-500">
                          (trừ từ cọc{" "}
                          {formatVnd(settlement.damageCoveredByDeposit)})
                        </span>
                      )}
                    </th>
                    <td className="py-2 text-right font-semibold text-slate-900">
                      {formatVnd(settlement.damageCharges)}
                    </td>
                  </tr>
                  <tr>
                    <th
                      scope="row"
                      className="py-2 pr-4 font-semibold text-slate-700"
                    >
                      Giữ bù nợ
                      {settlement.outstandingDebt > 0 && (
                        <span className="ml-1 font-normal text-slate-500">
                          (công nợ hóa đơn{" "}
                          {formatVnd(settlement.outstandingDebt)})
                        </span>
                      )}
                    </th>
                    <td className="py-2 text-right font-semibold text-slate-900">
                      {formatVnd(settlement.retainedForDebt)}
                    </td>
                  </tr>
                  <tr>
                    <th
                      scope="row"
                      className="py-2 pr-4 font-semibold text-slate-700"
                    >
                      Mất cọc
                      {forfeitedReason && (
                        <span className="ml-1.5 inline-block rounded bg-rose-100 px-1.5 py-0.5 text-[11px] font-medium text-rose-800">
                          {forfeitedReason}
                        </span>
                      )}
                    </th>
                    <td className="py-2 text-right font-semibold text-rose-700">
                      {formatVnd(settlement.forfeitedAmount)}
                    </td>
                  </tr>
                  <tr className="bg-emerald-50/70">
                    <th
                      scope="row"
                      className="px-2 py-2.5 pr-4 font-bold text-emerald-950"
                    >
                      Hoàn lại
                    </th>
                    <td className="px-2 py-2.5 text-right text-sm font-bold text-emerald-800">
                      {formatVnd(settlement.refundAmount)}
                    </td>
                  </tr>
                  <tr className="bg-amber-50/70">
                    <th
                      scope="row"
                      className="px-2 py-2.5 pr-4 font-bold text-amber-950"
                    >
                      Khách nộp thêm
                    </th>
                    <td className="px-2 py-2.5 text-right text-sm font-bold text-rose-700">
                      {formatVnd(settlement.amountDue)}
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>

            {settlement.isSettled && settlement.note && (
              <p className="mt-3 rounded-lg bg-slate-50 px-3 py-2 text-xs text-slate-700">
                Ghi chú: {settlement.note}
              </p>
            )}
          </div>

          {isManager && !settlement.isSettled && (
            <div className="space-y-3 rounded-xl border border-teal-200 bg-white p-4 text-xs">
              {settlement.outstandingDebt > 0 && (
                <div className="space-y-2 rounded-lg border border-amber-300 bg-amber-50 p-3 text-amber-950">
                  <p className="font-semibold">
                    Khách còn nợ hóa đơn {formatVnd(settlement.outstandingDebt)}
                    . Nên thu hết trước khi quyết toán; nếu khách không trả, hãy
                    xác nhận để giữ lại cọc bù nợ.
                  </p>
                  <label className="flex items-start gap-2 font-semibold">
                    <input
                      type="checkbox"
                      checked={outstandingDebtAcknowledged}
                      onChange={(e) => {
                        setOutstandingDebtAcknowledged(e.target.checked)
                        setSubmitError(null)
                      }}
                      className="mt-0.5"
                    />
                    <span>Khách không trả, giữ cọc bù nợ</span>
                  </label>
                </div>
              )}

              <label className="block font-semibold text-slate-700">
                Ghi chú quyết toán
                <input
                  type="text"
                  value={note}
                  maxLength={500}
                  onChange={(e) => setNote(e.target.value)}
                  placeholder="Ghi chú quyết toán cọc (không bắt buộc)"
                  className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-1.5 text-xs text-slate-900"
                />
              </label>

              {submitError && (
                <p
                  role="alert"
                  className="rounded-lg bg-rose-50 px-3 py-2 text-xs font-medium text-rose-800"
                >
                  {submitError}
                </p>
              )}

              <div className="flex justify-end">
                <button
                  type="button"
                  disabled={submitting}
                  onClick={handleOpenConfirm}
                  className="rounded-lg bg-[#087775] px-3.5 py-2 text-xs font-bold text-white hover:bg-teal-800 disabled:opacity-50"
                >
                  Xác nhận quyết toán
                </button>
              </div>
            </div>
          )}

          {confirmOpen && !settlement.isSettled && (
            <div
              role="dialog"
              aria-modal="true"
              aria-label="Hộp xác nhận quyết toán cọc"
              className="rounded-xl border border-teal-300 bg-white p-4 shadow-md"
            >
              <h5 className="text-sm font-bold text-slate-900">
                Xác nhận quyết toán cọc hợp đồng #{contract.contractId}
              </h5>
              <p className="mt-1 text-xs text-slate-600">
                Sau khi quyết toán, biên bản trả phòng và ảnh trả phòng sẽ bị
                khóa, đồng thời mở khóa nút xác nhận trả phòng.
              </p>
              <dl className="mt-3 space-y-1.5 rounded-lg bg-slate-50 p-3 text-xs">
                <div className="flex justify-between">
                  <dt className="text-slate-600">Hoàn lại cho khách:</dt>
                  <dd className="font-bold text-emerald-800">
                    {formatVnd(settlement.refundAmount)}
                  </dd>
                </div>
                <div className="flex justify-between">
                  <dt className="text-slate-600">Khách nộp thêm:</dt>
                  <dd className="font-bold text-rose-700">
                    {formatVnd(settlement.amountDue)}
                  </dd>
                </div>
                {settlement.retainedForDebt > 0 && (
                  <div className="flex justify-between">
                    <dt className="text-slate-600">Giữ bù nợ hóa đơn:</dt>
                    <dd className="font-semibold text-amber-900">
                      {formatVnd(settlement.retainedForDebt)}
                    </dd>
                  </div>
                )}
              </dl>
              <div className="mt-3 flex justify-end gap-2">
                <button
                  type="button"
                  disabled={submitting}
                  onClick={() => setConfirmOpen(false)}
                  className="rounded-lg border border-slate-300 px-3 py-1.5 text-xs font-semibold text-slate-700"
                >
                  Hủy
                </button>
                <button
                  type="button"
                  disabled={submitting}
                  onClick={() => void handleConfirmSettlement()}
                  className="rounded-lg bg-[#087775] px-3.5 py-1.5 text-xs font-bold text-white disabled:opacity-50"
                >
                  {submitting ? "Đang quyết toán..." : "Đồng ý quyết toán"}
                </button>
              </div>
            </div>
          )}
        </div>
      )}
    </section>
  )
}
