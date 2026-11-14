import { useEffect, useRef, useState, type FormEvent } from "react"
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
import { formatAddress } from "@/utils/publicFormatting"
import {
  buildFullPropertyAddress,
  hasDuplicateAdministrativeSegments,
  normalizeAdministrativeName,
  removeKnownAdministrativeSegments,
} from "@/utils/propertyAddress"
import type { PropertyWriteRequest } from "@/services/propertyService"

type PropertyFormValues = {
  propertyName: string
  addressText: string
  ward: string
  district: string
  city: string
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
  onClose: () => void
  onSubmit: (request: PropertyWriteRequest) => Promise<boolean>
}

type LoadStatus = "idle" | "loading" | "ready" | "error"

const emptyValues: PropertyFormValues = {
  propertyName: "",
  addressText: "",
  ward: "",
  district: "",
  city: "",
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
    ward: property.ward,
    district: property.district,
    city: property.city,
    provinceCode: "",
    wardCode: "",
    primaryImage: null,
    description: property.description ?? "",
  }
}

export default function PropertyFormModal({
  mode,
  accessToken,
  initialProperty,
  saving,
  onClose,
  onSubmit,
}: PropertyFormModalProps) {
  const [values, setValues] = useState<PropertyFormValues>(() =>
    valuesFromProperty(initialProperty),
  )
  const [formError, setFormError] = useState("")
  const [catalogStatus, setCatalogStatus] = useState<LoadStatus>("loading")
  const [catalogError, setCatalogError] = useState("")
  const [catalogReload, setCatalogReload] = useState(0)
  const [provinces, setProvinces] = useState<AdministrativeProvinceOption[]>([])
  const [wardStatus, setWardStatus] = useState<LoadStatus>("idle")
  const [wardError, setWardError] = useState("")
  const [wardReload, setWardReload] = useState(0)
  const [wards, setWards] = useState<AdministrativeWardOption[]>([])
  const [addressEditing, setAddressEditing] = useState(mode === "create")
  const [legacyAddressWarning, setLegacyAddressWarning] = useState(mode === "edit")
  const matchedProvincePropertyId = useRef<number | null>(null)
  const matchedWardPropertyId = useRef<number | null>(null)

  useEffect(() => {
    setValues(valuesFromProperty(initialProperty))
    setFormError("")
    setCatalogStatus("loading")
    setCatalogError("")
    setProvinces([])
    setWardStatus("idle")
    setWardError("")
    setWards([])
    setAddressEditing(mode === "create")
    setLegacyAddressWarning(mode === "edit")
    matchedProvincePropertyId.current = null
    matchedWardPropertyId.current = null
  }, [initialProperty?.propertyId, mode])

  useEffect(() => {
    const controller = new AbortController()
    setCatalogStatus("loading")
    setCatalogError("")
    setProvinces([])
    matchedProvincePropertyId.current = null
    matchedWardPropertyId.current = null

    getAdministrativeProvinces(accessToken, controller.signal)
      .then((loadedProvinces) => {
        if (controller.signal.aborted) return
        setProvinces(loadedProvinces)
        setCatalogStatus("ready")

        if (mode !== "edit" || !initialProperty) return

        const normalizedCity = normalizeAdministrativeName(initialProperty.city)
        const matchingProvinces = loadedProvinces.filter(
          (province) => normalizeAdministrativeName(province.provinceName) === normalizedCity,
        )
        if (matchingProvinces.length !== 1) {
          setLegacyAddressWarning(true)
          matchedProvincePropertyId.current = initialProperty.propertyId
          return
        }

        const [matchingProvince] = matchingProvinces
        setValues((current) => ({
          ...current,
          provinceCode: matchingProvince.provinceCode,
        }))
        matchedProvincePropertyId.current = initialProperty.propertyId
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

  useEffect(() => {
    if (
      mode !== "edit" ||
      !initialProperty ||
      catalogStatus !== "ready" ||
      wardStatus !== "ready" ||
      matchedProvincePropertyId.current !== initialProperty.propertyId ||
      matchedWardPropertyId.current === initialProperty.propertyId
    ) {
      return
    }

    const matchingProvinces = provinces.filter(
      (province) => province.provinceCode === values.provinceCode,
    )
    const matchingWards = wards.filter(
      (ward) => normalizeAdministrativeName(ward.wardName) ===
        normalizeAdministrativeName(initialProperty.ward),
    )
    const matchingProvince = matchingProvinces.length === 1
      ? matchingProvinces[0]
      : undefined
    const matchingWard = matchingWards.length === 1 ? matchingWards[0] : undefined
    const cleanStreetAddress = removeKnownAdministrativeSegments(
      initialProperty.addressText,
      [initialProperty.ward, initialProperty.district, initialProperty.city],
    )
    const isCanonicalAddress = Boolean(
      matchingProvince &&
      matchingWard &&
      !initialProperty.district.trim() &&
      cleanStreetAddress === initialProperty.addressText.trim(),
    )

    if (matchingProvince && matchingWard) {
      setValues((current) => ({
        ...current,
        provinceCode: matchingProvince.provinceCode,
        wardCode: matchingWard.wardCode,
      }))
    }
    setLegacyAddressWarning(!isCanonicalAddress)
    matchedWardPropertyId.current = initialProperty.propertyId
  }, [
    catalogStatus,
    initialProperty,
    mode,
    provinces,
    values.provinceCode,
    wardStatus,
    wards,
  ])

  function updateValue(
    field: "propertyName" | "description",
    value: string,
  ) {
    setValues((current) => ({ ...current, [field]: value }))
  }

  function changeProvince(provinceCode: string) {
    const selectedProvince = provinces.find(
      (province) => province.provinceCode === provinceCode,
    )
    setValues((current) => ({
      ...current,
      provinceCode,
      wardCode: "",
      ward: "",
      district: "",
      city: selectedProvince?.provinceName ?? "",
    }))
    setWards([])
    setFormError("")
  }

  function changeWard(wardCode: string) {
    const selectedWard = wards.find((ward) => ward.wardCode === wardCode)
    setValues((current) => ({
      ...current,
      wardCode,
      ward: selectedWard?.wardName ?? "",
      district: "",
    }))
    setFormError("")
  }

  function changeStreetAddress(addressText: string) {
    setValues((current) => ({ ...current, addressText }))
    setFormError("")
  }

  function beginAddressNormalization() {
    const selectedProvince = provinces.find(
      (province) => province.provinceCode === values.provinceCode,
    )
    const selectedWard = wards.find((ward) => ward.wardCode === values.wardCode)
    const propertyStreetAddress = removeKnownAdministrativeSegments(
      values.addressText,
      [values.ward, values.district, values.city],
    )

    setValues((current) => ({
      ...current,
      addressText: propertyStreetAddress,
      ward: selectedWard?.wardName ?? "",
      district: "",
      city: selectedProvince?.provinceName ?? "",
      provinceCode: selectedProvince?.provinceCode ?? "",
      wardCode: selectedWard?.wardCode ?? "",
    }))
    setAddressEditing(true)
    setFormError("")
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (saving) return

    const propertyName = values.propertyName.trim()
    if (!propertyName) {
      setFormError("Tên khu trọ không được để trống.")
      return
    }

    const addressText = addressEditing
      ? values.addressText.trim()
      : initialProperty?.addressText ?? values.addressText
    if (addressEditing && !addressText) {
      setFormError("Số nhà và tên đường không được để trống.")
      return
    }

    const selectedProvince = provinces.find(
      (province) => province.provinceCode === values.provinceCode,
    )
    const selectedWard = wards.find((ward) => ward.wardCode === values.wardCode)
    if (addressEditing && (!selectedProvince || !selectedWard)) {
      setFormError("Chọn tỉnh/thành phố và phường/xã từ danh mục hiện hành.")
      return
    }

    if (
      addressEditing && selectedProvince && selectedWard &&
      hasDuplicateAdministrativeSegments(addressText, [
        selectedWard.wardName,
        selectedProvince.provinceName,
      ])
    ) {
      setFormError("Ô số nhà và tên đường không nhập lại phường/xã hoặc tỉnh/thành phố.")
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
    await onSubmit({
      propertyName,
      addressText,
      ward: addressEditing
        ? selectedWard?.wardName ?? ""
        : initialProperty?.ward ?? values.ward,
      district: addressEditing
        ? ""
        : initialProperty?.district ?? values.district,
      city: addressEditing
        ? selectedProvince?.provinceName ?? ""
        : initialProperty?.city ?? values.city,
      provinceCode: addressEditing ? values.provinceCode : undefined,
      wardCode: addressEditing ? values.wardCode : undefined,
      primaryImage: values.primaryImage ?? undefined,
      description: values.description.trim() || undefined,
    })
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

        <form onSubmit={submit} className="mt-5 space-y-5">
          {formError && (
            <p role="alert" className="rounded-lg bg-rose-50 px-3 py-2 text-sm text-rose-700">
              {formError}
            </p>
          )}

          <div className="grid gap-4 sm:grid-cols-2">
            <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700 sm:col-span-2">
              Tên khu trọ
              <input
                aria-label="Tên khu trọ"
                required
                value={values.propertyName}
                onChange={(event) => updateValue("propertyName", event.target.value)}
                className="min-h-11 rounded-lg border border-slate-200 px-3 py-2 font-normal outline-none focus:border-teal-700 focus:ring-2 focus:ring-teal-100"
              />
            </label>

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

              {mode === "edit" && !addressEditing && (
                <div className="space-y-3 rounded-lg border border-slate-200 bg-slate-50 p-3">
                  <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">
                    Địa chỉ hiện đang lưu
                  </p>
                  <p className="text-sm text-slate-800">
                    {formatAddress(
                      initialProperty?.addressText,
                      initialProperty?.ward,
                      initialProperty?.district,
                      initialProperty?.city,
                    ) || "Chưa có địa chỉ."}
                  </p>
                  {legacyAddressWarning ? (
                    <p role="status" className="text-sm text-amber-800">
                      Địa chỉ cũ chưa được chuẩn hóa. Nếu chỉ sửa tên, ảnh hoặc mô tả, địa chỉ và tọa độ hiện tại sẽ được giữ nguyên.
                    </p>
                  ) : (
                    <p className="text-sm text-slate-600">
                      Địa chỉ và tọa độ hiện tại được giữ nguyên nếu bạn chỉ sửa tên, ảnh hoặc mô tả.
                    </p>
                  )}
                  <button
                    type="button"
                    disabled={catalogStatus !== "ready"}
                    onClick={beginAddressNormalization}
                    className="rounded-lg border border-[#087775] px-3 py-2 text-sm font-semibold text-[#087775] hover:bg-teal-50 disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    Chuẩn hóa địa chỉ
                  </button>
                </div>
              )}

              {addressEditing && (
                <div className="grid gap-4 sm:grid-cols-2">
                  <SearchableSingleSelect
                    id="property-province"
                    label="Tỉnh/Thành phố"
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

                  <div className="space-y-1">
                    <SearchableSingleSelect
                      id="property-ward"
                      label="Phường/Xã"
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

                  <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700 sm:col-span-2">
                    Số nhà, tên đường
                    <input
                      aria-label="Số nhà, tên đường"
                      required
                      value={values.addressText}
                      onChange={(event) => changeStreetAddress(event.target.value)}
                      placeholder="12 đường Lê Lợi"
                      className="min-h-11 rounded-lg border border-slate-200 px-3 py-2 font-normal outline-none focus:border-teal-700 focus:ring-2 focus:ring-teal-100"
                    />
                  </label>

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

                  {mode === "edit" && (
                    <p className="text-xs text-amber-800 sm:col-span-2">
                      Lưu địa chỉ chuẩn hóa sẽ bỏ giá trị quận/huyện cũ; hợp đồng và dữ liệu lịch sử không bị sửa.
                    </p>
                  )}

                </div>
              )}
            </section>

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

            <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700 sm:col-span-2">
              Mô tả (không bắt buộc)
              <textarea
                rows={3}
                value={values.description}
                onChange={(event) => updateValue("description", event.target.value)}
                className="rounded-lg border border-slate-200 px-3 py-2 font-normal"
              />
            </label>
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
