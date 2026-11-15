import { useCallback, useEffect, useRef, useState, type FormEvent } from "react"
import { ApiError } from "@/api/apiClient"
import { useAuth } from "@/auth/AuthContext"
import StatusBadge from "@/components/customer/StatusBadge"
import { createPropertyServiceRate, getPropertyServiceRates, getServiceCatalog } from "@/services/serviceRateService"
import type { PropertyServiceRateDto, ServiceCatalogItem } from "@/types/serviceRate"
import {
  parseServiceRatePriceInput,
  serviceIdError,
  serviceRatePriceError,
} from "@/utils/propertyValidation"
import { splitValidationErrors } from "@/utils/roomValidation"
import { formatVnd } from "@/utils/publicFormatting"

type RateFormField =
  | "serviceId"
  | "price"
  | "effectiveFrom"
  | "effectiveTo"

const RATE_FORM_FIELDS: readonly RateFormField[] = [
  "serviceId",
  "price",
  "effectiveFrom",
  "effectiveTo",
]

const unitLabels: Record<string, string> = {
  kwh: "kWh",
  m3: "m³",
  room_month: "phòng/tháng",
  person_month: "người/tháng",
  vehicle_month: "xe/tháng",
}

const inputClass =
  "mt-1 h-10 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm text-slate-700 outline-none focus:border-[#087775] focus:ring-2 focus:ring-[#087775]/15"

function unitLabel(unitCode: string) {
  return unitLabels[unitCode] ?? unitCode
}

// "2026-11-01" -> "11/2026"
function monthLabel(value: string) {
  const [year, month] = value.split("-")
  return `${month}/${year}`
}

// effectiveTo là mốc loại trừ, nên tháng cuối cùng còn áp dụng là tháng liền trước.
function lastMonthLabel(effectiveTo: string) {
  const [year, month] = effectiveTo.split("-").map(Number)
  const date = new Date(year, month - 2, 1)
  return `${String(date.getMonth() + 1).padStart(2, "0")}/${date.getFullYear()}`
}

function currentMonthStart(now = new Date()) {
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-01`
}

function isActive(rate: PropertyServiceRateDto, month: string) {
  return rate.effectiveFrom <= month && (rate.effectiveTo === null || rate.effectiveTo > month)
}

function rateErrorMessage(error: unknown) {
  if (error instanceof ApiError) {
    if (error.status === 409) return "Kỳ áp dụng bị trùng với một mức giá đã có của dịch vụ này. Giá cũ không bị thay đổi."
    if (error.status === 403) return "Bạn không có quyền xem hoặc thêm giá dịch vụ của khu trọ này."
    if (error.status === 404) return error.message || "Không tìm thấy khu trọ hoặc dịch vụ."
  }
  return error instanceof Error ? error.message : "Yêu cầu không thành công. Vui lòng thử lại."
}

export default function PropertyServiceRatesPanel({ propertyId }: { propertyId: number }) {
  const { accessToken, user } = useAuth()
  const isAdmin = user?.role === "ADMIN"
  const [rates, setRates] = useState<PropertyServiceRateDto[] | null>(null)
  const [catalog, setCatalog] = useState<ServiceCatalogItem[]>([])
  const [loadError, setLoadError] = useState<string | null>(null)
  const [reload, setReload] = useState(0)

  useEffect(() => {
    if (!accessToken) return
    const controller = new AbortController()
    setLoadError(null)
    Promise.all([
      getPropertyServiceRates(accessToken, propertyId, controller.signal),
      isAdmin ? getServiceCatalog(controller.signal) : Promise.resolve([] as ServiceCatalogItem[]),
    ])
      .then(([loadedRates, loadedCatalog]) => {
        setRates(loadedRates)
        setCatalog(loadedCatalog)
      })
      .catch((error: unknown) => {
        if (!controller.signal.aborted) setLoadError(rateErrorMessage(error))
      })
    return () => controller.abort()
  }, [accessToken, propertyId, isAdmin, reload])

  const thisMonth = currentMonthStart()

  return (
    <section className="mt-6" aria-labelledby={`service-rates-${propertyId}`}>
      <div className="flex items-center justify-between gap-3">
        <h3 id={`service-rates-${propertyId}`} className="text-sm font-bold text-slate-800">Bảng giá dịch vụ</h3>
        <button type="button" onClick={() => setReload((value) => value + 1)} className="text-xs font-semibold text-[#087775] hover:underline">
          Tải lại
        </button>
      </div>
      <p className="mt-1 text-xs leading-5 text-slate-500">
        Hoá đơn lấy dịch vụ theo loại phòng và lấy giá ở bảng này. Dịch vụ thiếu giá trong tháng sẽ không lập được hoá đơn.
      </p>
      {loadError && <p role="alert" className="mt-3 text-sm text-rose-600">{loadError}</p>}
      {!loadError && rates === null && <p role="status" className="mt-3 text-sm text-slate-500">Đang tải bảng giá...</p>}
      {rates?.length === 0 && <p className="mt-3 rounded-lg bg-amber-50 p-3 text-sm text-amber-800">Khu trọ chưa có giá dịch vụ nào.</p>}
      {rates && rates.length > 0 && (
        <ul className="mt-3 divide-y divide-slate-100 rounded-xl border border-slate-100">
          {rates.map((rate) => (
            <li key={rate.rateId} className="flex flex-wrap items-center justify-between gap-2 px-4 py-3 text-sm">
              <div>
                <p className="font-semibold text-slate-800">{rate.serviceName}</p>
                <p className="mt-0.5 text-xs text-slate-500">
                  Từ {monthLabel(rate.effectiveFrom)} {rate.effectiveTo ? `đến hết ${lastMonthLabel(rate.effectiveTo)}` : "· không thời hạn"}
                </p>
              </div>
              <div className="flex items-center gap-2">
                <span className="font-bold text-[#087775]">{formatVnd(rate.price)}/{unitLabel(rate.unitCode)}</span>
                {isActive(rate, thisMonth) && <StatusBadge tone="green">Đang áp dụng</StatusBadge>}
              </div>
            </li>
          ))}
        </ul>
      )}
      {isAdmin && accessToken && rates !== null && (
        <AddRateForm
          accessToken={accessToken}
          propertyId={propertyId}
          catalog={catalog}
          defaultMonth={thisMonth}
          onCreated={() => setReload((value) => value + 1)}
        />
      )}
    </section>
  )
}

function AddRateForm({ accessToken, propertyId, catalog, defaultMonth, onCreated }: {
  accessToken: string
  propertyId: number
  catalog: ServiceCatalogItem[]
  defaultMonth: string
  onCreated: () => void
}) {
  const [serviceId, setServiceId] = useState("")
  const [price, setPrice] = useState("")
  // input type="month" dùng dạng yyyy-MM; gửi lên BE thêm "-01".
  const [from, setFrom] = useState(defaultMonth.slice(0, 7))
  const [to, setTo] = useState("")
  const [fieldErrors, setFieldErrors] = useState<
    Partial<Record<RateFormField, string>>
  >({})
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)
  const [pending, setPending] = useState(false)
  const locked = useRef(false)

  const selectedService = catalog.find((service) => service.serviceId === Number(serviceId))

  function clearFieldError(field: RateFormField) {
    setFieldErrors((current) => {
      if (!current[field]) return current
      const next = { ...current }
      delete next[field]
      return next
    })
    setError(null)
  }

  const submit = useCallback(async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (locked.current) return
    setSuccess(null)
    setError(null)

    const svcErr = serviceIdError(serviceId)
    if (svcErr) {
      setFieldErrors({ serviceId: svcErr })
      return
    }
    if (!selectedService?.defaultUnitCode || !selectedService.defaultBillingBasis) {
      setFieldErrors({ serviceId: "Dịch vụ này chưa có cấu hình tính giá mặc định." })
      return
    }
    const priceErr = serviceRatePriceError(price)
    if (priceErr) {
      setFieldErrors({ price: priceErr })
      return
    }
    if (!/^\d{4}-\d{2}$/.test(from)) {
      setFieldErrors({ effectiveFrom: "Vui lòng chọn tháng bắt đầu áp dụng." })
      return
    }
    if (to && to <= from) {
      setFieldErrors({ effectiveTo: "Tháng kết thúc phải sau tháng bắt đầu." })
      return
    }

    const amount = parseServiceRatePriceInput(price)
    locked.current = true
    setPending(true)
    setFieldErrors({})
    setError(null)
    try {
      const created = await createPropertyServiceRate(accessToken, propertyId, {
        serviceId: Number(serviceId),
        price: amount,
        unitCode: selectedService.defaultUnitCode,
        billingBasis: selectedService.defaultBillingBasis,
        effectiveFrom: `${from}-01`,
        effectiveTo: to ? `${to}-01` : null,
      })
      setSuccess(`Đã thêm giá ${created.serviceName}: ${formatVnd(created.price)}/${unitLabel(created.unitCode)}.`)
      setServiceId("")
      setPrice("")
      setTo("")
      onCreated()
    } catch (createError) {
      if (createError instanceof ApiError && createError.status === 400) {
        const normalizedErrors = createError.validationErrors
          ? Object.fromEntries(
              Object.entries(createError.validationErrors).map(([key, msgs]) => {
                const lower = key.toLowerCase()
                if (lower === "unitcode" || lower === "billingbasis") {
                  return ["serviceId", msgs]
                }
                return [key, msgs]
              }),
            )
          : undefined
        const { fieldErrors: mapped, generalError } = splitValidationErrors(
          normalizedErrors,
          RATE_FORM_FIELDS,
          createError.message,
        )
        setFieldErrors(mapped)
        setError(generalError || null)
      } else {
        setError(rateErrorMessage(createError))
      }
    } finally {
      locked.current = false
      setPending(false)
    }
  }, [accessToken, catalog, from, onCreated, price, propertyId, selectedService, serviceId, to])

  return (
    <form onSubmit={submit} noValidate className="mt-4 space-y-3 rounded-xl bg-slate-50 p-4">
      <h4 className="text-sm font-bold text-slate-800">Thêm mức giá</h4>
      <div className="grid gap-3 sm:grid-cols-2">
        <div>
          <label className="block text-xs font-semibold text-slate-600">
            Dịch vụ
            <select
              aria-invalid={Boolean(fieldErrors.serviceId)}
              value={serviceId}
              onChange={(event) => {
                setServiceId(event.target.value)
                clearFieldError("serviceId")
              }}
              className={inputClass}
            >
              <option value="" disabled>Chọn dịch vụ</option>
              {catalog.map((service) => <option key={service.serviceId} value={service.serviceId}>{service.name}</option>)}
            </select>
          </label>
          {fieldErrors.serviceId && (
            <p role="alert" className="mt-1 text-xs text-rose-600">
              {fieldErrors.serviceId}
            </p>
          )}
        </div>
        <div>
          <label className="block text-xs font-semibold text-slate-600">
            Đơn giá (VND)
            <input
              aria-invalid={Boolean(fieldErrors.price)}
              value={price}
              inputMode="numeric"
              onChange={(event) => {
                setPrice(event.target.value)
                clearFieldError("price")
              }}
              className={inputClass}
            />
          </label>
          {fieldErrors.price && (
            <p role="alert" className="mt-1 text-xs text-rose-600">
              {fieldErrors.price}
            </p>
          )}
        </div>
        <div>
          <label className="block text-xs font-semibold text-slate-600">
            Áp dụng từ tháng
            <input
              type="month"
              aria-invalid={Boolean(fieldErrors.effectiveFrom)}
              value={from}
              onChange={(event) => {
                setFrom(event.target.value)
                clearFieldError("effectiveFrom")
              }}
              className={inputClass}
            />
          </label>
          {fieldErrors.effectiveFrom && (
            <p role="alert" className="mt-1 text-xs text-rose-600">
              {fieldErrors.effectiveFrom}
            </p>
          )}
        </div>
        <div className="sm:col-span-2">
          <label className="block text-xs font-semibold text-slate-600">
            Ngừng áp dụng từ tháng (để trống nếu không thời hạn)
            <input
              type="month"
              aria-invalid={Boolean(fieldErrors.effectiveTo)}
              value={to}
              onChange={(event) => {
                setTo(event.target.value)
                clearFieldError("effectiveTo")
              }}
              className={inputClass}
            />
          </label>
          {fieldErrors.effectiveTo && (
            <p role="alert" className="mt-1 text-xs text-rose-600">
              {fieldErrors.effectiveTo}
            </p>
          )}
        </div>
      </div>
      <p className="text-xs leading-5 text-slate-500">
        Giá đã tạo không sửa hoặc xoá được để giữ đúng các hoá đơn cũ. Muốn đổi giá theo thời gian, hãy đặt tháng ngừng áp dụng cho giá cũ ngay khi tạo.
      </p>
      {error && <p role="alert" className="text-sm text-rose-600">{error}</p>}
      {success && <p role="status" className="text-sm text-emerald-700">{success}</p>}
      <button type="submit" disabled={pending} className="rounded-lg bg-[#087775] px-4 py-2.5 text-sm font-bold text-white hover:bg-[#066a68] disabled:opacity-60">
        {pending ? "Đang lưu..." : "Thêm mức giá"}
      </button>
    </form>
  )
}
