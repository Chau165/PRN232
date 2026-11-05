import { useCallback, useEffect, useState } from "react"
import { ApiError } from "@/api/apiClient"
import { useAuth } from "@/auth/AuthContext"
import ManagementDataTable from "@/components/management/ManagementDataTable"
import ManagementPage from "@/components/management/ManagementPage"
import EquipmentDetailModal from "@/components/management/EquipmentDetailModal"
import EquipmentFormModal from "@/components/management/EquipmentFormModal"
import StatusBadge from "@/components/customer/StatusBadge"
import {
  createEquipment,
  deleteEquipment,
  getEquipment,
  getEquipments,
  updateEquipment,
} from "@/services/equipmentService"
import { getProperties } from "@/services/propertyService"
import { getManagementRooms } from "@/services/roomService"
import type { EquipmentDto, EquipmentWriteRequest } from "@/types/equipment"
import type { ManagementRoomDto } from "@/types/managementRoom"
import type { PropertyDto } from "@/types/user"

function errorMessage(cause: unknown): string {
  if (!(cause instanceof ApiError)) {
    return cause instanceof Error
      ? cause.message
      : "Không thể tải dữ liệu thiết bị."
  }
  if (cause.status === 409) {
    return cause.message || "Không thể xóa thiết bị vì đã có lịch sử bảo trì."
  }
  if (cause.status === 403) {
    return cause.message || "Bạn không có quyền thực hiện thao tác này."
  }
  if (cause.status === 400) {
    const messages = Object.values(cause.validationErrors ?? {}).flat()
    return messages.length > 0 ? messages.join(" ") : cause.message
  }
  return cause.message
}

function statusLabel(status: string): string {
  return status === "Operational" ? "Đang hoạt động" : status
}

function formatDate(value: string): string {
  if (!value) return "-"
  const parsed = new Date(`${value}T00:00:00`)
  return Number.isNaN(parsed.getTime())
    ? value
    : parsed.toLocaleDateString("vi-VN")
}

function emptyEquipmentMessage(
  role: string | undefined,
  propertyCount: number,
  roomCount: number,
): string {
  if (role === "MANAGER" && propertyCount === 0) {
    return "Bạn chưa được phân công quản lý khu trọ."
  }
  if (roomCount === 0) return "Chưa có phòng để gán thiết bị."
  return "Chưa có thiết bị nào."
}

const equipmentPageSize = 50

export default function ManagementEquipmentPage() {
  const { accessToken, user } = useAuth()
  const [equipment, setEquipment] = useState<EquipmentDto[]>([])
  const [properties, setProperties] = useState<PropertyDto[]>([])
  const [rooms, setRooms] = useState<ManagementRoomDto[]>([])
  const [propertyFilter, setPropertyFilter] = useState("")
  const [roomFilter, setRoomFilter] = useState("")
  const [statusFilter, setStatusFilter] = useState("")
  const [search, setSearch] = useState("")
  const [loading, setLoading] = useState(true)
  const [page, setPage] = useState(1)
  const [hasMore, setHasMore] = useState(false)
  const [error, setError] = useState("")
  const [success, setSuccess] = useState("")
  const [reload, setReload] = useState(0)
  const [dialog, setDialog] = useState<"create" | "edit" | null>(null)
  const [editingEquipment, setEditingEquipment] = useState<EquipmentDto | null>(null)
  const [detailEquipment, setDetailEquipment] = useState<EquipmentDto | null>(null)
  const [saving, setSaving] = useState(false)
  const [deletingId, setDeletingId] = useState<number | null>(null)

  useEffect(() => {
    if (!accessToken) return
    const controller = new AbortController()
    setError("")
    void Promise.all([
      getProperties(accessToken, controller.signal),
      getManagementRooms(accessToken, {}, controller.signal),
    ])
      .then(([propertyRows, roomRows]) => {
        if (controller.signal.aborted) return
        setProperties(propertyRows)
        setRooms(roomRows)
      })
      .catch((cause: unknown) => {
        if (!controller.signal.aborted) setError(errorMessage(cause))
      })
    return () => controller.abort()
  }, [accessToken])

  const loadEquipment = useCallback(
    async (signal: AbortSignal) => {
      if (!accessToken) return
      setLoading(true)
      setError("")
      try {
        const equipmentRows = await getEquipments(
          accessToken,
          {
            propertyId: propertyFilter ? Number(propertyFilter) : undefined,
            roomId: roomFilter ? Number(roomFilter) : undefined,
            status: statusFilter || undefined,
            search,
            page,
            pageSize: equipmentPageSize,
          },
          signal,
        )
        if (signal.aborted) return
        setEquipment(equipmentRows)
        setHasMore(equipmentRows.length === equipmentPageSize)
      } catch (cause) {
        if (!signal.aborted) setError(errorMessage(cause))
      } finally {
        if (!signal.aborted) {
          setLoading(false)
        }
      }
    },
    [accessToken, page, propertyFilter, roomFilter, search, statusFilter],
  )

  useEffect(() => {
    const controller = new AbortController()
    void loadEquipment(controller.signal)
    return () => controller.abort()
  }, [loadEquipment, reload])

  async function openDetail(item: EquipmentDto) {
    if (!accessToken) return
    setError("")
    try {
      setDetailEquipment(await getEquipment(accessToken, item.equipmentId))
    } catch (cause) {
      setError(errorMessage(cause))
    }
  }

  async function openEdit(item: EquipmentDto) {
    if (!accessToken) return
    setError("")
    try {
      setEditingEquipment(await getEquipment(accessToken, item.equipmentId))
      setDialog("edit")
    } catch (cause) {
      setError(errorMessage(cause))
    }
  }

  async function submitEquipment(
    request: EquipmentWriteRequest & { roomId?: number },
  ): Promise<boolean> {
    if (!accessToken) return false
    setSaving(true)
    setError("")
    setSuccess("")
    try {
      if (dialog === "edit" && editingEquipment) {
        await updateEquipment(accessToken, editingEquipment.equipmentId, request)
        setSuccess(`Đã cập nhật thiết bị ${editingEquipment.name}.`)
      } else if (request.roomId !== undefined) {
        await createEquipment(accessToken, {
          roomId: request.roomId,
          name: request.name,
          installDate: request.installDate,
          warrantyExpiry: request.warrantyExpiry,
          supplier: request.supplier,
          status: request.status,
        })
        setSuccess("Đã tạo thiết bị.")
      } else {
        setError("Vui lòng chọn phòng cho thiết bị.")
        return false
      }
      setDialog(null)
      setEditingEquipment(null)
      setPage(1)
      setHasMore(false)
      setReload((value) => value + 1)
      return true
    } catch (cause) {
      setError(errorMessage(cause))
      return false
    } finally {
      setSaving(false)
    }
  }

  async function removeEquipment(item: EquipmentDto) {
    if (!accessToken) return
    if (!window.confirm(`Bạn có chắc chắn muốn xóa thiết bị ${item.name}?`)) return
    setDeletingId(item.equipmentId)
    setError("")
    setSuccess("")
    try {
      await deleteEquipment(accessToken, item.equipmentId)
      setSuccess(`Đã xóa thiết bị ${item.name}.`)
      setPage(1)
      setHasMore(false)
      setReload((value) => value + 1)
    } catch (cause) {
      setError(errorMessage(cause))
    } finally {
      setDeletingId(null)
    }
  }

  const formRooms = propertyFilter
    ? rooms.filter((room) => String(room.propertyId) === propertyFilter)
    : rooms

  function resetEquipmentPaging() {
    setPage(1)
    setHasMore(false)
    setEquipment([])
  }

  function changeEquipmentPage(nextPage: number) {
    if (nextPage < 1 || (nextPage > page && !hasMore)) return
    setEquipment([])
    setPage(nextPage)
  }

  function reloadEquipment() {
    resetEquipmentPaging()
    setReload((value) => value + 1)
  }

  return (
    <ManagementPage
      title="Thiết bị"
      description="Theo dõi thiết bị theo phòng, thời hạn bảo hành và lịch sử sửa chữa bằng dữ liệu Backend."
    >
      <div className="mb-5 flex justify-end">
        <button
          type="button"
          onClick={() => {
            setEditingEquipment(null)
            setDialog("create")
          }}
          className="rounded-lg bg-[#087775] px-4 py-2.5 text-[13px] font-bold text-white"
        >
          + Thêm thiết bị
        </button>
      </div>

      {error && (
        <div role="alert" className="mb-4 rounded-lg bg-rose-50 px-4 py-3 text-sm text-rose-700">
          <p>{error}</p>
          <button
            type="button"
            onClick={reloadEquipment}
            className="mt-2 font-bold underline"
          >
            Tải lại
          </button>
        </div>
      )}
      {success && (
        <p role="status" className="mb-4 rounded-lg bg-emerald-50 px-4 py-3 text-sm text-emerald-800">
          {success}
        </p>
      )}

      <div className="mb-5 grid gap-3 md:grid-cols-[1.2fr_1fr_1fr_1fr_auto]">
        <input
          type="search"
          aria-label="Tìm thiết bị"
          placeholder="Tìm tên hoặc nhà cung cấp..."
          value={search}
          onChange={(event) => {
            setSearch(event.target.value)
            resetEquipmentPaging()
          }}
          className="h-11 rounded-lg border border-slate-200 bg-white px-3 text-[13px]"
        />
        <select
          aria-label="Lọc khu trọ"
          value={propertyFilter}
          onChange={(event) => {
            setPropertyFilter(event.target.value)
            setRoomFilter("")
            resetEquipmentPaging()
          }}
          className="h-11 rounded-lg border border-slate-200 bg-white px-3 text-[13px]"
        >
          <option value="">Tất cả khu trọ</option>
          {properties.map((property) => (
            <option key={property.propertyId} value={property.propertyId}>
              Khu trọ: {property.propertyName}
            </option>
          ))}
        </select>
        <select
          aria-label="Lọc phòng"
          value={roomFilter}
          onChange={(event) => {
            setRoomFilter(event.target.value)
            resetEquipmentPaging()
          }}
          className="h-11 rounded-lg border border-slate-200 bg-white px-3 text-[13px]"
        >
          <option value="">Tất cả phòng</option>
          {rooms
            .filter((room) => !propertyFilter || String(room.propertyId) === propertyFilter)
            .map((room) => (
              <option key={room.roomId} value={room.roomId}>
                Phòng {room.roomCode}
              </option>
            ))}
        </select>
        <select
          aria-label="Lọc trạng thái"
          value={statusFilter}
          onChange={(event) => {
            setStatusFilter(event.target.value)
            resetEquipmentPaging()
          }}
          className="h-11 rounded-lg border border-slate-200 bg-white px-3 text-[13px]"
        >
          <option value="">Tất cả trạng thái</option>
          <option value="Operational">Đang hoạt động</option>
        </select>
        <button
          type="button"
          onClick={reloadEquipment}
          className="h-11 rounded-lg border border-slate-200 bg-white px-3 text-[13px] font-semibold text-[#087775]"
        >
          Làm mới
        </button>
      </div>

      <ManagementDataTable
        headers={[
          "Thiết bị",
          "Phòng",
          "Khu trọ",
          "Lắp đặt",
          "Bảo hành đến",
          "Nhà cung cấp",
          "Trạng thái",
          "Thao tác",
        ]}
      >
        {loading && equipment.length === 0 ? (
          <p role="status" className="px-5 py-8 text-sm text-slate-500">
            Đang tải thiết bị...
          </p>
        ) : equipment.length === 0 ? (
          <div role="status" className="px-5 py-8 text-center text-sm text-slate-500">
            <p>
              {emptyEquipmentMessage(user?.role, properties.length, rooms.length)}
            </p>
            <button
              type="button"
              onClick={() => setDialog("create")}
              className="mt-4 rounded-lg bg-[#087775] px-4 py-2.5 text-[13px] font-bold text-white"
            >
              + Thêm thiết bị
            </button>
          </div>
        ) : (
          <>
            <div className="divide-y divide-slate-100">
              {equipment.map((item) => (
                <div
                  key={item.equipmentId}
                  className="grid gap-3 px-5 py-5 lg:grid-cols-[repeat(8,minmax(0,1fr))] lg:items-center"
                >
                  <strong className="text-[13px] text-slate-800">{item.name}</strong>
                  <span className="text-[13px] text-slate-600">
                    <small className="block text-slate-400">Phòng</small>
                    <strong>{item.roomCode || item.roomId}</strong>
                  </span>
                  <span className="text-[13px] text-slate-600">
                    {item.propertyName || "-"}
                  </span>
                  <span className="text-[13px] text-slate-600">
                    {formatDate(item.installDate)}
                  </span>
                  <span className="text-[13px] text-slate-600">
                    {formatDate(item.warrantyExpiry)}
                  </span>
                  <span className="text-[13px] text-slate-600">{item.supplier}</span>
                  <StatusBadge tone="green">
                    {statusLabel(item.status).toUpperCase()}
                  </StatusBadge>
                  <div className="flex flex-wrap gap-2">
                    <button
                      type="button"
                      aria-label={`Xem ${item.name}`}
                      onClick={() => void openDetail(item)}
                      className="rounded-lg bg-slate-100 px-3 py-2 text-[12px] font-bold text-slate-600"
                    >
                      Xem
                    </button>
                    <button
                      type="button"
                      aria-label={`Sửa ${item.name}`}
                      onClick={() => void openEdit(item)}
                      className="rounded-lg bg-teal-50 px-3 py-2 text-[12px] font-bold text-[#087775]"
                    >
                      Sửa
                    </button>
                    <button
                      type="button"
                      aria-label={`Xóa ${item.name}`}
                      onClick={() => void removeEquipment(item)}
                      disabled={deletingId === item.equipmentId}
                      className="rounded-lg bg-rose-50 px-3 py-2 text-[12px] font-bold text-rose-700 disabled:opacity-50"
                    >
                      {deletingId === item.equipmentId ? "Đang xóa..." : "Xóa"}
                    </button>
                  </div>
                </div>
              ))}
            </div>
            {(page > 1 || hasMore) && (
              <nav
                aria-label="Phân trang thiết bị"
                className="flex items-center justify-center gap-4 border-t border-slate-100 px-5 py-4"
              >
                <button
                  type="button"
                  aria-label="Trang trước"
                  onClick={() => changeEquipmentPage(page - 1)}
                  disabled={page <= 1 || loading}
                  className="rounded-lg border border-slate-200 bg-white px-4 py-2 text-[13px] font-semibold text-[#087775] disabled:opacity-50"
                >
                  Trang trước
                </button>
                <span
                  aria-live="polite"
                  className="text-[13px] text-slate-500"
                >
                  Trang {page}
                </span>
                <button
                  type="button"
                  aria-label="Trang sau"
                  onClick={() => changeEquipmentPage(page + 1)}
                  disabled={!hasMore || loading}
                  className="rounded-lg border border-slate-200 bg-white px-4 py-2 text-[13px] font-semibold text-[#087775] disabled:opacity-50"
                >
                  Trang sau
                </button>
              </nav>
            )}
          </>
        )}
      </ManagementDataTable>

      {detailEquipment && (
        <EquipmentDetailModal
          equipment={detailEquipment}
          onClose={() => setDetailEquipment(null)}
        />
      )}
      {dialog && (
        <EquipmentFormModal
          mode={dialog}
          initialEquipment={editingEquipment}
          properties={properties}
          rooms={formRooms}
          saving={saving}
          onClose={() => {
            if (!saving) {
              setDialog(null)
              setEditingEquipment(null)
            }
          }}
          onSubmit={submitEquipment}
        />
      )}
    </ManagementPage>
  )
}
