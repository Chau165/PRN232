import { useEffect, useState, type FormEvent } from "react"
import type { ManagementRoomDto } from "@/types/managementRoom"
import type { EquipmentDto, EquipmentWriteRequest } from "@/types/equipment"

type EquipmentFormModalProps = {
  mode: "create" | "edit"
  initialEquipment?: EquipmentDto | null
  room: ManagementRoomDto
  saving: boolean
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
  onClose,
  onSubmit,
}: EquipmentFormModalProps) {
  const [values, setValues] = useState<FormValues>(() =>
    valuesFromEquipment(initialEquipment),
  )
  const [formError, setFormError] = useState("")

  useEffect(() => {
    setValues(valuesFromEquipment(initialEquipment))
    setFormError("")
  }, [initialEquipment, mode])

  function updateValue(field: keyof FormValues, value: string) {
    setValues((current) => ({ ...current, [field]: value }))
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const name = values.name.trim()
    const supplier = values.supplier.trim()

    if (!name) {
      setFormError("Tên thiết bị không được để trống.")
      return
    }
    if (!values.installDate || !values.warrantyExpiry) {
      setFormError("Vui lòng nhập ngày lắp đặt và hạn bảo hành.")
      return
    }
    if (values.warrantyExpiry < values.installDate) {
      setFormError("Hạn bảo hành phải từ ngày lắp đặt trở đi.")
      return
    }
    if (!supplier) {
      setFormError("Nhà cung cấp không được để trống.")
      return
    }

    setFormError("")
    const request: Omit<EquipmentWriteRequest, "roomId"> = {
      name,
      installDate: values.installDate,
      warrantyExpiry: values.warrantyExpiry,
      supplier,
      status: values.status,
    }
    await onSubmit(request)
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

        <form onSubmit={submit} className="mt-6 space-y-4">
            {formError && (
              <p role="alert" className="rounded-lg bg-rose-50 px-3 py-2 text-sm text-rose-700">
                {formError}
              </p>
            )}
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
            <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
              Tên thiết bị
              <input
                aria-label="Tên thiết bị"
                value={values.name}
                onChange={(event) => updateValue("name", event.target.value)}
                className="rounded-lg border border-slate-200 px-3 py-2 font-normal"
              />
            </label>
            <div className="grid gap-4 sm:grid-cols-2">
              <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
                Ngày lắp đặt
                <input
                  aria-label="Ngày lắp đặt"
                  type="date"
                  value={values.installDate}
                  onChange={(event) => updateValue("installDate", event.target.value)}
                  className="rounded-lg border border-slate-200 px-3 py-2 font-normal"
                />
              </label>
              <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
                Hạn bảo hành
                <input
                  aria-label="Hạn bảo hành"
                  type="date"
                  value={values.warrantyExpiry}
                  onChange={(event) => updateValue("warrantyExpiry", event.target.value)}
                  className="rounded-lg border border-slate-200 px-3 py-2 font-normal"
                />
              </label>
            </div>
            <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
              Nhà cung cấp
              <input
                aria-label="Nhà cung cấp"
                value={values.supplier}
                onChange={(event) => updateValue("supplier", event.target.value)}
                className="rounded-lg border border-slate-200 px-3 py-2 font-normal"
              />
            </label>
            <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
              Trạng thái
              <select
                aria-label="Trạng thái"
                value={values.status}
                onChange={(event) => updateValue("status", event.target.value)}
                className="rounded-lg border border-slate-200 px-3 py-2 font-normal"
              >
                <option value="Operational">Đang hoạt động</option>
              </select>
            </label>
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
