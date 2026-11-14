import { useCallback, useEffect, useState, type FormEvent } from "react"
import { ApiError } from "@/api/apiClient"
import type { ManagementRoomDto } from "@/types/managementRoom"
import type { EquipmentDto, EquipmentWriteRequest } from "@/types/equipment"
import {
  getVietnamTodayIsoDate,
  validateEquipmentForm,
  type EquipmentFormField,
} from "@/utils/equipmentValidation"
import { splitValidationErrors } from "@/utils/roomValidation"

const EQUIPMENT_FORM_FIELDS: readonly EquipmentFormField[] = [
  "roomId",
  "name",
  "installDate",
  "warrantyExpiry",
  "supplier",
  "status",
]

type EquipmentFormModalProps = {
  mode: "create" | "edit"
  initialEquipment?: EquipmentDto | null
  room: ManagementRoomDto
  saving: boolean
  serverError?: unknown
  onClose: () => void
  onSubmit: (
    request: Omit<EquipmentWriteRequest, "roomId">,
  ) => Promise<boolean>
}

type FormValues = {
  name: string
  installDate: string
  warrantyExpiry: string
  supplier: string
  status: "Operational"
}

function valuesFromEquipment(
  equipment: EquipmentDto | null | undefined,
): FormValues {
  return {
    name: equipment?.name ?? "",
    installDate: equipment?.installDate ?? "",
    warrantyExpiry: equipment?.warrantyExpiry ?? "",
    supplier: equipment?.supplier ?? "",
    status: "Operational",
  }
}

export default function EquipmentFormModal({
  mode,
  initialEquipment,
  room,
  saving,
  serverError,
  onClose,
  onSubmit,
}: EquipmentFormModalProps) {
  const [values, setValues] = useState<FormValues>(() =>
    valuesFromEquipment(initialEquipment),
  )
  const [fieldErrors, setFieldErrors] = useState<
    Partial<Record<EquipmentFormField, string>>
  >({})
  const [formError, setFormError] = useState("")
  const today = getVietnamTodayIsoDate()

  const applyServerError = useCallback((cause: unknown) => {
    if (!cause) return
    if (cause instanceof ApiError && cause.status === 400) {
      const { fieldErrors: mapped, generalError } = splitValidationErrors(
        cause.validationErrors,
        EQUIPMENT_FORM_FIELDS,
        cause.message,
      )
      setFieldErrors(mapped)
      setFormError(generalError)
      return
    }
    setFieldErrors({})
    if (cause instanceof ApiError) {
      if (cause.status === 403) {
        setFormError("Bạn không có quyền thực hiện thao tác này.")
        return
      }
      setFormError(cause.message || "Không thể lưu thiết bị.")
      return
    }
    setFormError(
      cause instanceof Error ? cause.message : "Không thể lưu thiết bị.",
    )
  }, [])

  useEffect(() => {
    setValues(valuesFromEquipment(initialEquipment))
    setFieldErrors({})
    setFormError("")
  }, [initialEquipment, mode])

  useEffect(() => {
    if (serverError) {
      applyServerError(serverError)
    }
  }, [applyServerError, serverError])

  function updateValue(field: keyof FormValues, value: string) {
    setValues((current) => ({ ...current, [field]: value }))
    setFieldErrors((current) => {
      if (!current[field] && !(field === "installDate" && current.warrantyExpiry)) {
        return current
      }
      const next = { ...current }
      delete next[field]
      if (field === "installDate") delete next.warrantyExpiry
      return next
    })
    setFormError("")
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const nextFieldErrors = validateEquipmentForm(
      {
        mode,
        roomId: room.roomId,
        name: values.name,
        installDate: values.installDate,
        warrantyExpiry: values.warrantyExpiry,
        supplier: values.supplier,
        status: values.status,
      },
      today,
    )

    if (Object.keys(nextFieldErrors).length > 0) {
      setFieldErrors(nextFieldErrors)
      setFormError("")
      return
    }

    const name = values.name.trim()
    const supplier = values.supplier.trim()

    setFieldErrors({})
    setFormError("")
    const request: Omit<EquipmentWriteRequest, "roomId"> = {
      name,
      installDate: values.installDate,
      warrantyExpiry: values.warrantyExpiry,
      supplier,
      status: values.status,
    }
    try {
      await onSubmit(request)
    } catch (cause) {
      applyServerError(cause)
    }
  }

  const title =
    mode === "create"
      ? "Thêm thiết bị cho phòng " + room.roomCode
      : "Sửa thiết bị"

  return (
    <div className="fixed inset-0 z-[70] grid place-items-center bg-slate-950/40 p-4">
      <section
        role="dialog"
        aria-modal="true"
        aria-labelledby="equipment-form-title"
        className="max-h-[90vh] w-full max-w-lg overflow-y-auto rounded-2xl bg-white p-6 shadow-2xl"
      >
        <div className="flex items-start justify-between gap-4">
          <div>
            <p className="text-[11px] font-bold uppercase tracking-[.14em] text-[#087775]">
              Thiết bị
            </p>
            <h2
              id="equipment-form-title"
              className="mt-1 font-display text-2xl font-bold text-slate-900"
            >
              {title}
            </h2>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="Đóng"
            className="text-2xl text-slate-400"
          >
            ×
          </button>
        </div>

        <form onSubmit={submit} noValidate className="mt-6 space-y-4">
            {formError && (
              <p role="alert" className="rounded-lg bg-rose-50 px-3 py-2 text-sm text-rose-700">
                {formError}
              </p>
            )}
            <div>
              <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
                Phòng
                <input
                  aria-label="Phòng"
                  value={room.roomCode}
                  readOnly
                  className="rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 font-normal"
                />
                <span className="text-xs font-normal text-slate-500">
                  {room.propertyName}
                </span>
              </label>
              {fieldErrors.roomId && (
                <p role="alert" className="mt-1 text-xs text-rose-600">
                  {fieldErrors.roomId}
                </p>
              )}
            </div>
            <div>
              <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
                Tên thiết bị
                <input
                  aria-label="Tên thiết bị"
                  aria-invalid={Boolean(fieldErrors.name)}
                  value={values.name}
                  onChange={(event) => updateValue("name", event.target.value)}
                  className="rounded-lg border border-slate-200 px-3 py-2 font-normal"
                />
              </label>
              {fieldErrors.name && (
                <p role="alert" className="mt-1 text-xs text-rose-600">
                  {fieldErrors.name}
                </p>
              )}
            </div>
            <div className="grid gap-4 sm:grid-cols-2">
              <div>
                <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
                  Ngày lắp đặt
                  <input
                    aria-label="Ngày lắp đặt"
                    aria-invalid={Boolean(fieldErrors.installDate)}
                    type="date"
                    max={today}
                    value={values.installDate}
                    onChange={(event) => updateValue("installDate", event.target.value)}
                    className="rounded-lg border border-slate-200 px-3 py-2 font-normal"
                  />
                </label>
                {fieldErrors.installDate && (
                  <p role="alert" className="mt-1 text-xs text-rose-600">
                    {fieldErrors.installDate}
                  </p>
                )}
              </div>
              <div>
                <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
                  Hạn bảo hành
                  <input
                    aria-label="Hạn bảo hành"
                    aria-invalid={Boolean(fieldErrors.warrantyExpiry)}
                    type="date"
                    value={values.warrantyExpiry}
                    onChange={(event) => updateValue("warrantyExpiry", event.target.value)}
                    className="rounded-lg border border-slate-200 px-3 py-2 font-normal"
                  />
                </label>
                {fieldErrors.warrantyExpiry && (
                  <p role="alert" className="mt-1 text-xs text-rose-600">
                    {fieldErrors.warrantyExpiry}
                  </p>
                )}
              </div>
            </div>
            <div>
              <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
                Nhà cung cấp
                <input
                  aria-label="Nhà cung cấp"
                  aria-invalid={Boolean(fieldErrors.supplier)}
                  value={values.supplier}
                  onChange={(event) => updateValue("supplier", event.target.value)}
                  className="rounded-lg border border-slate-200 px-3 py-2 font-normal"
                />
              </label>
              {fieldErrors.supplier && (
                <p role="alert" className="mt-1 text-xs text-rose-600">
                  {fieldErrors.supplier}
                </p>
              )}
            </div>
            <div>
              <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
                Trạng thái
                <select
                  aria-label="Trạng thái"
                  aria-invalid={Boolean(fieldErrors.status)}
                  value={values.status}
                  onChange={(event) => updateValue("status", event.target.value)}
                  className="rounded-lg border border-slate-200 px-3 py-2 font-normal"
                >
                  <option value="Operational">Đang hoạt động</option>
                </select>
              </label>
              {fieldErrors.status && (
                <p role="alert" className="mt-1 text-xs text-rose-600">
                  {fieldErrors.status}
                </p>
              )}
            </div>
            <div className="flex justify-end gap-3 border-t border-slate-100 pt-4">
              <button
                type="button"
                onClick={onClose}
                className="rounded-lg bg-slate-100 px-4 py-2 text-sm font-bold text-slate-600"
              >
                Hủy
              </button>
              <button
                type="submit"
                disabled={saving}
                className="rounded-lg bg-[#087775] px-4 py-2 text-sm font-bold text-white disabled:opacity-50"
              >
                {saving ? "Đang lưu..." : "Lưu thiết bị"}
              </button>
            </div>
        </form>
      </section>
    </div>
  )
}
