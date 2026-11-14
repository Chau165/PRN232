import { useEffect, useRef, useState } from "react"
import { Link, useParams } from "react-router"
import { ApiError } from "@/api/apiClient"
import { useAuth } from "@/auth/AuthContext"
import EquipmentDetailModal from "@/components/management/EquipmentDetailModal"
import EquipmentFormModal from "@/components/management/EquipmentFormModal"
import ManagementDataTable from "@/components/management/ManagementDataTable"
import ManagementPage from "@/components/management/ManagementPage"
import Toast from "@/components/customer/Toast"
import StatusBadge from "@/components/customer/StatusBadge"
import {
  managementPropertyRoomsPath,
  parsePositiveRouteId,
  routes,
} from "@/constants/routes"
import {
  createEquipment,
  deleteEquipment,
  getEquipment,
  getEquipments,
  updateEquipment,
} from "@/services/equipmentService"
import { getManagementRoom } from "@/services/roomService"
import type { EquipmentDto, EquipmentWriteRequest } from "@/types/equipment"
import type { ManagementRoomDto } from "@/types/managementRoom"

const equipmentPageSize = 50
const maintenanceHistoryMessage =
  "Không thể xóa thiết bị vì đã có lịch sử bảo trì liên quan."

function errorMessage(cause: unknown, fallback: string): string {
  if (!(cause instanceof ApiError)) {
    return cause instanceof Error ? cause.message : fallback
  }
  if (cause.status === 409) return maintenanceHistoryMessage
  if (cause.status === 403) {
    return "Bạn không có quyền thực hiện thao tác này."
  }
  if (cause.status === 404) return fallback
  if (cause.status === 400) {
    const messages = Object.values(cause.validationErrors ?? {}).flat()
    return messages.length > 0 ? messages.join(" ") : cause.message
  }
  return cause.message || fallback
}

function formatDate(value: string): string {
  if (!value) return "-"
  const parsed = new Date(value + "T00:00:00")
  return Number.isNaN(parsed.getTime())
    ? value
    : parsed.toLocaleDateString("vi-VN")
}

function statusLabel(status: string): string {
  return status === "Operational" ? "Đang hoạt động" : status
}

function roomStatusLabel(status: string): string {
  const labels: Record<string, string> = {
    Available: "Trống",
    Reserved: "Đã đặt cọc",
    Occupied: "Đang thuê",
    Maintenance: "Đang sửa chữa",
  }
  return labels[status] ?? status
}

export default function ManagementRoomEquipmentPage() {
  const { accessToken } = useAuth()
  const { propertyId: propertyIdParam, roomId: roomIdParam } = useParams<{
    propertyId: string
    roomId: string
  }>()
  const propertyId = parsePositiveRouteId(propertyIdParam)
  const roomId = parsePositiveRouteId(roomIdParam)
  const routeIsValid = propertyId !== null && roomId !== null
  const roomListPath =
    propertyId === null
      ? routes.managementProperties
      : managementPropertyRoomsPath(propertyId)

  const [roomContext, setRoomContext] = useState<ManagementRoomDto | null>(null)
  const [roomLoading, setRoomLoading] = useState(true)
  const [contextError, setContextError] = useState("")
  const [equipment, setEquipment] = useState<EquipmentDto[]>([])
  const [statusFilter, setStatusFilter] = useState("")
  const [search, setSearch] = useState("")
  const [loading, setLoading] = useState(false)
  const [page, setPage] = useState(1)
  const [hasMore, setHasMore] = useState(false)
  const [error, setError] = useState("")
  const [toast, setToast] = useState("")
  const [reload, setReload] = useState(0)
  const [dialog, setDialog] = useState<"create" | "edit" | null>(null)
  const [editingEquipment, setEditingEquipment] = useState<EquipmentDto | null>(
    null,
  )
  const [detailEquipment, setDetailEquipment] = useState<EquipmentDto | null>(
    null,
  )
  const [saving, setSaving] = useState(false)
  const [deletingId, setDeletingId] = useState<number | null>(null)
  const detailController = useRef<AbortController | null>(null)

  const currentRoom =
    roomContext?.roomId === roomId && roomContext.propertyId === propertyId
      ? roomContext
      : null

  useEffect(() => {
    detailController.current?.abort()
    setRoomContext(null)
    setEquipment([])
    setHasMore(false)
    setPage(1)
    setSearch("")
    setStatusFilter("")
    setContextError("")
    setError("")
    setToast("")
    setDialog(null)
    setEditingEquipment(null)
    setDetailEquipment(null)

    if (
      !routeIsValid ||
      !accessToken ||
      propertyId === null ||
      roomId === null
    ) {
      setRoomLoading(false)
      return
    }

    const controller = new AbortController()
    setRoomLoading(true)
    void getManagementRoom(accessToken, roomId, controller.signal)
      .then((room) => {
        if (controller.signal.aborted) return
        if (room.roomId !== roomId || room.propertyId !== propertyId) {
          setContextError("Phòng không thuộc khu trọ trong đường dẫn.")
          return
        }
        setRoomContext(room)
      })
      .catch((cause: unknown) => {
        if (!controller.signal.aborted) {
          setContextError(
            errorMessage(
              cause,
              "Không tìm thấy phòng hoặc bạn không có quyền truy cập.",
            ),
          )
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setRoomLoading(false)
      })

    return () => {
      controller.abort()
      detailController.current?.abort()
    }
  }, [accessToken, propertyId, roomId, routeIsValid])

  useEffect(() => {
    if (!accessToken || !currentRoom) {
      setEquipment([])
      setHasMore(false)
      setLoading(false)
      return
    }

    const controller = new AbortController()
    setLoading(true)
    setError("")
    void getEquipments(
      accessToken,
      {
        roomId: currentRoom.roomId,
        status: statusFilter || undefined,
        search,
        page,
        pageSize: equipmentPageSize,
      },
      controller.signal,
    )
      .then((rows) => {
        if (controller.signal.aborted) return
        const scopedRows = rows.filter(
          (item) => item.roomId === currentRoom.roomId,
        )
        if (scopedRows.length !== rows.length) {
          setError("Dữ liệu thiết bị trả về không thuộc phòng đang mở.")
        }
        setEquipment(scopedRows)
        setHasMore(rows.length === equipmentPageSize)
      })
      .catch((cause: unknown) => {
        if (!controller.signal.aborted) {
          setError(errorMessage(cause, "Không thể tải dữ liệu thiết bị."))
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })

    return () => controller.abort()
  }, [accessToken, currentRoom?.roomId, page, reload, search, statusFilter])

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
    setEquipment([])
    setHasMore(false)
    setReload((value) => value + 1)
  }

  async function loadEquipmentAction(
    item: EquipmentDto,
    action: "detail" | "edit",
  ) {
    if (!accessToken || !currentRoom) return
    if (item.roomId !== currentRoom.roomId) {
      setError("Thiết bị này không thuộc phòng đang mở.")
      return
    }

    detailController.current?.abort()
    const controller = new AbortController()
    detailController.current = controller
    setError("")
    setDetailEquipment(null)

    try {
      const loaded = await getEquipment(
        accessToken,
        item.equipmentId,
        controller.signal,
      )
      if (controller.signal.aborted) return
      if (loaded.roomId !== currentRoom.roomId) {
        setError("Thiết bị này không thuộc phòng đang mở.")
        return
      }
      if (action === "detail") {
        setDetailEquipment(loaded)
      } else {
        setEditingEquipment(loaded)
        setDialog("edit")
      }
    } catch (cause) {
      if (!controller.signal.aborted) {
        setError(errorMessage(cause, "Không thể tải chi tiết thiết bị."))
      }
    }
  }

  async function submitEquipment(
    request: Omit<EquipmentWriteRequest, "roomId">,
  ): Promise<boolean> {
    if (!accessToken || !currentRoom) return false
    setSaving(true)
    setError("")
    try {
      if (dialog === "edit" && editingEquipment) {
        if (editingEquipment.roomId !== currentRoom.roomId) {
          setError("Thiết bị này không thuộc phòng đang mở.")
          return false
        }
        await updateEquipment(
          accessToken,
          editingEquipment.equipmentId,
          request,
        )
        setToast("Đã cập nhật thiết bị " + editingEquipment.name + ".")
      } else {
        await createEquipment(accessToken, {
          ...request,
          roomId: currentRoom.roomId,
        })
        setToast("Đã tạo thiết bị cho phòng " + currentRoom.roomCode + ".")
        setPage(1)
        setHasMore(false)
      }
      setDialog(null)
      setEditingEquipment(null)
      setReload((value) => value + 1)
      return true
    } catch (cause) {
      setError(errorMessage(cause, "Không thể lưu thiết bị."))
      return false
    } finally {
      setSaving(false)
    }
  }

  async function removeEquipment(item: EquipmentDto) {
    if (!accessToken || !currentRoom) return
    if (item.roomId !== currentRoom.roomId) {
      setError("Thiết bị này không thuộc phòng đang mở.")
      return
    }
    if (
      !window.confirm("Bạn có chắc chắn muốn xóa thiết bị " + item.name + "?")
    ) {
      return
    }

    setDeletingId(item.equipmentId)
    setError("")
    try {
      await deleteEquipment(accessToken, item.equipmentId)
      setToast("Đã xóa thiết bị " + item.name + ".")
      if (page > 1 && equipment.length === 1) {
        setPage(page - 1)
        setHasMore(false)
      } else {
        reloadEquipment()
      }
    } catch (cause) {
      setError(errorMessage(cause, "Không thể xóa thiết bị."))
    } finally {
      setDeletingId(null)
    }
  }

  const title = currentRoom
    ? "Thiết bị — Phòng " + currentRoom.roomCode
    : "Thiết bị"
  const description = currentRoom
    ? "Quản lý danh sách thiết bị được lắp đặt trong phòng " +
      currentRoom.roomCode +
      " thuộc khu trọ " +
      currentRoom.propertyName +
      "."
    : "Quản lý thiết bị trong từng phòng."

  return (
    <ManagementPage title={title} description={description}>
      {currentRoom && (
        <>
          <nav
            aria-label="Đường dẫn"
            className="mb-3 flex flex-wrap items-center gap-2 text-sm text-slate-500"
          >
            <Link
              to={routes.managementProperties}
              className="hover:text-[#087775]"
            >
              {currentRoom.propertyName}
            </Link>
            <span aria-hidden="true">›</span>
            <Link to={roomListPath} className="hover:text-[#087775]">
              Phòng {currentRoom.roomCode}
            </Link>
            <span aria-hidden="true">›</span>
            <span aria-current="page" className="font-semibold text-slate-800">
              Thiết bị
            </span>
          </nav>
          <div className="mb-5 flex flex-wrap items-center justify-between gap-3">
            <Link
              to={roomListPath}
              className="text-sm font-semibold text-[#087775] hover:underline"
            >
              ← Quay lại danh sách phòng
            </Link>
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
          <section
            aria-label="Thông tin phòng"
            className="mb-5 grid gap-3 rounded-xl border border-slate-100 bg-white p-4 text-sm sm:grid-cols-3"
          >
            <p>
              <span className="text-slate-500">Khu trọ: </span>
              <strong className="text-slate-800">
                {currentRoom.propertyName}
              </strong>
            </p>
            <p>
              <span className="text-slate-500">Loại phòng: </span>
              <strong className="text-slate-800">
                {currentRoom.roomTypeName}
              </strong>
            </p>
            <p>
              <span className="text-slate-500">Trạng thái phòng: </span>
              <strong className="text-slate-800">
                {roomStatusLabel(currentRoom.status)}
              </strong>
            </p>
          </section>
        </>
      )}

      {!routeIsValid && (
        <div
          role="alert"
          className="mb-4 rounded-lg bg-rose-50 px-4 py-3 text-sm text-rose-700"
        >
          Đường dẫn khu trọ hoặc phòng không hợp lệ.
          <Link to={roomListPath} className="ml-2 font-bold underline">
            Quay lại
          </Link>
        </div>
      )}
      {routeIsValid && roomLoading && (
        <p role="status" className="mb-4 text-sm text-slate-500">
          Đang kiểm tra phòng...
        </p>
      )}
      {routeIsValid && !roomLoading && !currentRoom && contextError && (
        <div
          role="alert"
          className="mb-4 rounded-lg bg-rose-50 px-4 py-3 text-sm text-rose-700"
        >
          <p>{contextError}</p>
          <Link
            to={roomListPath}
            className="mt-2 inline-block font-bold underline"
          >
            Quay lại danh sách phòng
          </Link>
        </div>
      )}

      {currentRoom && (
        <>
          {error && (
            <div
              role="alert"
              className="mb-4 rounded-lg bg-rose-50 px-4 py-3 text-sm text-rose-700"
            >
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

          <div className="mb-5 grid gap-3 sm:grid-cols-[1fr_1fr_auto]">
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
              "Tên thiết bị",
              "Ngày lắp đặt",
              "Hạn bảo hành",
              "Nhà cung cấp",
              "Trạng thái",
              "Thao tác",
            ]}
          >
            {loading && equipment.length === 0 ? (
              <p role="status" className="px-5 py-8 text-sm text-slate-500">
                Đang tải thiết bị...
              </p>
            ) : error && equipment.length === 0 ? (
              <p
                role="status"
                className="px-5 py-8 text-center text-sm text-slate-500"
              >
                Không thể tải danh sách thiết bị.
              </p>
            ) : equipment.length === 0 ? (
              <div
                role="status"
                className="px-5 py-8 text-center text-sm text-slate-500"
              >
                {search || statusFilter
                  ? "Không có thiết bị khớp bộ lọc."
                  : "Phòng này chưa có thiết bị nào."}
              </div>
            ) : (
              <>
                <div className="divide-y divide-slate-100">
                  {equipment.map((item) => (
                    <div
                      key={item.equipmentId}
                      className="grid gap-3 px-5 py-5 lg:grid-cols-[1.3fr_1fr_1fr_1.2fr_.9fr_1.5fr] lg:items-center"
                    >
                      <strong className="text-[13px] text-slate-800">
                        {item.name}
                      </strong>
                      <span className="text-[13px] text-slate-600">
                        {formatDate(item.installDate)}
                      </span>
                      <span className="text-[13px] text-slate-600">
                        {formatDate(item.warrantyExpiry)}
                      </span>
                      <span className="text-[13px] text-slate-600">
                        {item.supplier}
                      </span>
                      <StatusBadge tone="green">
                        {statusLabel(item.status).toUpperCase()}
                      </StatusBadge>
                      <div className="flex flex-wrap gap-2">
                        <button
                          type="button"
                          aria-label={"Xem " + item.name}
                          onClick={() =>
                            void loadEquipmentAction(item, "detail")
                          }
                          className="rounded-lg bg-slate-100 px-3 py-2 text-[12px] font-bold text-slate-600"
                        >
                          Xem
                        </button>
                        <button
                          type="button"
                          aria-label={"Sửa " + item.name}
                          onClick={() => void loadEquipmentAction(item, "edit")}
                          className="rounded-lg bg-teal-50 px-3 py-2 text-[12px] font-bold text-[#087775]"
                        >
                          Sửa
                        </button>
                        <button
                          type="button"
                          aria-label={"Xóa " + item.name}
                          onClick={() => void removeEquipment(item)}
                          disabled={deletingId === item.equipmentId}
                          className="rounded-lg bg-rose-50 px-3 py-2 text-[12px] font-bold text-rose-700 disabled:opacity-50"
                        >
                          {deletingId === item.equipmentId
                            ? "Đang xóa..."
                            : "Xóa"}
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
              room={currentRoom}
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
        </>
      )}
      {toast && <Toast message={toast} onClose={() => setToast("")} />}
    </ManagementPage>
  )
}
