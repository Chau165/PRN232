import { useCallback, useEffect, useState } from "react"
import { useParams } from "react-router"
import { useAuth } from "@/auth/AuthContext"
import { ApiError } from "@/api/apiClient"
import ManagementDataTable from "@/components/management/ManagementDataTable"
import ManagementPage from "@/components/management/ManagementPage"
import RoomDetailDrawer from "@/components/management/RoomDetailDrawer"
import RoomFormModal from "@/components/management/RoomFormModal"
import StatusBadge from "@/components/customer/StatusBadge"
import { getProperties } from "@/services/propertyService"
import {
  createRoom,
  deleteRoom,
  getManagementRoom,
  getManagementRooms,
  getRoomTypes,
  updateRoom,
} from "@/services/roomService"
import type { PropertyDto } from "@/types/user"
import type {
  CreateRoomRequest,
  ManagementRoomDto,
  RoomTypeDto,
  UpdateRoomRequest,
} from "@/types/managementRoom"
import { parsePositiveRouteId } from "@/constants/routes"
import { formatVnd } from "@/utils/publicFormatting"

type RoomFilter = "ALL" | "Available" | "Reserved" | "Occupied" | "Maintenance"

const filters: Array<{ value: RoomFilter; label: string }> = [
  { value: "ALL", label: "Tất cả" },
  { value: "Available", label: "Trống" },
  { value: "Reserved", label: "Đã đặt cọc" },
  { value: "Occupied", label: "Đang thuê" },
  { value: "Maintenance", label: "Đang sửa chữa" },
]

function statusLabel(status: string): string {
  return filters.find((filter) => filter.value === status)?.label ?? status
}

function statusTone(status: string) {
  if (status === "Available") return "green"
  if (status === "Maintenance") return "rose"
  if (status === "Reserved") return "amber"
  return "teal"
}

function errorMessage(cause: unknown): string {
  if (!(cause instanceof ApiError)) {
    return cause instanceof Error
      ? cause.message
      : "Không thể tải dữ liệu phòng."
  }
  if (cause.status === 409) {
    return (
      cause.message ||
      "Thao tác bị từ chối vì phòng còn dữ liệu nghiệp vụ cần bảo toàn."
    )
  }
  if (cause.status === 403)
    return cause.message || "Bạn không có quyền thực hiện thao tác này."
  if (cause.status === 400) {
    const messages = Object.values(cause.validationErrors ?? {}).flat()
    return messages.length > 0 ? messages.join(" ") : cause.message
  }
  return cause.message
}

export default function ManagementRoomsPage() {
  const { accessToken, user } = useAuth()
  const { propertyId: propertyIdParam } = useParams<{ propertyId: string }>()
  const scopedPropertyId = parsePositiveRouteId(propertyIdParam)
  const [rooms, setRooms] = useState<ManagementRoomDto[]>([])
  const [properties, setProperties] = useState<PropertyDto[]>([])
  const [roomTypes, setRoomTypes] = useState<RoomTypeDto[]>([])
  const [filter, setFilter] = useState<RoomFilter>("ALL")
  const [propertyFilter, setPropertyFilter] = useState("")
  const [roomTypeFilter, setRoomTypeFilter] = useState("")
  const [search, setSearch] = useState("")
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState("")
  const [success, setSuccess] = useState("")
  const [reload, setReload] = useState(0)
  const [roomDialog, setRoomDialog] = useState<"create" | "edit" | null>(null)
  const [editingRoom, setEditingRoom] = useState<ManagementRoomDto | null>(null)
  const [detailRoom, setDetailRoom] = useState<ManagementRoomDto | null>(null)
  const [savingRoom, setSavingRoom] = useState(false)
  const [deletingRoomId, setDeletingRoomId] = useState<number | null>(null)
  const effectivePropertyFilter =
    scopedPropertyId === null ? propertyFilter : String(scopedPropertyId)

  const loadData = useCallback(
    async (signal: AbortSignal) => {
      if (!accessToken) return
      setLoading(true)
      setError("")
      try {
        const [roomRows, propertyRows, roomTypeRows] = await Promise.all([
          getManagementRooms(
            accessToken,
            {
              propertyId: effectivePropertyFilter
                ? Number(effectivePropertyFilter)
                : undefined,
              roomTypeId: roomTypeFilter ? Number(roomTypeFilter) : undefined,
              status: filter === "ALL" ? undefined : filter,
              search,
            },
            signal,
          ),
          getProperties(accessToken, signal),
          getRoomTypes(accessToken, signal),
        ])
        if (signal.aborted) return
        setRooms(roomRows)
        setProperties(propertyRows)
        setRoomTypes(roomTypeRows)
      } catch (cause) {
        if (!signal.aborted) setError(errorMessage(cause))
      } finally {
        if (!signal.aborted) setLoading(false)
      }
    },
    [accessToken, effectivePropertyFilter, filter, roomTypeFilter, search],
  )

  useEffect(() => {
    const controller = new AbortController()
    void loadData(controller.signal)
    return () => controller.abort()
  }, [loadData, reload])

  const visibleRooms = rooms
  const isAdmin = user?.role === "ADMIN"
  const scopedProperty = properties.find(
    (property) => property.propertyId === scopedPropertyId,
  )
  const formProperties =
    scopedPropertyId === null
      ? properties
      : properties.filter((property) => property.propertyId === scopedPropertyId)

  async function submitRoom(
    request: CreateRoomRequest | UpdateRoomRequest,
  ): Promise<boolean> {
    if (!accessToken) return false
    setSavingRoom(true)
    setError("")
    setSuccess("")
    try {
      if (roomDialog === "edit" && editingRoom) {
        await updateRoom(
          accessToken,
          editingRoom.roomId,
          request as UpdateRoomRequest,
        )
        setSuccess(`Đã cập nhật phòng ${editingRoom.roomCode}.`)
      } else {
        await createRoom(accessToken, request as CreateRoomRequest)
        setSuccess("Đã tạo phòng.")
      }
      setRoomDialog(null)
      setEditingRoom(null)
      setReload((value) => value + 1)
      return true
    } catch (cause) {
      setError(errorMessage(cause))
      return false
    } finally {
      setSavingRoom(false)
    }
  }

  async function openRoomDetail(room: ManagementRoomDto) {
    if (!accessToken) return
    setError("")
    try {
      setDetailRoom(await getManagementRoom(accessToken, room.roomId))
    } catch (cause) {
      setError(errorMessage(cause))
    }
  }

  async function openRoomEdit(room: ManagementRoomDto) {
    if (!accessToken) return
    setError("")
    try {
      setEditingRoom(await getManagementRoom(accessToken, room.roomId))
      setRoomDialog("edit")
    } catch (cause) {
      setError(errorMessage(cause))
    }
  }

  async function removeRoom(room: ManagementRoomDto) {
    if (!accessToken) return
    const confirmed = window.confirm(
      "Bạn có chắc chắn muốn xóa phòng này? Chỉ những phòng chưa có dữ liệu nghiệp vụ cần bảo toàn mới có thể xóa.",
    )
    if (!confirmed) return

    setDeletingRoomId(room.roomId)
    setError("")
    setSuccess("")
    try {
      await deleteRoom(accessToken, room.roomId)
      setSuccess(`Đã xóa phòng ${room.roomCode}.`)
      setReload((value) => value + 1)
    } catch (cause) {
      setError(errorMessage(cause))
    } finally {
      setDeletingRoomId(null)
    }
  }

  return (
    <ManagementPage
      title="Phòng"
      description={
        scopedProperty
          ? `Quản lý phòng của ${scopedProperty.propertyName}`
          : "Theo dõi và quản lý cấu hình phòng bằng dữ liệu Backend theo phạm vi được cấp quyền."
      }
    >
      {isAdmin && (
        <div className="mb-5 flex justify-end">
          <button
            type="button"
            onClick={() => {
              setEditingRoom(null)
              setRoomDialog("create")
            }}
            className="rounded-lg bg-[#087775] px-4 py-2.5 text-[13px] font-bold text-white"
          >
            + Thêm phòng
          </button>
        </div>
      )}
      {error && (
        <div
          role="alert"
          className="mb-4 rounded-lg bg-rose-50 px-4 py-3 text-sm text-rose-700"
        >
          <p>{error}</p>
          <button
            type="button"
            onClick={() => setReload((value) => value + 1)}
            className="mt-2 font-bold underline"
          >
            Tải lại
          </button>
        </div>
      )}
      {success && (
        <p
          role="status"
          className="mb-4 rounded-lg bg-emerald-50 px-4 py-3 text-sm text-emerald-800"
        >
          {success}
        </p>
      )}

      <div className="mb-5 flex flex-wrap gap-2">
        {filters.map((item) => (
          <button
            key={item.value}
            type="button"
            onClick={() => setFilter(item.value)}
            className={`rounded-full px-3 py-1.5 text-[12px] font-semibold ${
              filter === item.value
                ? "bg-[#087775] text-white"
                : "bg-slate-100 text-slate-500"
            }`}
          >
            {item.label}
          </button>
        ))}
      </div>

      <div
        className={`mb-5 grid gap-3 ${
          scopedPropertyId === null
            ? "md:grid-cols-[1.2fr_1fr_1fr_auto]"
            : "md:grid-cols-[1.2fr_1fr_auto]"
        }`}
      >
        <input
          type="search"
          aria-label="Tìm theo mã phòng"
          placeholder="Tìm mã phòng..."
          value={search}
          onChange={(event) => setSearch(event.target.value)}
          className="h-11 rounded-lg border border-slate-200 bg-white px-3 text-[13px]"
        />
        {scopedPropertyId === null && (
          <select
            aria-label="Lọc khu trọ"
            value={propertyFilter}
            onChange={(event) => setPropertyFilter(event.target.value)}
            className="h-11 rounded-lg border border-slate-200 bg-white px-3 text-[13px]"
          >
            <option value="">Tất cả khu trọ</option>
            {properties.map((property) => (
              <option key={property.propertyId} value={property.propertyId}>
                {property.propertyName}
              </option>
            ))}
          </select>
        )}
        <select
          aria-label="Lọc loại phòng"
          value={roomTypeFilter}
          onChange={(event) => setRoomTypeFilter(event.target.value)}
          className="h-11 rounded-lg border border-slate-200 bg-white px-3 text-[13px]"
        >
          <option value="">Tất cả loại phòng</option>
          {roomTypes.map((roomType) => (
            <option key={roomType.roomTypeId} value={roomType.roomTypeId}>
              {roomType.typeName}
            </option>
          ))}
        </select>
        <button
          type="button"
          onClick={() => setReload((value) => value + 1)}
          className="h-11 rounded-lg border border-slate-200 bg-white px-3 text-[13px] font-semibold text-[#087775]"
        >
          Làm mới
        </button>
      </div>

      <ManagementDataTable
        headers={[
          "Phòng",
          "Khu trọ",
          "Loại phòng",
          "Giá loại phòng",
          "Diện tích",
          "Trạng thái",
          "Thao tác",
        ]}
      >
        {loading ? (
          <p role="status" className="px-5 py-8 text-sm text-slate-500">
            Đang tải phòng...
          </p>
        ) : visibleRooms.length === 0 ? (
          <div
            role="status"
            className="px-5 py-8 text-center text-sm text-slate-500"
          >
            <p>
              {properties.length === 0
                ? "Vui lòng tạo khu trọ trước khi thêm phòng."
                : "Chưa có phòng nào."}
            </p>
            {isAdmin && properties.length > 0 && (
              <button
                type="button"
                onClick={() => setRoomDialog("create")}
                className="mt-4 rounded-lg bg-[#087775] px-4 py-2.5 text-[13px] font-bold text-white"
              >
                + Thêm phòng
              </button>
            )}
          </div>
        ) : (
          <div className="divide-y divide-slate-100">
            {visibleRooms.map((room) => (
              <div
                key={room.roomId}
                className="grid gap-3 px-5 py-5 lg:grid-cols-[.8fr_1.3fr_1fr_1fr_.8fr_1fr_1.4fr] lg:items-center"
              >
                <strong className="text-slate-800">
                  Phòng {room.roomCode}
                </strong>
                <span className="text-[13px] text-slate-600">
                  {room.propertyName}
                </span>
                <span className="text-[13px] text-slate-600">
                  {room.roomTypeName}
                </span>
                <span className="text-[13px] font-semibold text-[#087775]">
                  {formatVnd(room.roomTypePrice)}
                </span>
                <span className="text-[13px] text-slate-600">
                  {room.areaSqm} m²
                </span>
                <StatusBadge tone={statusTone(room.status)}>
                  {statusLabel(room.status).toUpperCase()}
                </StatusBadge>
                <div className="flex flex-wrap gap-2">
                  <button
                    type="button"
                    onClick={() => void openRoomDetail(room)}
                    className="rounded-lg bg-slate-100 px-3 py-2 text-[12px] font-bold text-slate-600"
                  >
                    Xem
                  </button>
                  {isAdmin && (
                    <>
                      <button
                        type="button"
                        onClick={() => void openRoomEdit(room)}
                        className="rounded-lg bg-teal-50 px-3 py-2 text-[12px] font-bold text-[#087775]"
                      >
                        Sửa
                      </button>
                      <button
                        type="button"
                        onClick={() => void removeRoom(room)}
                        disabled={deletingRoomId === room.roomId}
                        className="rounded-lg bg-rose-50 px-3 py-2 text-[12px] font-bold text-rose-700 disabled:opacity-50"
                      >
                        {deletingRoomId === room.roomId ? "Đang xóa..." : "Xóa"}
                      </button>
                    </>
                  )}
                </div>
              </div>
            ))}
          </div>
        )}
      </ManagementDataTable>

      {detailRoom && (
        <RoomDetailDrawer
          room={detailRoom}
          onClose={() => setDetailRoom(null)}
        />
      )}
      {roomDialog && (
        <RoomFormModal
          mode={roomDialog}
          initialRoom={editingRoom}
          properties={formProperties}
          roomTypes={roomTypes}
          lockedPropertyId={scopedPropertyId ?? undefined}
          saving={savingRoom}
          onClose={() => {
            if (!savingRoom) {
              setRoomDialog(null)
              setEditingRoom(null)
            }
          }}
          onSubmit={submitRoom}
        />
      )}
    </ManagementPage>
  )
}
