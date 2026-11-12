import { useEffect, useState, type FormEvent } from "react"
import type { PropertyDto } from "@/types/user"
import type { PropertyWriteRequest } from "@/services/propertyService"

type PropertyFormValues = {
  propertyName: string
  addressText: string
  ward: string
  district: string
  city: string
  primaryImage: File | null
  description: string
}

type PropertyFormModalProps = {
  mode: "create" | "edit"
  initialProperty?: PropertyDto | null
  saving: boolean
  onClose: () => void
  onSubmit: (request: PropertyWriteRequest) => Promise<boolean>
}

const emptyValues: PropertyFormValues = {
  propertyName: "",
  addressText: "",
  ward: "",
  district: "",
  city: "",
  primaryImage: null,
  description: "",
}

function valuesFromProperty(
  property: PropertyDto | null | undefined,
): PropertyFormValues {
  if (!property) return emptyValues
  return {
    propertyName: property.propertyName,
    addressText: property.addressText,
    ward: property.ward,
    district: property.district,
    city: property.city,
    primaryImage: null,
    description: property.description ?? "",
  }
}

export default function PropertyFormModal({
  mode,
  initialProperty,
  saving,
  onClose,
  onSubmit,
}: PropertyFormModalProps) {
  const [values, setValues] = useState<PropertyFormValues>(() =>
    valuesFromProperty(initialProperty),
  )
  const [formError, setFormError] = useState("")

  useEffect(() => {
    setValues(valuesFromProperty(initialProperty))
    setFormError("")
  }, [initialProperty, mode])

  function updateValue(
    field: keyof Omit<PropertyFormValues, "primaryImage">,
    value: string,
  ) {
    setValues((current) => ({ ...current, [field]: value }))
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const propertyName = values.propertyName.trim()
    const addressText = values.addressText.trim()

    if (!propertyName) {
      setFormError("Tên khu trọ không được để trống.")
      return
    }
    if (!addressText) {
      setFormError("Địa chỉ không được để trống.")
      return
    }
    if (values.primaryImage) {
      const allowedTypes = ["image/jpeg", "image/png", "image/webp"]
      const extension = values.primaryImage.name
        .toLowerCase()
        .match(/\.[^.]+$/)?.[0]
      const allowedExtensions = [".jpg", ".jpeg", ".png", ".webp"]
      if (
        values.primaryImage.size === 0 ||
        values.primaryImage.size > 5 * 1024 * 1024 ||
        !allowedTypes.includes(values.primaryImage.type) ||
        !extension ||
        !allowedExtensions.includes(extension)
      ) {
        setFormError("Ảnh phải là JPEG, PNG hoặc WebP và không vượt quá 5 MiB.")
        return
      }
    }

    setFormError("")
    const saved = await onSubmit({
      propertyName,
      addressText,
      ward: values.ward.trim(),
      district: values.district.trim(),
      city: values.city.trim(),
      primaryImage: values.primaryImage ?? undefined,
      description: values.description.trim() || undefined,
    })
    if (!saved) return
  }

  const title = mode === "create" ? "Thêm khu trọ" : "Sửa khu trọ"
  const submitLabel = mode === "create" ? "Tạo khu trọ" : "Lưu khu trọ"

  return (
    <div className="fixed inset-0 z-[70] grid place-items-center bg-slate-950/40 p-4">
      <section
        role="dialog"
        aria-modal="true"
        aria-labelledby="property-form-title"
        className="max-h-[92vh] w-full max-w-2xl overflow-y-auto rounded-2xl bg-white p-6 shadow-2xl"
      >
        <div className="flex items-start justify-between gap-4">
          <div>
            <p className="text-[11px] font-bold uppercase tracking-[.14em] text-[#087775]">
              Khu trọ
            </p>
            <h2
              id="property-form-title"
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
            <p
              role="alert"
              className="rounded-lg bg-rose-50 px-3 py-2 text-sm text-rose-700"
            >
              {formError}
            </p>
          )}
          <div className="grid gap-4 sm:grid-cols-2">
            <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700 sm:col-span-2">
              Tên khu trọ
              <input
                aria-label="Tên khu trọ"
                value={values.propertyName}
                onChange={(event) =>
                  updateValue("propertyName", event.target.value)
                }
                className="rounded-lg border border-slate-200 px-3 py-2 font-normal"
              />
            </label>
            <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700 sm:col-span-2">
              Địa chỉ
              <input
                aria-label="Địa chỉ"
                value={values.addressText}
                onChange={(event) =>
                  updateValue("addressText", event.target.value)
                }
                className="rounded-lg border border-slate-200 px-3 py-2 font-normal"
              />
            </label>
            <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
              Phường
              <input
                value={values.ward}
                onChange={(event) => updateValue("ward", event.target.value)}
                className="rounded-lg border border-slate-200 px-3 py-2 font-normal"
              />
            </label>
            <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
              Quận / huyện
              <input
                value={values.district}
                onChange={(event) =>
                  updateValue("district", event.target.value)
                }
                className="rounded-lg border border-slate-200 px-3 py-2 font-normal"
              />
            </label>
            <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
              Thành phố
              <input
                value={values.city}
                onChange={(event) => updateValue("city", event.target.value)}
                className="rounded-lg border border-slate-200 px-3 py-2 font-normal"
              />
            </label>
            <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700 sm:col-span-2">
              Ảnh đại diện (không bắt buộc)
              <input
                aria-label="Ảnh đại diện"
                type="file"
                accept="image/jpeg,image/png,image/webp"
                onChange={(event) =>
                  setValues((current) => ({
                    ...current,
                    primaryImage: event.target.files?.[0] ?? null,
                  }))
                }
                className="rounded-lg border border-slate-200 px-3 py-2 font-normal"
              />
              <span className="text-xs font-normal text-slate-500">
                Chọn ảnh JPEG, PNG hoặc WebP, tối đa 5 MiB.
              </span>
              {values.primaryImage && (
                <span className="text-xs font-normal text-slate-600">
                  Đã chọn: {values.primaryImage.name}
                </span>
              )}
              {!values.primaryImage && initialProperty?.primaryImageUrl && (
                <img
                  src={initialProperty.primaryImageUrl}
                  alt={initialProperty.propertyName}
                  className="mt-1 h-24 w-40 rounded-lg object-cover"
                />
              )}
            </label>
            <p className="text-xs font-normal text-slate-500 sm:col-span-2">
              Hệ thống sẽ tự động xác định vĩ độ và kinh độ từ địa chỉ khi lưu.
            </p>
            <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700 sm:col-span-2">
              Mô tả (không bắt buộc)
              <textarea
                rows={3}
                value={values.description}
                onChange={(event) =>
                  updateValue("description", event.target.value)
                }
                className="rounded-lg border border-slate-200 px-3 py-2 font-normal"
              />
            </label>
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
              {saving ? "Đang lưu..." : submitLabel}
            </button>
          </div>
        </form>
      </section>
    </div>
  )
}
