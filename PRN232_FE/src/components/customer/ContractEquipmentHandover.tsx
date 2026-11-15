import { useEffect, useState, type FormEvent } from "react"
import { ApiError } from "@/api/apiClient"
import { useAuth } from "@/auth/AuthContext"
import {
  getContractHandovers,
  saveContractMoveInHandover,
  saveContractMoveOutHandover,
} from "@/services/contractHandoverService"
import type {
  ContractDto,
  ContractHandoversDto,
  EquipmentCondition,
  HandoverKind,
  HandoverRecordDto,
} from "@/types/contract"
import {
  formatEquipmentSummary,
  getVietnamTodayIsoDate,
} from "@/utils/equipmentValidation"
import {
  canChargeForHandoverItem,
  EQUIPMENT_CONDITIONS,
  formatConditionLabel,
  handoverChargeAmountError,
  handoverConditionError,
  handoverDateError,
  isDamageOrMissingCondition,
  splitHandoverValidationErrors,
} from "@/utils/handoverValidation"
import { formatContractDate } from "@/utils/managementContracts"
import { formatVnd } from "@/utils/publicFormatting"

interface EditableHandoverRow {
  equipmentId: number
  equipmentName: string
  quantity: number
  compensationValue: number
  condition: EquipmentCondition
  chargeAmount: string
  note: string
  moveInCondition: EquipmentCondition | string | null
}

function buildEditableRows(
  kind: HandoverKind,
  handovers: ContractHandoversDto,
): EditableHandoverRow[] {
  const existingRecord =
    kind === "MoveIn" ? handovers.moveIn : handovers.moveOut
  const existingByEquipment = new Map(
    (existingRecord?.items ?? []).map((item) => [item.equipmentId, item]),
  )
  const moveInByEquipment = new Map(
    (handovers.moveIn?.items ?? []).map((item) => [item.equipmentId, item]),
  )

  // Biên bản trả phòng ưu tiên giá trị đã chụp lúc nhận phòng, bổ sung thêm thiết bị phòng nếu có.
  const baseEquipments =
    kind === "MoveOut" && handovers.moveIn && handovers.moveIn.items.length > 0
      ? [
          ...handovers.moveIn.items.map((item) => ({
            equipmentId: item.equipmentId,
            name: item.equipmentName,
            quantity: item.quantity,
            compensationValue: item.compensationValue,
          })),
          ...handovers.roomEquipments.filter(
            (eq) => !moveInByEquipment.has(eq.equipmentId),
          ),
        ]
      : handovers.roomEquipments

  return baseEquipments.map((eq) => {
    const existing = existingByEquipment.get(eq.equipmentId)
    const moveInItem = moveInByEquipment.get(eq.equipmentId)
    const moveInCondition =
      existing?.moveInCondition ?? moveInItem?.condition ?? null
    const defaultCondition: EquipmentCondition =
      (existing?.condition as EquipmentCondition | undefined) ??
      (kind === "MoveOut" && moveInCondition
        ? (moveInCondition as EquipmentCondition)
        : "Good")

    return {
      equipmentId: eq.equipmentId,
      equipmentName: eq.name,
      quantity: eq.quantity,
      compensationValue: eq.compensationValue,
      condition: defaultCondition,
      chargeAmount:
        kind === "MoveOut" && existing ? String(existing.chargeAmount) : "0",
      note: existing?.note ?? "",
      moveInCondition,
    }
  })
}

export default function ContractEquipmentHandover({
  contract,
  isDepositSettled = false,
  onMoveOutSaved,
}: {
  contract: ContractDto
  isDepositSettled?: boolean
  onMoveOutSaved?: () => void
}) {
  const { accessToken, user } = useAuth()
  const isManager = user?.role === "MANAGER"
  const [data, setData] = useState<ContractHandoversDto | null>(null)
  const [loading, setLoading] = useState(false)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [retryCount, setRetryCount] = useState(0)

  const [editingKind, setEditingKind] = useState<HandoverKind | null>(null)
  const [handoverDate, setHandoverDate] = useState<string>("")
  const [recordNote, setRecordNote] = useState<string>("")
  const [rows, setRows] = useState<EditableHandoverRow[]>([])
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const [generalError, setGeneralError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    if (!accessToken) return
    const controller = new AbortController()
    setLoading(true)
    setLoadError(null)
    setEditingKind(null)

    Promise.resolve(
      getContractHandovers(accessToken, contract.contractId, controller.signal),
    )
      .then((res) => {
        if (!controller.signal.aborted && res) {
          setData(res)
        }
      })
      .catch((err: unknown) => {
        if (!controller.signal.aborted) {
          setLoadError(
            err instanceof Error
              ? err.message
              : "Không thể tải biên bản bàn giao thiết bị.",
          )
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })

    return () => controller.abort()
  }, [accessToken, contract.contractId, retryCount])

  const startEditing = (kind: HandoverKind) => {
    if (!data) return
    const existing = kind === "MoveIn" ? data.moveIn : data.moveOut
    setEditingKind(kind)
    setHandoverDate(existing?.handoverDate ?? getVietnamTodayIsoDate())
    setRecordNote(existing?.note ?? "")
    setRows(buildEditableRows(kind, data))
    setFieldErrors({})
    setGeneralError(null)
  }

  const cancelEditing = () => {
    if (saving) return
    setEditingKind(null)
    setFieldErrors({})
    setGeneralError(null)
  }

  const updateRowCondition = (index: number, nextCondition: EquipmentCondition) => {
    setRows((prev) =>
      prev.map((row, idx) => {
        if (idx !== index) return row
        const canCharge =
          editingKind !== null &&
          canChargeForHandoverItem(
            editingKind,
            nextCondition,
            row.moveInCondition,
          )
        return {
          ...row,
          condition: nextCondition,
          chargeAmount: canCharge ? row.chargeAmount : "0",
        }
      }),
    )
    setFieldErrors((prev) => {
      const next = { ...prev }
      delete next[`items[${index}].condition`]
      delete next[`items[${index}].chargeAmount`]
      return next
    })
  }

  const updateRowChargeAmount = (index: number, value: string) => {
    setRows((prev) =>
      prev.map((row, idx) =>
        idx === index ? { ...row, chargeAmount: value } : row,
      ),
    )
    setFieldErrors((prev) => {
      const next = { ...prev }
      delete next[`items[${index}].chargeAmount`]
      return next
    })
  }

  const updateRowNote = (index: number, value: string) => {
    setRows((prev) =>
      prev.map((row, idx) => (idx === index ? { ...row, note: value } : row)),
    )
    setFieldErrors((prev) => {
      const next = { ...prev }
      delete next[`items[${index}].note`]
      return next
    })
  }

  const submitForm = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!accessToken || !editingKind || saving) return

    const nextFieldErrors: Record<string, string> = {}
    const dateErr = handoverDateError(handoverDate)
    if (dateErr) nextFieldErrors.handoverDate = dateErr

    rows.forEach((row, idx) => {
      const condErr = handoverConditionError(row.condition)
      if (condErr) nextFieldErrors[`items[${idx}].condition`] = condErr

      const chargeErr = handoverChargeAmountError({
        kind: editingKind,
        equipmentName: row.equipmentName,
        quantity: row.quantity,
        compensationValue: row.compensationValue,
        condition: row.condition,
        moveInCondition: row.moveInCondition,
        chargeAmount: editingKind === "MoveOut" ? row.chargeAmount : 0,
      })
      if (chargeErr) nextFieldErrors[`items[${idx}].chargeAmount`] = chargeErr
    })

    if (Object.keys(nextFieldErrors).length > 0) {
      setFieldErrors(nextFieldErrors)
      setGeneralError(null)
      return
    }

    setSaving(true)
    setFieldErrors({})
    setGeneralError(null)
    try {
      const payload = {
        handoverDate: handoverDate.trim(),
        note: recordNote.trim() || null,
        items: rows.map((row) => {
          const canCharge = canChargeForHandoverItem(
            editingKind,
            row.condition,
            row.moveInCondition,
          )
          const parsedCharge = canCharge ? Number(row.chargeAmount || "0") : 0
          return {
            equipmentId: row.equipmentId,
            condition: row.condition,
            chargeAmount: Number.isFinite(parsedCharge) ? parsedCharge : 0,
            note: row.note.trim() || null,
          }
        }),
      }
      const saved =
        editingKind === "MoveIn"
          ? await saveContractMoveInHandover(
              accessToken,
              contract.contractId,
              payload,
            )
          : await saveContractMoveOutHandover(
              accessToken,
              contract.contractId,
              payload,
            )
      setData(saved)
      if (editingKind === "MoveOut") {
        onMoveOutSaved?.()
      }
      setEditingKind(null)
    } catch (saveErr: unknown) {
      if (saveErr instanceof ApiError && saveErr.status === 400 && saveErr.validationErrors) {
        const { fieldErrors: mapped, generalError: mappedGeneral } =
          splitHandoverValidationErrors(saveErr.validationErrors, saveErr.message)
        setFieldErrors(mapped)
        setGeneralError(mappedGeneral || null)
      } else if (saveErr instanceof ApiError && saveErr.status === 409) {
        setGeneralError(
          saveErr.message ||
            "Không thể sửa biên bản nhận phòng khi đã có biên bản trả phòng.",
        )
      } else {
        setGeneralError(
          saveErr instanceof Error
            ? saveErr.message
            : "Không thể lưu biên bản bàn giao.",
        )
      }
    } finally {
      setSaving(false)
    }
  }

  const liveTotalCharge =
    editingKind === "MoveOut"
      ? rows.reduce((sum, row) => {
          if (
            !canChargeForHandoverItem(
              "MoveOut",
              row.condition,
              row.moveInCondition,
            )
          ) {
            return sum
          }
          const n = Number(row.chargeAmount || "0")
          return sum + (Number.isFinite(n) && n > 0 ? n : 0)
        }, 0)
      : 0

  return (
    <section
      className="mt-5 rounded-xl bg-slate-50 p-5 text-sm"
      aria-label="Biên bản bàn giao"
    >
      <h3 className="font-bold text-slate-800">Biên bản bàn giao</h3>
      {loading && (
        <p role="status" className="mt-2 text-slate-500">
          Đang tải biên bản bàn giao...
        </p>
      )}
      {loadError && (
        <p role="alert" className="mt-2 text-rose-600">
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

      {!loading && !loadError && data && (
        <div className="mt-3 space-y-4">
          <div className="grid gap-4 md:grid-cols-2">
            <HandoverRecordCard
              title="Biên bản nhận phòng"
              kind="MoveIn"
              record={data.moveIn}
              isManager={isManager}
              canEdit={
                isManager &&
                data.moveOut === null &&
                (data.roomEquipments.length > 0 || data.moveIn !== null)
              }
              lockedReason={
                isManager && data.moveOut !== null
                  ? "Đã có biên bản trả phòng nên không thể sửa biên bản nhận phòng."
                  : null
              }
              onEdit={() => startEditing("MoveIn")}
            />
            <HandoverRecordCard
              title="Biên bản trả phòng"
              kind="MoveOut"
              record={data.moveOut}
              isManager={isManager}
              canEdit={
                isManager &&
                !isDepositSettled &&
                (data.roomEquipments.length > 0 ||
                  data.moveIn !== null ||
                  data.moveOut !== null)
              }
              lockedReason={
                isManager && isDepositSettled
                  ? "Hợp đồng đã quyết toán cọc nên không thể sửa biên bản trả phòng."
                  : null
              }
              onEdit={() => startEditing("MoveOut")}
            />
          </div>

          {editingKind && (
            <form
              noValidate
              onSubmit={submitForm}
              className="space-y-4 rounded-xl border border-teal-200 bg-white p-4"
              aria-label={
                editingKind === "MoveIn"
                  ? "Biểu mẫu biên bản nhận phòng"
                  : "Biểu mẫu biên bản trả phòng"
              }
            >
              <div className="flex flex-wrap items-center justify-between gap-2">
                <h4 className="font-bold text-slate-900">
                  {editingKind === "MoveIn"
                    ? "Lập biên bản nhận phòng"
                    : "Lập biên bản trả phòng"}
                </h4>
              </div>

              <div className="grid gap-3 sm:grid-cols-2">
                <label className="block space-y-1 text-xs font-semibold text-slate-700">
                  <span>Ngày bàn giao</span>
                  <input
                    type="date"
                    aria-label="Ngày bàn giao"
                    max={getVietnamTodayIsoDate()}
                    value={handoverDate}
                    onChange={(e) => {
                      setHandoverDate(e.target.value)
                      setFieldErrors((prev) => {
                        const next = { ...prev }
                        delete next.handoverDate
                        return next
                      })
                    }}
                    disabled={saving}
                    className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm font-normal text-slate-900"
                  />
                  {fieldErrors.handoverDate && (
                    <span className="block font-normal text-rose-600">
                      {fieldErrors.handoverDate}
                    </span>
                  )}
                </label>

                <label className="block space-y-1 text-xs font-semibold text-slate-700">
                  <span>Ghi chú biên bản</span>
                  <input
                    type="text"
                    aria-label="Ghi chú biên bản"
                    value={recordNote}
                    onChange={(e) => setRecordNote(e.target.value)}
                    disabled={saving}
                    placeholder="Ví dụ: Biên bản có chữ ký hai bên"
                    className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm font-normal text-slate-900"
                  />
                </label>
              </div>

              <div className="space-y-3">
                {rows.map((row, idx) => {
                  const canCharge = canChargeForHandoverItem(
                    editingKind,
                    row.condition,
                    row.moveInCondition,
                  )
                  const maxCharge = row.quantity * row.compensationValue
                  const condErr = fieldErrors[`items[${idx}].condition`]
                  const chargeErr = fieldErrors[`items[${idx}].chargeAmount`]
                  const noteErr = fieldErrors[`items[${idx}].note`]

                  return (
                    <div
                      key={row.equipmentId}
                      className="rounded-lg border border-slate-200 bg-slate-50/70 p-3"
                    >
                      <div className="flex flex-wrap items-center justify-between gap-2">
                        <span className="font-semibold text-slate-800">
                          {formatEquipmentSummary({
                            name: row.equipmentName,
                            quantity: row.quantity,
                            compensationValue: row.compensationValue,
                          })}
                        </span>
                        {editingKind === "MoveOut" && (
                          <span className="text-xs font-medium text-slate-600">
                            Lúc nhận phòng:{" "}
                            <strong>
                              {formatConditionLabel(row.moveInCondition)}
                            </strong>
                          </span>
                        )}
                      </div>

                      <div className="mt-2 grid gap-3 sm:grid-cols-3">
                        <label className="block space-y-1 text-xs font-medium text-slate-700">
                          <span>Tình trạng</span>
                          <select
                            aria-label={`Tình trạng ${row.equipmentName}`}
                            value={row.condition}
                            onChange={(e) =>
                              updateRowCondition(
                                idx,
                                e.target.value as EquipmentCondition,
                              )
                            }
                            disabled={saving}
                            className="w-full rounded-lg border border-slate-300 bg-white px-2.5 py-1.5 text-sm text-slate-900"
                          >
                            {EQUIPMENT_CONDITIONS.map((opt) => (
                              <option key={opt.value} value={opt.value}>
                                {opt.label}
                              </option>
                            ))}
                          </select>
                          {condErr && (
                            <span className="block text-rose-600">
                              {condErr}
                            </span>
                          )}
                        </label>

                        {editingKind === "MoveOut" && (
                          <label className="block space-y-1 text-xs font-medium text-slate-700">
                            <span>Tiền bồi thường (VND)</span>
                            <input
                              type="number"
                              aria-label={`Tiền bồi thường ${row.equipmentName}`}
                              min={0}
                              step={1000}
                              max={maxCharge > 0 ? maxCharge : undefined}
                              value={row.chargeAmount}
                              onChange={(e) =>
                                updateRowChargeAmount(idx, e.target.value)
                              }
                              disabled={!canCharge || saving}
                              className="w-full rounded-lg border border-slate-300 bg-white px-2.5 py-1.5 text-sm text-slate-900 disabled:bg-slate-100 disabled:text-slate-400"
                            />
                            <span className="block text-[11px] text-slate-500">
                              {isDamageOrMissingCondition(row.moveInCondition)
                                ? "Đã hư hỏng/mất từ lúc nhận phòng — không tính tiền"
                                : canCharge
                                  ? maxCharge > 0
                                    ? `Tối đa ${maxCharge.toLocaleString("vi-VN")}đ (${row.quantity} × ${row.compensationValue.toLocaleString("vi-VN")}đ)`
                                    : "Chưa định giá"
                                  : "Chỉ mở khi chọn Hư hỏng hoặc Mất"}
                            </span>
                            {chargeErr && (
                              <span className="block text-rose-600">
                                {chargeErr}
                              </span>
                            )}
                          </label>
                        )}

                        <label className="block space-y-1 text-xs font-medium text-slate-700">
                          <span>Ghi chú thiết bị</span>
                          <input
                            type="text"
                            aria-label={`Ghi chú ${row.equipmentName}`}
                            value={row.note}
                            onChange={(e) => updateRowNote(idx, e.target.value)}
                            disabled={saving}
                            className="w-full rounded-lg border border-slate-300 bg-white px-2.5 py-1.5 text-sm text-slate-900"
                          />
                          {noteErr && (
                            <span className="block text-rose-600">
                              {noteErr}
                            </span>
                          )}
                        </label>
                      </div>
                    </div>
                  )
                })}
              </div>

              {editingKind === "MoveOut" && (
                <p className="text-right text-sm font-bold text-[#087775]">
                  Tổng bồi thường: {formatVnd(liveTotalCharge)}
                </p>
              )}

              {generalError && (
                <p role="alert" className="text-xs font-medium text-rose-600">
                  {generalError}
                </p>
              )}

              <div className="flex justify-end gap-2">
                <button
                  type="button"
                  onClick={cancelEditing}
                  disabled={saving}
                  className="rounded-lg border border-slate-300 px-3 py-2 text-xs font-semibold text-slate-700"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={saving}
                  className="rounded-lg bg-[#087775] px-4 py-2 text-xs font-bold text-white disabled:opacity-60"
                >
                  {saving ? "Đang lưu..." : "Lưu biên bản"}
                </button>
              </div>
            </form>
          )}
        </div>
      )}
    </section>
  )
}

function HandoverRecordCard({
  title,
  kind,
  record,
  isManager,
  canEdit,
  lockedReason,
  onEdit,
}: {
  title: string
  kind: HandoverKind
  record: HandoverRecordDto | null
  isManager: boolean
  canEdit: boolean
  lockedReason: string | null
  onEdit: () => void
}) {
  const actionLabel =
    kind === "MoveIn" ? "Lập biên bản nhận phòng" : "Lập biên bản trả phòng"

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h4 className="font-bold text-slate-800">{title}</h4>
        {isManager && canEdit && (
          <button
            type="button"
            onClick={onEdit}
            className="rounded-lg bg-teal-50 px-3 py-1.5 text-xs font-bold text-[#087775] hover:bg-teal-100"
          >
            {actionLabel}
          </button>
        )}
      </div>

      {lockedReason && (
        <p className="mt-1 text-xs text-slate-500">{lockedReason}</p>
      )}

      {!record ? (
        <p className="mt-2 text-xs text-slate-500">
          {kind === "MoveIn"
            ? "Chưa lập biên bản nhận phòng."
            : "Chưa lập biên bản trả phòng."}
        </p>
      ) : (
        <div className="mt-3 space-y-2 text-xs text-slate-700">
          <p>
            Ngày bàn giao:{" "}
            <strong>{formatContractDate(record.handoverDate)}</strong>
            {record.recordedByName && (
              <>
                {" "}
                · Người lập: <strong>{record.recordedByName}</strong>
              </>
            )}
          </p>
          {record.note && <p>Ghi chú: {record.note}</p>}
          <ul className="space-y-1.5 border-t border-slate-100 pt-2">
            {record.items.map((item) => (
              <li
                key={item.handoverItemId || item.equipmentId}
                className="flex flex-wrap items-center justify-between gap-2"
              >
                <span>
                  <strong>
                    {item.equipmentName} × {item.quantity}
                  </strong>{" "}
                  ({formatConditionLabel(item.condition)})
                  {kind === "MoveOut" && item.moveInCondition && (
                    <span className="text-slate-500">
                      {" "}
                      · Lúc nhận phòng:{" "}
                      {formatConditionLabel(item.moveInCondition)}
                    </span>
                  )}
                  {item.note && (
                    <span className="text-slate-500"> — {item.note}</span>
                  )}
                </span>
                {kind === "MoveOut" && item.chargeAmount > 0 && (
                  <span className="font-semibold text-rose-700">
                    {formatVnd(item.chargeAmount)}
                  </span>
                )}
              </li>
            ))}
          </ul>
          {kind === "MoveOut" && (
            <p className="border-t border-slate-100 pt-2 font-bold text-[#087775]">
              Tổng bồi thường: {formatVnd(record.totalCharge)}
            </p>
          )}
        </div>
      )}
    </div>
  )
}
