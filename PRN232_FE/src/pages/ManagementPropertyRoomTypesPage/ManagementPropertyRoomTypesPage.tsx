import { useEffect, useMemo, useState, type FormEvent } from "react"
import { Link, useParams } from "react-router"
import { ApiError } from "@/api/apiClient"
import { useAuth } from "@/auth/AuthContext"
import ManagementPage from "@/components/management/ManagementPage"
import {
  managementPropertyRoomsPath,
  parsePositiveRouteId,
  routes,
} from "@/constants/routes"
import { getProperties } from "@/services/propertyService"
import { createRoomType, getRoomTypes } from "@/services/roomService"
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
import { formatVnd } from "@/utils/publicFormatting"

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
    if (error.status === 400) {
      const messages = Object.values(error.validationErrors ?? {}).flat()
      return messages.length ? messages.join(" ") : error.message
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
    <div
      role="alert"
      className="mb-4 flex flex-wrap items-center gap-3 rounded-lg bg-rose-50 px-4 py-3 text-sm text-rose-700"
    >
      <p className="min-w-0 flex-1">{message}</p>
      <button
        type="button"
        onClick={onRetry}
        className="rounded-lg border border-rose-200 bg-white px-3 py-1.5 text-xs font-bold text-rose-700"
      >
        {retryLabel}
      </button>
    </div>
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
  }

  async function addConfiguration(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!accessToken || propertyId === null || actionInProgress) return

    const roomTypeId = Number(newRoomTypeId)
    const monthlyRent = Number(newMonthlyRent)
    if (
      !Number.isSafeInteger(roomTypeId) ||
      roomTypeId < 1 ||
      !Number.isFinite(monthlyRent) ||
      monthlyRent <= 0
    ) {
      setActionError("Chọn loại phòng và nhập giá thuê lớn hơn 0.")
      return
    }

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
      setActionError(errorMessage(cause))
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
    const monthlyRent = Number(newCatalogMonthlyRent)
    if (!typeName) {
      setActionError("Nhập tên loại phòng mới.")
      return
    }
    if (!Number.isFinite(monthlyRent) || monthlyRent <= 0) {
      setActionError("Giá thuê của loại phòng mới phải lớn hơn 0.")
      return
    }

    const duplicate = catalog.find(
      (item) => normalizedRoomTypeKey(item.typeName) === normalizedRoomTypeKey(typeName),
    )
    if (duplicate) {
      setActionError(
        `Loại phòng "${duplicate.typeName}" đã có trong danh mục. Hãy chọn loại phòng đó ở ô bên trên.`,
      )
      return
    }

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
      setActionError(catalogErrorMessage(cause))
    } finally {
      setCreatingCatalogType(false)
    }
  }

  async function savePrice(item: PropertyRoomTypeDto) {
    if (!accessToken || actionInProgress) return

    const monthlyRent = Number(priceDrafts[item.roomTypeId])
    if (!Number.isFinite(monthlyRent) || monthlyRent <= 0) {
      setActionError("Giá thuê phải lớn hơn 0.")
      return
    }

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
      setActionError(errorMessage(cause))
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

      {actionError && (
        <p
          role="alert"
          className="mb-4 rounded-lg bg-rose-50 px-4 py-3 text-sm text-rose-700"
        >
          {actionError}
        </p>
      )}
      {partialWarning && (
        <p
          role="alert"
          className="mb-4 rounded-lg bg-amber-50 px-4 py-3 text-sm text-amber-800"
        >
          {partialWarning}
        </p>
      )}
      {success && (
        <p
          role="status"
          className="mb-4 rounded-lg bg-emerald-50 px-4 py-3 text-sm text-emerald-800"
        >
          {success}
        </p>
      )}

      <section className="rounded-2xl bg-white p-5 shadow-sm sm:p-6">
        <h2 className="font-display text-lg font-bold text-slate-900">
          Thêm loại phòng
        </h2>

        {catalogState.loading && (
          <p role="status" className="mt-3 text-sm text-slate-500">
            Đang tải danh mục loại phòng...
          </p>
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
          className="mt-4 grid gap-3 md:grid-cols-[minmax(0,1fr)_220px_auto]"
        >
          <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
            Danh mục loại phòng
            <select
              aria-label="Danh mục loại phòng"
              value={newRoomTypeId}
              onChange={(event) => setNewRoomTypeId(event.target.value)}
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
          <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
            Giá thuê / tháng (VND)
            <input
              aria-label="Giá thuê mới"
              type="number"
              min="1"
              step="1"
              value={newMonthlyRent}
              onChange={(event) => setNewMonthlyRent(event.target.value)}
              className="h-11 rounded-lg border border-slate-200 px-3 font-normal"
            />
          </label>
          <button
            type="submit"
            disabled={!catalogIsReady || actionInProgress || propertyId === null}
            className="self-end rounded-lg bg-[#087775] px-5 py-3 text-sm font-bold text-white disabled:opacity-50"
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
            className="mt-3 grid gap-3 md:grid-cols-[minmax(0,1fr)_220px_auto]"
          >
            <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
              Tên loại phòng mới
              <input
                aria-label="Tên loại phòng mới"
                value={newCatalogName}
                onChange={(event) => setNewCatalogName(event.target.value)}
                className="h-11 rounded-lg border border-slate-200 px-3 font-normal"
              />
            </label>
            <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
              Giá thuê / tháng (VND)
              <input
                aria-label="Giá thuê loại phòng mới"
                type="number"
                min="1"
                step="1"
                value={newCatalogMonthlyRent}
                onChange={(event) =>
                  setNewCatalogMonthlyRent(event.target.value)
                }
                className="h-11 rounded-lg border border-slate-200 px-3 font-normal"
              />
            </label>
            <button
              type="submit"
              disabled={
                !catalogIsReady || actionInProgress || propertyId === null
              }
              className="self-end rounded-lg border border-[#087775] bg-white px-5 py-3 text-sm font-bold text-[#087775] disabled:opacity-50"
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
          <p role="status" className="px-5 py-8 text-sm text-slate-500">
            Đang tải cấu hình...
          </p>
        )}

        {!configurationState.loading &&
          !configurationState.error &&
          configurations.length === 0 && (
            <p role="status" className="px-5 py-8 text-sm text-slate-500">
              Chưa có loại phòng nào được cấu hình.
            </p>
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
                <label className="flex flex-col gap-1 text-xs font-semibold text-slate-500">
                  Giá thuê / tháng
                  <input
                    aria-label={`Giá thuê ${item.roomTypeName}`}
                    type="number"
                    min="1"
                    step="1"
                    value={priceDrafts[item.roomTypeId] ?? ""}
                    onChange={(event) =>
                      setPriceDrafts((current) => ({
                        ...current,
                        [item.roomTypeId]: event.target.value,
                      }))
                    }
                    className="h-10 rounded-lg border border-slate-200 px-3 text-sm font-normal text-slate-800"
                  />
                </label>
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
              </article>
            ))}
          </div>
        )}
      </section>
    </ManagementPage>
  )
}
