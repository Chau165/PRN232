import { useEffect, useMemo, useState, type FormEvent } from "react"
import { Link, useParams } from "react-router"
import { ApiError } from "@/api/apiClient"
import { useAuth } from "@/auth/AuthContext"
import ManagementPage from "@/components/management/ManagementPage"
import {
  ManagementEmptyState,
  ManagementErrorState,
  ManagementLoadingState,
  ManagementSuccessBanner,
} from "@/components/management/ManagementState"
import {
  managementPropertyRoomsPath,
  parsePositiveRouteId,
  routes,
} from "@/constants/routes"
import { getProperties } from "@/services/propertyService"
import { createRoomType, getRoomTypes } from "@/services/roomService"
import { addRoomTypeService, getRoomTypeServices, removeRoomTypeService } from "@/services/roomTypeService"
import { getServiceCatalog } from "@/services/serviceRateService"
import {
  createPropertyRoomType,
  getPropertyRoomTypes,
  setPropertyRoomTypeActive,
  updatePropertyRoomType,
} from "@/services/propertyRoomTypeService"
import type {
  PropertyRoomTypeDto,
  RoomTypeCatalogItem,
} from "@/types/propertyRoomType"
import type { PropertyDto } from "@/types/user"
import type { ServiceCatalogItem } from "@/types/serviceRate"
import { formatVnd } from "@/utils/publicFormatting"
import {
  MONTHLY_RENT_MAX,
  MONTHLY_RENT_MIN,
  MONTHLY_RENT_STEP,
  monthlyRentError,
  splitValidationErrors,
  validatePropertyRoomTypeForm,
  type PropertyRoomTypeFormField,
} from "@/utils/roomValidation"

const PROPERTY_ROOM_TYPE_FIELDS: readonly PropertyRoomTypeFormField[] = [
  "roomTypeId",
  "monthlyRent",
]

type CatalogRoomTypeFormField = "typeName" | "monthlyRent"
const CATALOG_ROOM_TYPE_FIELDS: readonly CatalogRoomTypeFormField[] = [
  "typeName",
  "monthlyRent",
]

type ResourceState = {
  loading: boolean
  error: string
}

function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 409) {
      return error.message || "Loại phòng đã được cấu hình tại khu trọ này."
    }
    if (error.status === 403) {
      return error.message || "Bạn không có quyền cấu hình giá khu trọ."
    }
    return error.message
  }
  return error instanceof Error
    ? error.message
    : "Không thể tải cấu hình loại phòng."
}

function catalogErrorMessage(error: unknown): string {
  if (error instanceof ApiError && error.status === 409) {
    return error.message || "Tên loại phòng đã tồn tại trong danh mục."
  }
  return errorMessage(error)
}

function normalizeRoomTypeName(value: string): string {
  return value
    .normalize("NFC")
    .split(/\s+/)
    .filter(Boolean)
    .join(" ")
}

function normalizedRoomTypeKey(value: string): string {
  return normalizeRoomTypeName(value).toLocaleLowerCase("vi")
}

function ResourceError({
  message,
  retryLabel,
  onRetry,
}: {
  message: string
  retryLabel: string
  onRetry: () => void
}) {
  return (
    <ManagementErrorState
      variant="banner"
      message={message}
      retryLabel={retryLabel}
      onRetry={onRetry}
    />
  )
}

export default function ManagementPropertyRoomTypesPage() {
  const { accessToken } = useAuth()
  const { propertyId: propertyIdParam } = useParams<{ propertyId: string }>()
  const propertyId = parsePositiveRouteId(propertyIdParam)

  const [property, setProperty] = useState<PropertyDto | null>(null)
  const [propertyState, setPropertyState] = useState<ResourceState>({
    loading: true,
    error: "",
  })
  const [catalog, setCatalog] = useState<RoomTypeCatalogItem[]>([])
  const [catalogState, setCatalogState] = useState<ResourceState>({
    loading: true,
    error: "",
  })
  const [configurations, setConfigurations] = useState<PropertyRoomTypeDto[]>([])
  const [configurationState, setConfigurationState] = useState<ResourceState>({
    loading: true,
    error: "",
  })

  const [propertyReload, setPropertyReload] = useState(0)
  const [catalogReload, setCatalogReload] = useState(0)
  const [configurationReload, setConfigurationReload] = useState(0)

  const [priceDrafts, setPriceDrafts] = useState<Record<number, string>>({})
  const [newRoomTypeId, setNewRoomTypeId] = useState("")
  const [newMonthlyRent, setNewMonthlyRent] = useState("")
  const [newCatalogName, setNewCatalogName] = useState("")
  const [newCatalogMonthlyRent, setNewCatalogMonthlyRent] = useState("")
  const [saving, setSaving] = useState(false)
  const [creatingCatalogType, setCreatingCatalogType] = useState(false)
  const [addFieldErrors, setAddFieldErrors] = useState<
    Partial<Record<PropertyRoomTypeFormField, string>>
  >({})
  const [catalogFieldErrors, setCatalogFieldErrors] = useState<
    Partial<Record<CatalogRoomTypeFormField, string>>
  >({})
  const [rowErrors, setRowErrors] = useState<Record<number, string>>({})
  const [actionError, setActionError] = useState("")
  const [success, setSuccess] = useState("")
  const [partialWarning, setPartialWarning] = useState("")

  useEffect(() => {
    if (!accessToken) return
    if (propertyId === null) {
      setPropertyState({ loading: false, error: "Khu trọ không hợp lệ." })
      return
    }

    const controller = new AbortController()
    setPropertyState({ loading: true, error: "" })

    void (async () => {
      try {
        const properties = await getProperties(accessToken, controller.signal)
        if (controller.signal.aborted) return
        const selectedProperty =
          properties.find((item) => item.propertyId === propertyId) ?? null
        setProperty(selectedProperty)
        setPropertyState({
          loading: false,
          error: selectedProperty ? "" : "Không tìm thấy khu trọ trong danh sách.",
        })
      } catch (cause) {
        if (!controller.signal.aborted) {
          setPropertyState({ loading: false, error: errorMessage(cause) })
        }
      }
    })()

    return () => controller.abort()
  }, [accessToken, propertyId, propertyReload])

  useEffect(() => {
    if (!accessToken) return
    if (propertyId === null) {
      setCatalogState({ loading: false, error: "Khu trọ không hợp lệ." })
      return
    }

    const controller = new AbortController()
    setCatalogState({ loading: true, error: "" })

    void (async () => {
      try {
        const roomTypes = await getRoomTypes(accessToken, controller.signal)
        if (controller.signal.aborted) return
        setCatalog(roomTypes)
        setCatalogState({ loading: false, error: "" })
      } catch (cause) {
        if (!controller.signal.aborted) {
          setCatalogState({ loading: false, error: errorMessage(cause) })
        }
      }
    })()

    return () => controller.abort()
  }, [accessToken, propertyId, catalogReload])

  useEffect(() => {
    if (!accessToken) return
    if (propertyId === null) {
      setConfigurationState({ loading: false, error: "Khu trọ không hợp lệ." })
      return
    }

    const controller = new AbortController()
    setConfigurationState({ loading: true, error: "" })

    void (async () => {
      try {
        const rows = await getPropertyRoomTypes(
          accessToken,
          propertyId,
          controller.signal,
        )
        if (controller.signal.aborted) return
        setConfigurations(rows)
        setPriceDrafts(
          Object.fromEntries(
            rows.map((item) => [item.roomTypeId, String(item.monthlyRent)]),
          ),
        )
        setConfigurationState({ loading: false, error: "" })
      } catch (cause) {
        if (!controller.signal.aborted) {
          setConfigurationState({ loading: false, error: errorMessage(cause) })
        }
      }
    })()

    return () => controller.abort()
  }, [accessToken, propertyId, configurationReload])

  const configuredRoomTypeIds = useMemo(
    () => new Set(configurations.map((item) => item.roomTypeId)),
    [configurations],
  )

  const availableCatalogItems = catalog.filter(
    (item) => !configuredRoomTypeIds.has(item.roomTypeId),
  )

  const actionInProgress = saving || creatingCatalogType
  const catalogIsReady = !catalogState.loading && !catalogState.error

  function resetActionMessages() {
    setActionError("")
    setSuccess("")
    setPartialWarning("")
    setAddFieldErrors({})
    setCatalogFieldErrors({})
    setRowErrors({})
  }

  async function addConfiguration(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!accessToken || propertyId === null || actionInProgress) return

    const nextFieldErrors = validatePropertyRoomTypeForm({
      mode: "create",
      roomTypeId: newRoomTypeId,
      monthlyRent: newMonthlyRent,
    })
    if (Object.keys(nextFieldErrors).length > 0) {
      resetActionMessages()
      setAddFieldErrors(nextFieldErrors)
      return
    }

    const roomTypeId = Number(newRoomTypeId)
    const monthlyRent = Number(newMonthlyRent)

    setSaving(true)
    resetActionMessages()
    try {
      await createPropertyRoomType(accessToken, propertyId, {
        roomTypeId,
        monthlyRent,
      })
      setNewRoomTypeId("")
      setNewMonthlyRent("")
      setSuccess("Đã thêm loại phòng cho khu trọ.")
      setConfigurationReload((value) => value + 1)
    } catch (cause) {
      if (cause instanceof ApiError && cause.status === 400) {
        const { fieldErrors: mapped, generalError } = splitValidationErrors(
          cause.validationErrors,
          PROPERTY_ROOM_TYPE_FIELDS,
          cause.message,
        )
        setAddFieldErrors(mapped)
        setActionError(generalError)
      } else {
        setActionError(errorMessage(cause))
      }
    } finally {
      setSaving(false)
    }
  }

  async function createCatalogTypeAndConfiguration(
    event: FormEvent<HTMLFormElement>,
  ) {
    event.preventDefault()
    if (!accessToken || propertyId === null || actionInProgress) return
    if (!catalogIsReady) {
      setActionError("Cần tải danh mục loại phòng trước khi tạo loại mới.")
      return
    }

    const typeName = normalizeRoomTypeName(newCatalogName)
    const nextCatalogErrors: Partial<Record<CatalogRoomTypeFormField, string>> =
      {}
    if (!typeName) {
      nextCatalogErrors.typeName = "Nhập tên loại phòng mới."
    } else {
      const duplicate = catalog.find(
        (item) =>
          normalizedRoomTypeKey(item.typeName) ===
          normalizedRoomTypeKey(typeName),
      )
      if (duplicate) {
        nextCatalogErrors.typeName = `Loại phòng "${duplicate.typeName}" đã có trong danh mục. Hãy chọn loại phòng đó ở ô bên trên.`
      }
    }

    const rentErr = monthlyRentError(newCatalogMonthlyRent)
    if (rentErr) {
      nextCatalogErrors.monthlyRent = rentErr
    }

    if (Object.keys(nextCatalogErrors).length > 0) {
      resetActionMessages()
      setCatalogFieldErrors(nextCatalogErrors)
      return
    }

    const monthlyRent = Number(newCatalogMonthlyRent)

    setCreatingCatalogType(true)
    resetActionMessages()

    try {
      // RoomTypes.Price is the legacy catalog field. The rent that applies to this
      // property is written only to PropertyRoomTypes.MonthlyRent below.
      const created = await createRoomType(accessToken, {
        typeName,
        price: 0,
      })

      try {
        await createPropertyRoomType(accessToken, propertyId, {
          roomTypeId: created.roomTypeId,
          monthlyRent,
        })
        setNewCatalogName("")
        setNewCatalogMonthlyRent("")
        setSuccess(
          `Đã tạo loại phòng "${created.typeName}" và cấu hình giá cho khu trọ.`,
        )
        setCatalogReload((value) => value + 1)
        setConfigurationReload((value) => value + 1)
      } catch (cause) {
        setNewCatalogName("")
        setNewCatalogMonthlyRent("")
        setCatalog((current) =>
          current.some((item) => item.roomTypeId === created.roomTypeId)
            ? current
            : [
                ...current,
                {
                  roomTypeId: created.roomTypeId,
                  typeName: created.typeName,
                  price: created.price,
                },
              ],
        )
        setNewRoomTypeId(String(created.roomTypeId))
        setNewMonthlyRent(String(monthlyRent))
        setPartialWarning(
          `Đã tạo loại phòng "${created.typeName}" trong danh mục, nhưng bước gắn giá thuê cho khu trọ thất bại: ${errorMessage(cause)} ` +
            "Loại phòng đã được chọn sẵn ở mục \"Thêm loại phòng\". Kiểm tra giá thuê rồi bấm \"Thêm cấu hình\" để thử lại; không cần tạo lại danh mục.",
        )
        setConfigurationReload((value) => value + 1)
      }
    } catch (cause) {
      if (cause instanceof ApiError && cause.status === 400) {
        const { fieldErrors: mapped, generalError } = splitValidationErrors(
          cause.validationErrors,
          CATALOG_ROOM_TYPE_FIELDS,
          cause.message,
        )
        setCatalogFieldErrors(mapped)
        setActionError(generalError)
      } else {
        setActionError(catalogErrorMessage(cause))
      }
    } finally {
      setCreatingCatalogType(false)
    }
  }

  async function savePrice(item: PropertyRoomTypeDto) {
    if (!accessToken || actionInProgress) return

    const draftValue = priceDrafts[item.roomTypeId] ?? ""
    const rentErr = monthlyRentError(draftValue)
    if (rentErr) {
      resetActionMessages()
      setRowErrors({ [item.roomTypeId]: rentErr })
      return
    }

    const monthlyRent = Number(draftValue)

    setSaving(true)
    resetActionMessages()
    try {
      await updatePropertyRoomType(
        accessToken,
        item.propertyId,
        item.roomTypeId,
        { monthlyRent },
      )
      setSuccess(`Đã cập nhật giá ${item.roomTypeName}.`)
      setConfigurationReload((value) => value + 1)
    } catch (cause) {
      if (cause instanceof ApiError && cause.status === 400) {
        const { fieldErrors: mapped, generalError } = splitValidationErrors(
          cause.validationErrors,
          ["monthlyRent"] as const,
          cause.message,
        )
        if (mapped.monthlyRent) {
          setRowErrors({ [item.roomTypeId]: mapped.monthlyRent })
        }
        setActionError(generalError)
      } else {
        setActionError(errorMessage(cause))
      }
    } finally {
      setSaving(false)
    }
  }

  async function toggleStatus(item: PropertyRoomTypeDto) {
    if (!accessToken || actionInProgress) return

    setSaving(true)
    resetActionMessages()
    try {
      await setPropertyRoomTypeActive(
        accessToken,
        item.propertyId,
        item.roomTypeId,
        !item.isActive,
      )
      setSuccess(
        item.isActive
          ? "Đã ngừng dùng cấu hình cho giao dịch mới."
          : "Đã kích hoạt cấu hình.",
      )
      setConfigurationReload((value) => value + 1)
    } catch (cause) {
      setActionError(errorMessage(cause))
    } finally {
      setSaving(false)
    }
  }

  return (
    <ManagementPage
      title="Loại phòng và giá thuê"
      description={
        propertyState.loading
          ? "Đang tải thông tin khu trọ..."
          : property
            ? `Cấu hình riêng cho ${property.propertyName}. Giá chung trong danh mục không được dùng làm giá thuê.`
            : "Cấu hình giá thuê riêng theo khu trọ."
      }
      breadcrumbs={[
        { label: "Khu trọ", to: routes.managementProperties },
        { label: property?.propertyName ?? "Loại phòng và giá thuê" },
      ]}
    >
      <Link
        to={routes.managementProperties}
        className="mb-5 inline-block text-sm font-semibold text-[#087775] hover:underline"
      >
        ← Danh sách khu trọ
      </Link>

      {propertyState.error && (
        <ResourceError
          message={`Không tải được thông tin khu trọ: ${propertyState.error}`}
          retryLabel="Thử tải lại khu trọ"
          onRetry={() => setPropertyReload((value) => value + 1)}
        />
      )}

      {actionError && <ManagementErrorState variant="banner" message={actionError} />}
      {partialWarning && (
        <p
          role="alert"
          className="mb-4 rounded-lg bg-amber-50 px-4 py-3 text-sm text-amber-800"
        >
          {partialWarning}
        </p>
      )}
      {success && <ManagementSuccessBanner message={success} />}

      <section className="rounded-2xl bg-white p-5 shadow-sm sm:p-6">
        <h2 className="font-display text-lg font-bold text-slate-900">
          Thêm loại phòng
        </h2>

        {catalogState.loading && (
          <div className="mt-3">
            <ManagementLoadingState
              variant="inline"
              message="Đang tải danh mục loại phòng..."
            />
          </div>
        )}
        {catalogState.error && (
          <div className="mt-4">
            <ResourceError
              message={`Không tải được danh mục loại phòng: ${catalogState.error}`}
              retryLabel="Thử tải lại danh mục"
              onRetry={() => setCatalogReload((value) => value + 1)}
            />
          </div>
        )}

        <form
          onSubmit={addConfiguration}
          noValidate
          className="mt-4 grid gap-3 md:grid-cols-[minmax(0,1fr)_220px_auto] md:items-start"
        >
          <div>
            <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
              Danh mục loại phòng
              <select
                aria-label="Danh mục loại phòng"
                aria-invalid={Boolean(addFieldErrors.roomTypeId)}
                value={newRoomTypeId}
                onChange={(event) => {
                  setNewRoomTypeId(event.target.value)
                  setAddFieldErrors((current) => {
                    if (!current.roomTypeId) return current
                    const next = { ...current }
                    delete next.roomTypeId
                    return next
                  })
                }}
                disabled={!catalogIsReady}
                className="h-11 rounded-lg border border-slate-200 px-3 font-normal disabled:bg-slate-50 disabled:text-slate-400"
              >
                <option value="">Chọn loại phòng</option>
                {availableCatalogItems.map((item) => (
                  <option key={item.roomTypeId} value={item.roomTypeId}>
                    {item.typeName}
                  </option>
                ))}
              </select>
            </label>
            {addFieldErrors.roomTypeId && (
              <p role="alert" className="mt-1 text-xs text-rose-600">
                {addFieldErrors.roomTypeId}
              </p>
            )}
          </div>
          <div>
            <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
              Giá thuê / tháng (VND)
              <input
                aria-label="Giá thuê mới"
                aria-invalid={Boolean(addFieldErrors.monthlyRent)}
                type="number"
                min={MONTHLY_RENT_MIN}
                max={MONTHLY_RENT_MAX}
                step={MONTHLY_RENT_STEP}
                value={newMonthlyRent}
                onChange={(event) => {
                  setNewMonthlyRent(event.target.value)
                  setAddFieldErrors((current) => {
                    if (!current.monthlyRent) return current
                    const next = { ...current }
                    delete next.monthlyRent
                    return next
                  })
                }}
                className="h-11 rounded-lg border border-slate-200 px-3 font-normal"
              />
            </label>
            {addFieldErrors.monthlyRent && (
              <p role="alert" className="mt-1 text-xs text-rose-600">
                {addFieldErrors.monthlyRent}
              </p>
            )}
          </div>
          <button
            type="submit"
            disabled={!catalogIsReady || actionInProgress || propertyId === null}
            className="md:mt-6 rounded-lg bg-[#087775] px-5 py-3 text-sm font-bold text-white disabled:opacity-50"
          >
            {saving ? "Đang lưu..." : "Thêm cấu hình"}
          </button>
        </form>

        <div className="mt-6 border-t border-slate-100 pt-5">
          <h3 className="font-display text-base font-bold text-slate-900">
            Tạo loại phòng mới trong danh mục
          </h3>
          <p className="mt-1 text-xs leading-5 text-slate-500">
            Loại phòng mới chỉ được thêm vào danh mục dùng chung. Giá thuê thực
            tế được lưu riêng cho khu trọ này; hệ thống không tạo phòng vật lý và
            không gắn loại phòng mới vào khu trọ khác.
          </p>
          <form
            onSubmit={createCatalogTypeAndConfiguration}
            noValidate
            className="mt-3 grid gap-3 md:grid-cols-[minmax(0,1fr)_220px_auto] md:items-start"
          >
            <div>
              <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
                Tên loại phòng mới
                <input
                  aria-label="Tên loại phòng mới"
                  aria-invalid={Boolean(catalogFieldErrors.typeName)}
                  value={newCatalogName}
                  onChange={(event) => {
                    setNewCatalogName(event.target.value)
                    setCatalogFieldErrors((current) => {
                      if (!current.typeName) return current
                      const next = { ...current }
                      delete next.typeName
                      return next
                    })
                  }}
                  className="h-11 rounded-lg border border-slate-200 px-3 font-normal"
                />
              </label>
              {catalogFieldErrors.typeName && (
                <p role="alert" className="mt-1 text-xs text-rose-600">
                  {catalogFieldErrors.typeName}
                </p>
              )}
            </div>
            <div>
              <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
                Giá thuê / tháng (VND)
                <input
                  aria-label="Giá thuê loại phòng mới"
                  aria-invalid={Boolean(catalogFieldErrors.monthlyRent)}
                  type="number"
                  min={MONTHLY_RENT_MIN}
                  max={MONTHLY_RENT_MAX}
                  step={MONTHLY_RENT_STEP}
                  value={newCatalogMonthlyRent}
                  onChange={(event) => {
                    setNewCatalogMonthlyRent(event.target.value)
                    setCatalogFieldErrors((current) => {
                      if (!current.monthlyRent) return current
                      const next = { ...current }
                      delete next.monthlyRent
                      return next
                    })
                  }}
                  className="h-11 rounded-lg border border-slate-200 px-3 font-normal"
                />
              </label>
              {catalogFieldErrors.monthlyRent && (
                <p role="alert" className="mt-1 text-xs text-rose-600">
                  {catalogFieldErrors.monthlyRent}
                </p>
              )}
            </div>
            <button
              type="submit"
              disabled={
                !catalogIsReady || actionInProgress || propertyId === null
              }
              className="md:mt-6 rounded-lg border border-[#087775] bg-white px-5 py-3 text-sm font-bold text-[#087775] disabled:opacity-50"
            >
              {creatingCatalogType ? "Đang tạo..." : "Tạo và cấu hình"}
            </button>
          </form>
        </div>
      </section>

      <section className="mt-6 rounded-2xl bg-white shadow-sm">
        <h2 className="border-b border-slate-100 px-5 py-4 font-display text-lg font-bold text-slate-900">
          Cấu hình hiện có
        </h2>

        {configurationState.error && (
          <div className="border-b border-slate-100 px-5 pt-4">
            <ResourceError
              message={`Không tải được cấu hình loại phòng: ${configurationState.error}`}
              retryLabel="Thử tải lại cấu hình"
              onRetry={() => setConfigurationReload((value) => value + 1)}
            />
          </div>
        )}

        {configurationState.loading && configurations.length === 0 && (
          <ManagementLoadingState
            variant="table"
            rows={3}
            message="Đang tải cấu hình..."
          />
        )}

        {!configurationState.loading &&
          !configurationState.error &&
          configurations.length === 0 && (
            <ManagementEmptyState
              icon="building"
              message="Chưa có loại phòng nào được cấu hình."
            />
          )}

        {configurations.length > 0 && (
          <div className="divide-y divide-slate-100">
            {configurations.map((item) => (
              <article
                key={item.roomTypeId}
                className="grid gap-4 px-5 py-5 md:grid-cols-[minmax(140px,1fr)_220px_160px_auto] md:items-center"
              >
                <div>
                  <h3 className="font-semibold text-slate-800">
                    {item.roomTypeName}
                  </h3>
                  <p className="mt-1 text-xs text-slate-500">
                    {item.totalRoomCount} phòng · {item.availableRoomCount} hiện
                    có thể thuê
                  </p>
                </div>
                <div>
                  <label className="flex flex-col gap-1 text-xs font-semibold text-slate-500">
                    Giá thuê / tháng
                    <input
                      aria-label={`Giá thuê ${item.roomTypeName}`}
                      aria-invalid={Boolean(rowErrors[item.roomTypeId])}
                      type="number"
                      min={MONTHLY_RENT_MIN}
                      max={MONTHLY_RENT_MAX}
                      step={MONTHLY_RENT_STEP}
                      value={priceDrafts[item.roomTypeId] ?? ""}
                      onChange={(event) => {
                        setPriceDrafts((current) => ({
                          ...current,
                          [item.roomTypeId]: event.target.value,
                        }))
                        setRowErrors((current) => {
                          if (!current[item.roomTypeId]) return current
                          const next = { ...current }
                          delete next[item.roomTypeId]
                          return next
                        })
                      }}
                      className="h-10 rounded-lg border border-slate-200 px-3 text-sm font-normal text-slate-800"
                    />
                  </label>
                  {rowErrors[item.roomTypeId] && (
                    <p role="alert" className="mt-1 text-xs text-rose-600">
                      {rowErrors[item.roomTypeId]}
                    </p>
                  )}
                </div>
                <p className="text-sm font-bold text-[#087775]">
                  {formatVnd(item.monthlyRent)} / tháng
                </p>
                <div className="flex flex-wrap gap-2">
                  <button
                    type="button"
                    disabled={actionInProgress}
                    onClick={() => void savePrice(item)}
                    className="rounded-lg border border-[#087775] px-3 py-2 text-xs font-bold text-[#087775] disabled:opacity-50"
                  >
                    Lưu giá
                  </button>
                  <button
                    type="button"
                    disabled={actionInProgress}
                    onClick={() => void toggleStatus(item)}
                    className={`rounded-lg px-3 py-2 text-xs font-bold disabled:opacity-50 ${
                      item.isActive
                        ? "bg-amber-50 text-amber-800"
                        : "bg-emerald-50 text-emerald-800"
                    }`}
                  >
                    {item.isActive ? "Ngừng dùng" : "Kích hoạt"}
                  </button>
                  <Link
                    to={`${managementPropertyRoomsPath(item.propertyId)}?roomTypeId=${item.roomTypeId}`}
                    className="rounded-lg bg-slate-100 px-3 py-2 text-xs font-bold text-slate-700"
                  >
                    Xem phòng
                  </Link>
                </div>
                {!item.isActive && (
                  <p className="text-xs font-semibold text-amber-700 md:col-span-4">
                    Đang ngừng dùng cho giao dịch mới.
                  </p>
                )}
                {accessToken && (
                  <div className="md:col-span-4">
                    <RoomTypeServicesEditor
                      accessToken={accessToken}
                      roomTypeId={item.roomTypeId}
                      roomTypeName={item.roomTypeName}
                    />
                  </div>
                )}
              </article>
            ))}
          </div>
        )}
      </section>
    </ManagementPage>
  )
}

function RoomTypeServicesEditor({ accessToken, roomTypeId, roomTypeName }: {
  accessToken: string
  roomTypeId: number
  roomTypeName: string
}) {
  const [catalog, setCatalog] = useState<ServiceCatalogItem[]>([])
  const [assigned, setAssigned] = useState<ServiceCatalogItem[]>([])
  const [selectedId, setSelectedId] = useState("")
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState("")
  const [reload, setReload] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    setLoading(true)
    setError("")
    void Promise.all([
      getServiceCatalog(controller.signal),
      getRoomTypeServices(accessToken, roomTypeId, controller.signal),
    ])
      .then(([allServices, roomServices]) => {
        setCatalog(allServices)
        setAssigned(roomServices.map((service) => ({
          serviceId: service.serviceId,
          name: service.name,
          price: service.price,
        })))
      })
      .catch((cause: unknown) => {
        if (!controller.signal.aborted) setError(errorMessage(cause))
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })
    return () => controller.abort()
  }, [accessToken, roomTypeId, reload])

  const assignedIds = new Set(assigned.map((service) => service.serviceId))
  const available = catalog.filter((service) => !assignedIds.has(service.serviceId))

  async function changeService(serviceId: number, add: boolean) {
    setBusy(true)
    setError("")
    try {
      if (add) await addRoomTypeService(accessToken, roomTypeId, serviceId)
      else await removeRoomTypeService(accessToken, roomTypeId, serviceId)
      setReload((value) => value + 1)
    } catch (cause) {
      setError(errorMessage(cause))
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="rounded-xl border border-slate-100 bg-slate-50 p-4" aria-label={`Dịch vụ cho ${roomTypeName}`}>
      <h4 className="text-sm font-bold text-slate-800">Dịch vụ áp dụng cho {roomTypeName}</h4>
      <p className="mt-1 text-xs text-slate-500">Hóa đơn chỉ tính dịch vụ được gắn với loại phòng này.</p>
      {loading && <p role="status" className="mt-2 text-xs text-slate-500">Đang tải dịch vụ...</p>}
      {error && <p role="alert" className="mt-2 text-xs text-rose-600">{error}</p>}
      {!loading && !error && (
        <>
          <div className="mt-3 flex flex-wrap gap-2">
            <select aria-label={`Chọn dịch vụ ${roomTypeName}`} value={selectedId}
              onChange={(event) => setSelectedId(event.target.value)}
              className="h-9 min-w-52 rounded-lg border border-slate-200 bg-white px-3 text-sm">
              <option value="">Chọn dịch vụ để thêm</option>
              {available.map((service) => <option key={service.serviceId} value={service.serviceId}>{service.name}</option>)}
            </select>
            <button type="button" disabled={busy || !selectedId}
              onClick={() => { void changeService(Number(selectedId), true); setSelectedId("") }}
              className="rounded-lg bg-teal-700 px-3 py-2 text-xs font-bold text-white disabled:opacity-40">
              Thêm dịch vụ
            </button>
          </div>
          {assigned.length === 0 ? <p className="mt-2 text-xs text-slate-500">Chưa gắn dịch vụ.</p> : (
            <ul className="mt-2 flex flex-wrap gap-2">
              {assigned.map((service) => (
                <li key={service.serviceId} className="flex items-center gap-2 rounded-lg bg-white px-3 py-2 text-xs text-slate-700">
                  {service.name}
                  <button type="button" aria-label={`Gỡ dịch vụ ${service.name}`} disabled={busy}
                    onClick={() => void changeService(service.serviceId, false)}
                    className="font-bold text-rose-600 disabled:opacity-40">Gỡ</button>
                </li>
              ))}
            </ul>
          )}
        </>
      )}
    </section>
  )
}
