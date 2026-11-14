import { useCallback, useEffect, useState, type FormEvent } from "react"
import { ApiError } from "@/api/apiClient"
import SearchableSingleSelect from "@/components/common/SearchableSingleSelect"
import {
  getAdministrativeProvinces,
  getAdministrativeWards,
} from "@/services/propertyService"
import type {
  AdministrativeProvinceOption,
  AdministrativeWardOption,
} from "@/types/administrativeDivision"
import type { PropertyDto } from "@/types/user"
import {
  buildFullPropertyAddress,
  hasDuplicateAdministrativeSegments,
} from "@/utils/propertyAddress"
import {
  validatePropertyForm,
  type PropertyFormField,
} from "@/utils/propertyValidation"
import { splitValidationErrors } from "@/utils/roomValidation"
import type { PropertyWriteRequest } from "@/services/propertyService"

const PROPERTY_FORM_FIELDS: readonly PropertyFormField[] = [
  "propertyName",
  "addressText",
  "description",
  "provinceCode",
  "wardCode",
  "primaryImage",
]

type PropertyFormValues = {
  propertyName: string
  addressText: string
  provinceCode: string
  wardCode: string
  primaryImage: File | null
  description: string
}

type PropertyFormModalProps = {
  mode: "create" | "edit"
  accessToken: string
  initialProperty?: PropertyDto | null
  saving: boolean
  serverError?: unknown
  onClose: () => void
  onSubmit: (request: PropertyWriteRequest) => Promise<boolean>
}

type LoadStatus = "idle" | "loading" | "ready" | "error"

const emptyValues: PropertyFormValues = {
  propertyName: "",
  addressText: "",
  provinceCode: "",
  wardCode: "",
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
    provinceCode: property.provinceCode ?? "",
    wardCode: property.wardCode ?? "",
    primaryImage: null,
    description: property.description ?? "",
  }
}

export default function PropertyFormModal({
  mode,
  accessToken,
  initialProperty,
  saving,
  serverError,
  onClose,
  onSubmit,
}: PropertyFormModalProps) {
  const [values, setValues] = useState<PropertyFormValues>(() =>
    valuesFromProperty(initialProperty),
  )
  const [fieldErrors, setFieldErrors] = useState<
    Partial<Record<PropertyFormField, string>>
  >({})
  const [formError, setFormError] = useState("")
  const [catalogStatus, setCatalogStatus] = useState<LoadStatus>("loading")
  const [catalogError, setCatalogError] = useState("")
  const [catalogReload, setCatalogReload] = useState(0)
  const [provinces, setProvinces] = useState<AdministrativeProvinceOption[]>([])
  const [wardStatus, setWardStatus] = useState<LoadStatus>("idle")
  const [wardError, setWardError] = useState("")
  const [wardReload, setWardReload] = useState(0)
  const [wards, setWards] = useState<AdministrativeWardOption[]>([])

  const applyServerError = useCallback((cause: unknown) => {
    if (!cause) return
    if (cause instanceof ApiError && cause.status === 400) {
      const normalizedErrors = cause.validationErrors
        ? Object.fromEntries(
            Object.entries(cause.validationErrors).map(([key, msgs]) => {
              const lower = key.toLowerCase()
              if (lower === "city") return ["provinceCode", msgs]
              if (lower === "ward") return ["wardCode", msgs]
              return [key, msgs]
            }),
          )
        : undefined
      const { fieldErrors: mapped, generalError } = splitValidationErrors(
        normalizedErrors,
        PROPERTY_FORM_FIELDS,
        cause.message,
      )
      setFieldErrors(mapped)
      setFormError(generalError)
      return
    }
    setFieldErrors({})
    if (cause instanceof ApiError) {
      if (cause.status === 409) {
        setFormError(
          cause.message ||
            "Thao tác bị từ chối vì khu trọ còn dữ liệu nghiệp vụ liên quan.",
        )
        return
      }
      if (cause.status === 403) {
        setFormError(
          cause.message || "Bạn không có quyền thực hiện thao tác này.",
        )
        return
      }
      setFormError(cause.message)
      return
    }
    setFormError(
      cause instanceof Error
        ? cause.message
        : "Không thể cập nhật dữ liệu khu trọ.",
    )
  }, [])

  useEffect(() => {
    setValues(valuesFromProperty(initialProperty))
    setFieldErrors({})
    setFormError("")
    setCatalogStatus("loading")
    setCatalogError("")
    setProvinces([])
    setWardStatus("idle")
    setWardError("")
    setWards([])
  }, [initialProperty?.propertyId, mode])

  useEffect(() => {
    if (serverError) {
      applyServerError(serverError)
    }
  }, [applyServerError, serverError])

  useEffect(() => {
    const controller = new AbortController()
    setCatalogStatus("loading")
    setCatalogError("")
    setProvinces([])

    getAdministrativeProvinces(accessToken, controller.signal)
      .then((loadedProvinces) => {
        if (controller.signal.aborted) return
        setProvinces(loadedProvinces)
        setCatalogStatus("ready")
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted) return
        setCatalogStatus("error")
        setCatalogError(
          cause instanceof Error
            ? cause.message
            : "Không thể tải danh mục tỉnh/thành phố.",
        )
      })

    return () => controller.abort()
  }, [accessToken, catalogReload, initialProperty?.propertyId, mode])

  useEffect(() => {
    if (!values.provinceCode || catalogStatus !== "ready") {
      setWards([])
      setWardStatus("idle")
      setWardError("")
      return
    }

    const controller = new AbortController()
    setWards([])
    setWardStatus("loading")
    setWardError("")

    getAdministrativeWards(accessToken, values.provinceCode, controller.signal)
      .then((loadedWards) => {
        if (controller.signal.aborted) return
        setWards(loadedWards)
        setWardStatus("ready")
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted) return
        setWardStatus("error")
        setWardError(
          cause instanceof Error
            ? cause.message
            : "Không thể tải danh sách phường/xã.",
        )
      })

    return () => controller.abort()
  }, [accessToken, catalogStatus, values.provinceCode, wardReload])

  function updateValue(
    field: "propertyName" | "description",
    value: string,
  ) {
    setValues((current) => ({ ...current, [field]: value }))
    setFieldErrors((current) => {
      if (!current[field]) return current
      const next = { ...current }
      delete next[field]
      return next
    })
    setFormError("")
  }

  function changeProvince(provinceCode: string) {
    setValues((current) => ({
      ...current,
      provinceCode,
      wardCode: "",
    }))
    setWards([])
    setFieldErrors((current) => {
      if (!current.provinceCode && !current.wardCode) return current
      const next = { ...current }
      delete next.provinceCode
      delete next.wardCode
      return next
    })
    setFormError("")
  }

  function changeWard(wardCode: string) {
    setValues((current) => ({
      ...current,
      wardCode,
    }))
    setFieldErrors((current) => {
      if (!current.wardCode) return current
      const next = { ...current }
      delete next.wardCode
      return next
    })
    setFormError("")
  }

  function changeStreetAddress(addressText: string) {
    setValues((current) => ({ ...current, addressText }))
    setFieldErrors((current) => {
      if (!current.addressText) return current
      const next = { ...current }
      delete next.addressText
      return next
    })
    setFormError("")
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (saving) return

    if (
      catalogStatus !== "ready" ||
      (Boolean(values.provinceCode) && wardStatus === "loading")
    ) {
      setFormError("Đang tải danh mục hành chính, vui lòng đợi rồi lưu lại.")
      return
    }

    const nextFieldErrors = validatePropertyForm({
      propertyName: values.propertyName,
      addressText: values.addressText,
      description: values.description,
    })

    const propertyName = values.propertyName.trim()
    const addressText = values.addressText.trim()

    const selectedProvince = provinces.find(
      (province) => province.provinceCode === values.provinceCode,
    )
    const selectedWard = wards.find((ward) => ward.wardCode === values.wardCode)

    if (!selectedProvince) {
      nextFieldErrors.provinceCode = "Chọn tỉnh/thành phố."
    }
    if (!selectedWard) {
      nextFieldErrors.wardCode = "Chọn phường/xã."
    }

    const isUnchangedEditAddress =
      mode === "edit" &&
      initialProperty != null &&
      addressText === initialProperty.addressText.trim()

    if (
      !isUnchangedEditAddress &&
      !nextFieldErrors.addressText &&
      selectedProvince &&
      selectedWard &&
      hasDuplicateAdministrativeSegments(addressText, [
        selectedWard.wardName,
        selectedProvince.provinceName,
      ])
    ) {
      nextFieldErrors.addressText =
        "Ô số nhà và tên đường không nhập lại phường/xã hoặc tỉnh/thành phố."
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
        nextFieldErrors.primaryImage =
          "Ảnh phải là JPEG, PNG hoặc WebP và không vượt quá 5 MiB."
      }
    }

    if (Object.keys(nextFieldErrors).length > 0) {
      setFieldErrors(nextFieldErrors)
      setFormError("")
      return
    }

    setFieldErrors({})
    setFormError("")
    try {
      await onSubmit({
        propertyName,
        addressText,
        provinceCode: values.provinceCode,
        wardCode: values.wardCode,
        primaryImage: values.primaryImage ?? undefined,
        description: values.description.trim() || undefined,
      })
    } catch (cause) {
      applyServerError(cause)
    }
  }

  const title = mode === "create" ? "Thêm khu trọ" : "Sửa khu trọ"
  const submitLabel = mode === "create" ? "Tạo khu trọ" : "Lưu khu trọ"
  const selectedProvince = provinces.find(
    (province) => province.provinceCode === values.provinceCode,
  )
  const selectedWard = wards.find((ward) => ward.wardCode === values.wardCode)
  const fullAddressPreview = selectedProvince && selectedWard
    ? buildFullPropertyAddress(
      values.addressText,
      selectedWard.wardName,
      selectedProvince.provinceName,
    )
    : ""
  return (
    <div className="fixed inset-0 z-[70] grid place-items-center bg-slate-950/40 p-3 sm:p-4">
      <section
        role="dialog"
        aria-modal="true"
        aria-labelledby="property-form-title"
        className="max-h-[95vh] w-full max-w-4xl overflow-y-auto rounded-xl bg-white p-4 shadow-2xl sm:p-6"
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
            className="rounded-lg px-2 text-2xl text-slate-400 hover:bg-slate-100"
          >
            ×
          </button>
        </div>

        <form onSubmit={submit} noValidate className="mt-5 space-y-5">
          {formError && (
            <p role="alert" className="rounded-lg bg-rose-50 px-3 py-2 text-sm text-rose-700">
              {formError}
            </p>
          )}

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="sm:col-span-2">
              <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
                Tên khu trọ
                <input
                  aria-label="Tên khu trọ"
                  aria-invalid={Boolean(fieldErrors.propertyName)}
                  required
                  value={values.propertyName}
                  onChange={(event) => updateValue("propertyName", event.target.value)}
                  className="min-h-11 rounded-lg border border-slate-200 px-3 py-2 font-normal outline-none focus:border-teal-700 focus:ring-2 focus:ring-teal-100"
                />
              </label>
              {fieldErrors.propertyName && (
                <p role="alert" className="mt-1 text-xs text-rose-600">
                  {fieldErrors.propertyName}
                </p>
              )}
            </div>

            <section className="space-y-4 sm:col-span-2" aria-labelledby="property-address-heading">
              <div>
                <h3 id="property-address-heading" className="text-base font-bold text-slate-900">
                  Địa chỉ khu trọ
                </h3>
                <p className="mt-1 text-sm text-slate-600">
                  Chọn đúng địa giới hành chính; tọa độ sẽ được xác định tự động khi lưu.
                </p>
              </div>

              {catalogStatus === "loading" && (
                <p role="status" className="text-sm text-slate-600">
                  Đang tải danh mục hành chính chính thức...
                </p>
              )}
              {catalogStatus === "error" && (
                <div role="alert" className="rounded-lg bg-rose-50 p-3 text-sm text-rose-700">
                  <p>{catalogError || "Không thể tải danh mục hành chính."}</p>
                  <button
                    type="button"
                    onClick={() => setCatalogReload((value) => value + 1)}
                    className="mt-2 rounded font-semibold underline"
                  >
                    Tải lại danh mục
                  </button>
                </div>
              )}

              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-1">
                  <SearchableSingleSelect
                    id="property-province"
                    label="Tỉnh/Thành phố"
                    invalid={Boolean(fieldErrors.provinceCode)}
                    options={provinces.map((province) => ({
                      value: province.provinceCode,
                      label: province.provinceName,
                    }))}
                    value={values.provinceCode}
                    onChange={changeProvince}
                    disabled={catalogStatus !== "ready"}
                    hint={catalogStatus === "ready" ? undefined : "Danh mục chưa sẵn sàng."}
                    placeholder="Tìm tỉnh/thành phố..."
                  />
                  {fieldErrors.provinceCode && (
                    <p role="alert" className="text-xs text-rose-600">
                      {fieldErrors.provinceCode}
                    </p>
                  )}
                </div>

                <div className="space-y-1">
                  <SearchableSingleSelect
                    id="property-ward"
                    label="Phường/Xã"
                    invalid={Boolean(fieldErrors.wardCode)}
                    options={wards.map((ward) => ({
                      value: ward.wardCode,
                      label: ward.wardName,
                    }))}
                    value={values.wardCode}
                    onChange={changeWard}
                    disabled={!values.provinceCode || wardStatus !== "ready"}
                    hint={
                      !values.provinceCode
                        ? "Chọn tỉnh/thành phố trước."
                        : wardStatus === "loading"
                          ? "Đang tải phường/xã trực thuộc..."
                          : undefined
                    }
                    placeholder="Tìm phường/xã..."
                  />
                  {fieldErrors.wardCode && (
                    <p role="alert" className="text-xs text-rose-600">
                      {fieldErrors.wardCode}
                    </p>
                  )}
                  {wardStatus === "error" && (
                    <p role="alert" className="text-xs text-rose-700">
                      {wardError}
                      <button
                        type="button"
                        onClick={() => setWardReload((value) => value + 1)}
                        className="ml-2 font-semibold underline"
                      >
                        Tải lại
                      </button>
                    </p>
                  )}
                </div>

                <div className="sm:col-span-2">
                  <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
                    Số nhà, tên đường
                    <input
                      aria-label="Số nhà, tên đường"
                      aria-invalid={Boolean(fieldErrors.addressText)}
                      required
                      value={values.addressText}
                      onChange={(event) => changeStreetAddress(event.target.value)}
                      placeholder="12 đường Lê Lợi"
                      className="min-h-11 rounded-lg border border-slate-200 px-3 py-2 font-normal outline-none focus:border-teal-700 focus:ring-2 focus:ring-teal-100"
                    />
                  </label>
                  {fieldErrors.addressText && (
                    <p role="alert" className="mt-1 text-xs text-rose-600">
                      {fieldErrors.addressText}
                    </p>
                  )}
                </div>

                <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700 sm:col-span-2">
                  Địa chỉ đầy đủ
                  <input
                    aria-label="Địa chỉ đầy đủ"
                    readOnly
                    value={fullAddressPreview}
                    placeholder="Chọn tỉnh/thành phố và phường/xã để xem địa chỉ"
                    className="min-h-11 rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 font-normal text-slate-600"
                  />
                </label>
              </div>
            </section>

            <div className="sm:col-span-2">
              <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
                Ảnh đại diện (không bắt buộc)
                <input
                  aria-label="Ảnh đại diện"
                  aria-invalid={Boolean(fieldErrors.primaryImage)}
                  type="file"
                  accept="image/jpeg,image/png,image/webp"
                  onChange={(event) => {
                    setValues((current) => ({
                      ...current,
                      primaryImage: event.target.files?.[0] ?? null,
                    }))
                    setFieldErrors((current) => {
                      if (!current.primaryImage) return current
                      const next = { ...current }
                      delete next.primaryImage
                      return next
                    })
                    setFormError("")
                  }}
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
              {fieldErrors.primaryImage && (
                <p role="alert" className="mt-1 text-xs text-rose-600">
                  {fieldErrors.primaryImage}
                </p>
              )}
            </div>

            <div className="sm:col-span-2">
              <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
                Mô tả (không bắt buộc)
                <textarea
                  rows={3}
                  aria-invalid={Boolean(fieldErrors.description)}
                  value={values.description}
                  onChange={(event) => updateValue("description", event.target.value)}
                  className="rounded-lg border border-slate-200 px-3 py-2 font-normal"
                />
              </label>
              {fieldErrors.description && (
                <p role="alert" className="mt-1 text-xs text-rose-600">
                  {fieldErrors.description}
                </p>
              )}
            </div>
          </div>

          <div className="flex flex-wrap justify-end gap-3 border-t border-slate-100 pt-4">
            <button
              type="button"
              onClick={onClose}
              disabled={saving}
              className="rounded-lg bg-slate-100 px-4 py-2 text-sm font-bold text-slate-600 disabled:opacity-50"
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
