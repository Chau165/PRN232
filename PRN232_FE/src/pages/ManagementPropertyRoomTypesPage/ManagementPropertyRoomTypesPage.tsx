import { useCallback, useEffect, useMemo, useState, type FormEvent } from "react"
import { Link, useParams } from "react-router"
import { ApiError } from "@/api/apiClient"
import { useAuth } from "@/auth/AuthContext"
import ManagementPage from "@/components/management/ManagementPage"
import { managementPropertyRoomsPath, parsePositiveRouteId, routes } from "@/constants/routes"
import { getProperties } from "@/services/propertyService"
import { getRoomTypes } from "@/services/roomService"
import {
  createPropertyRoomType,
  getPropertyRoomTypes,
  setPropertyRoomTypeActive,
  updatePropertyRoomType,
} from "@/services/propertyRoomTypeService"
import type { PropertyRoomTypeDto, RoomTypeCatalogItem } from "@/types/propertyRoomType"
import type { PropertyDto } from "@/types/user"
import { formatVnd } from "@/utils/publicFormatting"

function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 409) return error.message || "Loại phòng đã được cấu hình tại khu trọ này."
    if (error.status === 403) return error.message || "Bạn không có quyền cấu hình giá khu trọ."
    if (error.status === 400) {
      const messages = Object.values(error.validationErrors ?? {}).flat()
      return messages.length ? messages.join(" ") : error.message
    }
    return error.message
  }
  return error instanceof Error ? error.message : "Không thể tải cấu hình loại phòng."
}

export default function ManagementPropertyRoomTypesPage() {
  const { accessToken } = useAuth()
  const { propertyId: propertyIdParam } = useParams<{ propertyId: string }>()
  const propertyId = parsePositiveRouteId(propertyIdParam)
  const [property, setProperty] = useState<PropertyDto | null>(null)
  const [catalog, setCatalog] = useState<RoomTypeCatalogItem[]>([])
  const [configurations, setConfigurations] = useState<PropertyRoomTypeDto[]>([])
  const [priceDrafts, setPriceDrafts] = useState<Record<number, string>>({})
  const [newRoomTypeId, setNewRoomTypeId] = useState("")
  const [newMonthlyRent, setNewMonthlyRent] = useState("")
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState("")
  const [success, setSuccess] = useState("")
  const [reload, setReload] = useState(0)

  const loadData = useCallback(async (signal: AbortSignal) => {
    if (!accessToken || propertyId === null) return
    setLoading(true)
    setError("")
    try {
      const [propertyRow, roomTypeRows, configurationRows] = await Promise.all([
        getProperties(accessToken, signal).then((properties) =>
          properties.find((item) => item.propertyId === propertyId) ?? null,
        ),
        getRoomTypes(accessToken, signal),
        getPropertyRoomTypes(accessToken, propertyId, signal),
      ])
      if (signal.aborted) return
      setProperty(propertyRow)
      setCatalog(roomTypeRows)
      setConfigurations(configurationRows)
      setPriceDrafts(Object.fromEntries(configurationRows.map((item) => [item.roomTypeId, String(item.monthlyRent)])))
    } catch (cause) {
      if (!signal.aborted) setError(errorMessage(cause))
    } finally {
      if (!signal.aborted) setLoading(false)
    }
  }, [accessToken, propertyId])

  useEffect(() => {
    const controller = new AbortController()
    void loadData(controller.signal)
    return () => controller.abort()
  }, [loadData, reload])

  const configuredRoomTypeIds = useMemo(
    () => new Set(configurations.map((item) => item.roomTypeId)),
    [configurations],
  )

  async function addConfiguration(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!accessToken || propertyId === null || saving) return
    const roomTypeId = Number(newRoomTypeId)
    const monthlyRent = Number(newMonthlyRent)
    if (!Number.isSafeInteger(roomTypeId) || roomTypeId < 1 || !Number.isFinite(monthlyRent) || monthlyRent <= 0) {
      setError("Chọn loại phòng và nhập giá thuê lớn hơn 0.")
      return
    }
    setSaving(true)
    setError("")
    setSuccess("")
    try {
      await createPropertyRoomType(accessToken, propertyId, { roomTypeId, monthlyRent })
      setNewRoomTypeId("")
      setNewMonthlyRent("")
      setSuccess("Đã thêm loại phòng cho khu trọ.")
      setReload((value) => value + 1)
    } catch (cause) {
      setError(errorMessage(cause))
    } finally {
      setSaving(false)
    }
  }

  async function savePrice(item: PropertyRoomTypeDto) {
    if (!accessToken || saving) return
    const monthlyRent = Number(priceDrafts[item.roomTypeId])
    if (!Number.isFinite(monthlyRent) || monthlyRent <= 0) {
      setError("Giá thuê phải lớn hơn 0.")
      return
    }
    setSaving(true)
    setError("")
    setSuccess("")
    try {
      await updatePropertyRoomType(accessToken, item.propertyId, item.roomTypeId, { monthlyRent })
      setSuccess(`Đã cập nhật giá ${item.roomTypeName}.`)
      setReload((value) => value + 1)
    } catch (cause) {
      setError(errorMessage(cause))
    } finally {
      setSaving(false)
    }
  }

  async function toggleStatus(item: PropertyRoomTypeDto) {
    if (!accessToken || saving) return
    setSaving(true)
    setError("")
    setSuccess("")
    try {
      await setPropertyRoomTypeActive(accessToken, item.propertyId, item.roomTypeId, !item.isActive)
      setSuccess(item.isActive ? "Đã ngừng dùng cấu hình cho giao dịch mới." : "Đã kích hoạt cấu hình.")
      setReload((value) => value + 1)
    } catch (cause) {
      setError(errorMessage(cause))
    } finally {
      setSaving(false)
    }
  }

  return (
    <ManagementPage
      title="Loại phòng và giá thuê"
      description={property ? `Cấu hình riêng cho ${property.propertyName}. Giá chung trong danh mục không được dùng làm giá thuê.` : "Cấu hình giá thuê riêng theo khu trọ."}
    >
      <Link to={routes.managementProperties} className="mb-5 inline-block text-sm font-semibold text-[#087775] hover:underline">
        ← Danh sách khu trọ
      </Link>
      {error && <p role="alert" className="mb-4 rounded-lg bg-rose-50 px-4 py-3 text-sm text-rose-700">{error}</p>}
      {success && <p role="status" className="mb-4 rounded-lg bg-emerald-50 px-4 py-3 text-sm text-emerald-800">{success}</p>}

      <section className="rounded-2xl bg-white p-5 shadow-sm sm:p-6">
        <h2 className="font-display text-lg font-bold text-slate-900">Thêm loại phòng</h2>
        <form onSubmit={addConfiguration} className="mt-4 grid gap-3 md:grid-cols-[minmax(0,1fr)_220px_auto]">
          <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
            Danh mục loại phòng
            <select aria-label="Danh mục loại phòng" value={newRoomTypeId} onChange={(event) => setNewRoomTypeId(event.target.value)} className="h-11 rounded-lg border border-slate-200 px-3 font-normal">
              <option value="">Chọn loại phòng</option>
              {catalog.filter((item) => !configuredRoomTypeIds.has(item.roomTypeId)).map((item) => (
                <option key={item.roomTypeId} value={item.roomTypeId}>{item.typeName}</option>
              ))}
            </select>
          </label>
          <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
            Giá thuê / tháng (VND)
            <input aria-label="Giá thuê mới" type="number" min="1" step="1" value={newMonthlyRent} onChange={(event) => setNewMonthlyRent(event.target.value)} className="h-11 rounded-lg border border-slate-200 px-3 font-normal" />
          </label>
          <button type="submit" disabled={loading || saving || propertyId === null} className="self-end rounded-lg bg-[#087775] px-5 py-3 text-sm font-bold text-white disabled:opacity-50">
            {saving ? "Đang lưu..." : "Thêm cấu hình"}
          </button>
        </form>
      </section>

      <section className="mt-6 rounded-2xl bg-white shadow-sm">
        <h2 className="border-b border-slate-100 px-5 py-4 font-display text-lg font-bold text-slate-900">Cấu hình hiện có</h2>
        {loading ? (
          <p role="status" className="px-5 py-8 text-sm text-slate-500">Đang tải cấu hình...</p>
        ) : configurations.length === 0 ? (
          <p role="status" className="px-5 py-8 text-sm text-slate-500">Chưa có loại phòng nào được cấu hình.</p>
        ) : (
          <div className="divide-y divide-slate-100">
            {configurations.map((item) => (
              <article key={item.roomTypeId} className="grid gap-4 px-5 py-5 md:grid-cols-[minmax(140px,1fr)_220px_160px_auto] md:items-center">
                <div>
                  <h3 className="font-semibold text-slate-800">{item.roomTypeName}</h3>
                  <p className="mt-1 text-xs text-slate-500">{item.totalRoomCount} phòng · {item.availableRoomCount} hiện có thể thuê</p>
                </div>
                <label className="flex flex-col gap-1 text-xs font-semibold text-slate-500">
                  Giá thuê / tháng
                  <input aria-label={`Giá thuê ${item.roomTypeName}`} type="number" min="1" step="1" value={priceDrafts[item.roomTypeId] ?? ""} onChange={(event) => setPriceDrafts((current) => ({ ...current, [item.roomTypeId]: event.target.value }))} className="h-10 rounded-lg border border-slate-200 px-3 text-sm font-normal text-slate-800" />
                </label>
                <p className="text-sm font-bold text-[#087775]">{formatVnd(item.monthlyRent)} / tháng</p>
                <div className="flex flex-wrap gap-2">
                  <button type="button" disabled={saving} onClick={() => void savePrice(item)} className="rounded-lg border border-[#087775] px-3 py-2 text-xs font-bold text-[#087775] disabled:opacity-50">Lưu giá</button>
                  <button type="button" disabled={saving} onClick={() => void toggleStatus(item)} className={`rounded-lg px-3 py-2 text-xs font-bold disabled:opacity-50 ${item.isActive ? "bg-amber-50 text-amber-800" : "bg-emerald-50 text-emerald-800"}`}>
                    {item.isActive ? "Ngừng dùng" : "Kích hoạt"}
                  </button>
                  <Link to={`${managementPropertyRoomsPath(item.propertyId)}?roomTypeId=${item.roomTypeId}`} className="rounded-lg bg-slate-100 px-3 py-2 text-xs font-bold text-slate-700">Xem phòng</Link>
                </div>
                {!item.isActive && <p className="text-xs font-semibold text-amber-700 md:col-span-4">Đang ngừng dùng cho giao dịch mới.</p>}
              </article>
            ))}
          </div>
        )}
      </section>
    </ManagementPage>
  )
}
