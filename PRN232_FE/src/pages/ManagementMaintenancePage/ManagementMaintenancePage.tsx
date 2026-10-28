import { useCallback, useEffect, useMemo, useRef, useState } from "react"
import { useAuth } from "@/auth/AuthContext"
import { ApiError } from "@/api/apiClient"
import {
  assignMaintenanceRequest,
  getMaintenanceLogs,
  getMaintenanceRequest,
  getMaintenanceRequests,
  submitMaintenanceReport,
  type MaintenanceLogDto,
  type MaintenanceRequestDto,
} from "@/api/maintenanceApi"
import { getMaintenanceRooms, getRoles, getUsers, type MaintenanceRoomDto } from "@/api/usersApi"
import type { BackendUserDto } from "@/types/auth"
import MaintenanceManagementDrawer from "@/components/management/MaintenanceManagementDrawer"
import ManagementDataTable from "@/components/management/ManagementDataTable"
import ManagementPage from "@/components/management/ManagementPage"
import StatusBadge from "@/components/customer/StatusBadge"
import Toast from "@/components/customer/Toast"

type StatusFilter = "all" | MaintenanceRequestDto["status"]
type MaintenanceDetail = MaintenanceRequestDto & {
  logs: MaintenanceLogDto[]
}

const statusLabels: Record<MaintenanceRequestDto["status"], string> = {
  Pending: "Chờ xử lý",
  Approved: "Đã duyệt / Đã phân công",
  Completed: "Hoàn thành",
}

function apiFailure(error: unknown) {
  if (error instanceof ApiError && error.status) {
    const labels: Record<number, string> = {
      400: "Dữ liệu không hợp lệ",
      401: "Phiên đăng nhập hết hạn",
      403: "Bạn không có quyền thực hiện thao tác này",
      404: "Không tìm thấy yêu cầu hoặc dữ liệu liên quan",
      409: "Yêu cầu đã được cập nhật; tải lại dữ liệu trước khi thử lại",
    }
    return `${labels[error.status] ?? "Không thể hoàn tất yêu cầu"}: ${error.message}`
  }
  return error instanceof Error ? error.message : "Không thể tải dữ liệu bảo trì."
}

export default function ManagementMaintenancePage() {
  const { accessToken, user } = useAuth()
  const isAdmin = user?.role === "ADMIN"
  const [requests, setRequests] = useState<MaintenanceRequestDto[]>([])
  const [rooms, setRooms] = useState<MaintenanceRoomDto[]>([])
  const [managers, setManagers] = useState<BackendUserDto[]>([])
  const [managerRoleId, setManagerRoleId] = useState<number | null>(null)
  const [selected, setSelected] = useState<MaintenanceDetail | null>(null)
  const [filterStatus, setFilterStatus] = useState<StatusFilter>("all")
  const [filterProperty, setFilterProperty] = useState("all")
  const [filterRoom, setFilterRoom] = useState("all")
  const [search, setSearch] = useState("")
  const [assignmentManager, setAssignmentManager] = useState("")
  const [assignmentNote, setAssignmentNote] = useState("")
  const [reportDescription, setReportDescription] = useState("")
  const [reportImages, setReportImages] = useState<File[]>([])
  const [loading, setLoading] = useState(true)
  const [retryVersion, setRetryVersion] = useState(0)
  const [error, setError] = useState("")
  const [detailError, setDetailError] = useState("")
  const [actionError, setActionError] = useState("")
  const [submitting, setSubmitting] = useState(false)
  const [toast, setToast] = useState("")
  const [detailLoading, setDetailLoading] = useState(false)
  const requestVersion = useRef(0)
  const detailVersion = useRef(0)
  const actionLock = useRef(false)
  const tokenRef = useRef(accessToken)
  tokenRef.current = accessToken

  const refreshRequests = useCallback(async (signal?: AbortSignal) => {
    if (!accessToken) return
    const version = ++requestVersion.current
    setLoading(true)
    setError("")
    try {
      const data = await getMaintenanceRequests(accessToken, signal)
      if (!signal?.aborted && version === requestVersion.current && tokenRef.current === accessToken) setRequests(data)
    } catch (cause) {
      if (!signal?.aborted && version === requestVersion.current && tokenRef.current === accessToken) setError(apiFailure(cause))
    } finally {
      if (!signal?.aborted && version === requestVersion.current && tokenRef.current === accessToken) setLoading(false)
    }
  }, [accessToken])

  useEffect(() => {
    setRequests([])
    setRooms([])
    setManagers([])
    setManagerRoleId(null)
    setSelected(null)
    setError("")
    if (!accessToken) {
      setLoading(false)
      return
    }
    const controller = new AbortController()
    void refreshRequests(controller.signal)
    void getMaintenanceRooms(accessToken, controller.signal).then((items) => {
      if (!controller.signal.aborted && tokenRef.current === accessToken) setRooms(items)
    }).catch((cause: unknown) => {
      if (!controller.signal.aborted && tokenRef.current === accessToken) setError(apiFailure(cause))
    })
    if (isAdmin) {
      void getRoles().then(async (roles) => {
        const managerRole = roles.find((role) => role.roleName.trim().toLowerCase() === "manager")
        if (!managerRole) throw new Error("Backend chưa khai báo role Manager.")
        return { roleId: managerRole.roleId, managers: await getUsers(accessToken, managerRole.roleId) }
      }).then((result) => {
        if (!controller.signal.aborted && tokenRef.current === accessToken) {
          setManagerRoleId(result.roleId)
          setManagers(result.managers)
        }
      }).catch((cause: unknown) => {
        if (!controller.signal.aborted && tokenRef.current === accessToken) setError(apiFailure(cause))
      })
    }
    const refreshWhenActive = () => {
      if (document.visibilityState === "visible") void refreshRequests()
    }
    window.addEventListener("focus", refreshWhenActive)
    document.addEventListener("visibilitychange", refreshWhenActive)
    const timer = window.setInterval(() => {
      if (document.visibilityState === "visible") void refreshRequests()
    }, 30_000)
    return () => {
      controller.abort()
      requestVersion.current++
      window.clearInterval(timer)
      window.removeEventListener("focus", refreshWhenActive)
      document.removeEventListener("visibilitychange", refreshWhenActive)
    }
  }, [accessToken, isAdmin, refreshRequests, retryVersion])

  const propertyOptions = useMemo(() => {
    const unique = new Map<number, string>()
    const requestRoomIds = new Set(requests.map((item) => item.roomId))
    for (const room of rooms) if (requestRoomIds.has(room.roomId)) unique.set(room.propertyId, room.propertyName)
    return [...unique]
  }, [requests, rooms])
  const roomOptions = useMemo(() => {
    const requestRoomIds = new Set(requests.map((item) => item.roomId))
    return rooms.filter((room) => requestRoomIds.has(room.roomId) && (filterProperty === "all" || String(room.propertyId) === filterProperty))
  }, [filterProperty, requests, rooms])
  const roomById = useMemo(() => new Map(rooms.map((room) => [room.roomId, room])), [rooms])
  const filtered = useMemo(() => requests.filter((request) => {
    const room = roomById.get(request.roomId)
    const text = `${request.requestId} ${request.description} ${request.customerName ?? ""} ${request.roomCode ?? ""} ${request.equipmentName ?? ""} ${room?.propertyName ?? ""}`.toLocaleLowerCase()
    return (filterStatus === "all" || request.status === filterStatus) &&
      (filterProperty === "all" || String(room?.propertyId ?? "") === filterProperty) &&
      (filterRoom === "all" || String(request.roomId) === filterRoom) &&
      text.includes(search.trim().toLocaleLowerCase())
  }), [filterProperty, filterRoom, filterStatus, requests, roomById, search])

  async function openRequest(requestId: number) {
    if (!accessToken) return
    const version = ++detailVersion.current
    setDetailError("")
    setActionError("")
    setDetailLoading(true)
    setAssignmentManager("")
    setAssignmentNote("")
    setReportDescription("")
    setReportImages([])
    setSelected(null)
    try {
      const [detail, logs] = await Promise.all([
        getMaintenanceRequest(accessToken, requestId),
        getMaintenanceLogs(accessToken, requestId),
      ])
      if (version === detailVersion.current) setSelected({ ...detail, logs })
    } catch (cause) {
      if (version === detailVersion.current) setDetailError(apiFailure(cause))
    } finally {
      if (version === detailVersion.current) setDetailLoading(false)
    }
  }

  async function reloadSelected(requestId: number) {
    if (!accessToken) return
    const [detail, logs] = await Promise.all([
      getMaintenanceRequest(accessToken, requestId),
      getMaintenanceLogs(accessToken, requestId),
    ])
    setSelected({ ...detail, logs })
  }

  async function assignRequest() {
    if (!accessToken || !selected || selected.status !== "Pending" || actionLock.current) return
    const assignedTo = Number(assignmentManager)
    if (!Number.isSafeInteger(assignedTo) || !managers.some((manager) => Number(manager.userId) === assignedTo && manager.isActive)) {
      setActionError("Chọn một Manager đang hoạt động từ danh sách.")
      return
    }
    if (assignmentNote.length > 1000) {
      setActionError("Ghi chú tối đa 1.000 ký tự.")
      return
    }
    actionLock.current = true
    setSubmitting(true)
    setActionError("")
    const requestId = selected.requestId
    try {
      await assignMaintenanceRequest(accessToken, requestId, { assignedTo, assignmentNote })
      setToast("Đã phân công Manager thành công.")
      try { await Promise.all([refreshRequests(), reloadSelected(requestId)]) } catch (cause) { setDetailError(apiFailure(cause)) }
    } catch (cause) {
      setActionError(apiFailure(cause))
    } finally {
      actionLock.current = false
      setSubmitting(false)
    }
  }

  async function completeRequest() {
    if (!accessToken || !selected || selected.status !== "Approved" || selected.assignedTo !== Number(user?.id) || actionLock.current) return
    const validTypes = new Set(["image/jpeg", "image/png", "image/webp"])
    if (!reportDescription.trim()) {
      setActionError("Nhập mô tả công việc đã thực hiện.")
      return
    }
    if (reportImages.length < 1 || reportImages.length > 5 || reportImages.some((image) => !validTypes.has(image.type) || image.size === 0 || image.size > 5 * 1024 * 1024)) {
      setActionError("Chọn từ 1 đến 5 ảnh JPEG, PNG hoặc WebP; mỗi ảnh tối đa 5 MB.")
      return
    }
    const form = new FormData()
    form.set("Description", reportDescription.trim())
    reportImages.forEach((image) => form.append("Images", image))
    actionLock.current = true
    setSubmitting(true)
    setActionError("")
    const requestId = selected.requestId
    try {
      await submitMaintenanceReport(accessToken, requestId, form)
      setToast("Báo cáo hoàn thành đã được gửi.")
      setReportDescription("")
      setReportImages([])
      try { await Promise.all([refreshRequests(), reloadSelected(requestId)]) } catch (cause) { setDetailError(apiFailure(cause)) }
    } catch (cause) {
      setActionError(apiFailure(cause))
    } finally {
      actionLock.current = false
      setSubmitting(false)
    }
  }

  const filterButtons: Array<{ value: StatusFilter; label: string }> = [
    { value: "all", label: "Tất cả" },
    { value: "Pending", label: "Chờ xử lý" },
    { value: "Approved", label: "Đã duyệt / Đã phân công" },
    { value: "Completed", label: "Hoàn thành" },
  ]

  return (
    <ManagementPage title="Bảo trì / Sửa chữa" description={isAdmin ? "Xem yêu cầu của khách thuê và phân công Manager." : "Yêu cầu sửa chữa được Backend phân công cho bạn."}>
      {error && <div role="alert" className="mb-4 flex flex-wrap items-center justify-between gap-3 rounded-xl bg-rose-50 px-4 py-3 text-sm text-rose-700"><span>{error}</span><button onClick={() => setRetryVersion((value) => value + 1)} className="font-bold underline">Thử lại</button></div>}
      <div className="mb-4 flex flex-wrap gap-2" aria-label="Lọc theo trạng thái">
        {filterButtons.map(({ value, label }) => <button key={value} aria-pressed={filterStatus === value} onClick={() => setFilterStatus(value)} className={`rounded-full px-3 py-1.5 text-[12px] font-semibold ${filterStatus === value ? "bg-[#087775] text-white" : "bg-slate-100 text-slate-600 hover:bg-teal-50"}`}>{label}</button>)}
      </div>
      <div className="mb-5 grid gap-3 md:grid-cols-4">
        <input aria-label="Tìm yêu cầu bảo trì" type="search" value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Tìm yêu cầu, khách thuê, thiết bị..." className="h-11 rounded-lg border border-slate-200 bg-white px-3 text-[13px]" />
        <select aria-label="Lọc khu trọ" value={filterProperty} onChange={(event) => { setFilterProperty(event.target.value); setFilterRoom("all") }} className="h-11 rounded-lg border border-slate-200 bg-white px-3 text-[13px]"><option value="all">Tất cả khu trọ</option>{propertyOptions.map(([id, name]) => <option key={id} value={id}>{name}</option>)}</select>
        <select aria-label="Lọc phòng" value={filterRoom} onChange={(event) => setFilterRoom(event.target.value)} className="h-11 rounded-lg border border-slate-200 bg-white px-3 text-[13px]"><option value="all">Tất cả phòng</option>{roomOptions.map((room) => <option key={room.roomId} value={room.roomId}>{room.propertyName} · {room.roomCode}</option>)}</select>
        <button onClick={() => void refreshRequests()} className="h-11 rounded-lg border border-slate-200 bg-white px-3 text-[13px] font-semibold text-[#087775]">Làm mới</button>
      </div>
      <ManagementDataTable headers={["Yêu cầu", "Khách thuê", "Phòng", "Thiết bị / Vấn đề", "Trạng thái"]}>
        {loading && requests.length === 0 ? <p role="status" className="px-5 py-8 text-sm text-slate-500">Đang tải yêu cầu bảo trì...</p> : filtered.length === 0 ? <div role="status" className="px-5 py-10 text-center text-sm text-slate-500">{requests.length === 0 ? "Chưa có yêu cầu bảo trì." : "Không có yêu cầu khớp bộ lọc."}</div> : <div className="divide-y divide-slate-100">{filtered.map((request) => <button key={request.requestId} onClick={() => void openRequest(request.requestId)} className="grid w-full gap-3 px-5 py-4 text-left hover:bg-slate-50 lg:grid-cols-[.8fr_1.1fr_1fr_2fr_1.1fr] lg:items-center"><strong className="text-[13px] text-[#087775]">#{request.requestId}</strong><span className="text-[13px] text-slate-700">{request.customerName ?? "Khách thuê"}</span><span className="text-[13px] text-slate-600">{request.roomCode ? `Phòng ${request.roomCode}` : `Phòng #${request.roomId}`}<small className="block text-slate-400">{roomById.get(request.roomId)?.propertyName ?? ""}</small></span><span className="text-[13px] text-slate-700">{request.equipmentName ?? "Thiết bị không xác định"}<small className="mt-1 block text-slate-500">{request.description}</small></span><StatusBadge tone={request.status === "Completed" ? "green" : request.status === "Pending" ? "amber" : "slate"}>{statusLabels[request.status]}</StatusBadge></button>)}</div>}
      </ManagementDataTable>
      {detailLoading && <div role="status" className="fixed right-4 top-20 z-[110] rounded-xl border border-slate-200 bg-white p-4 text-sm text-slate-600 shadow-xl">Đang tải chi tiết và lịch sử...</div>}
      {detailError && <div role="alert" className="fixed right-4 top-20 z-[110] max-w-sm rounded-xl border border-rose-200 bg-white p-4 text-sm text-rose-700 shadow-xl"><p>{detailError}</p><button onClick={() => { setDetailError(""); if (selected) void openRequest(selected.requestId) }} className="mt-2 font-semibold">Thử lại</button></div>}
      {selected && <MaintenanceManagementDrawer
        request={selected}
        propertyName={roomById.get(selected.roomId)?.propertyName ?? null}
        detailLoading={detailLoading}
        onClose={() => { detailVersion.current++; setSelected(null) }}
        managers={isAdmin ? managers : []}
        managerRoleLoading={isAdmin && managerRoleId === null}
        canAssign={isAdmin}
        canComplete={user?.role === "MANAGER" && selected.assignedTo === Number(user.id)}
        submitting={submitting}
        actionError={actionError}
        reportDescription={reportDescription}
        reportImages={reportImages}
        onManagerChange={setAssignmentManager}
        onAssignmentNoteChange={setAssignmentNote}
        assignmentNote={assignmentNote}
        onAssign={() => void assignRequest()}
        onReportDescriptionChange={setReportDescription}
        onReportImagesChange={setReportImages}
        onComplete={() => void completeRequest()}
      />}
      {toast && <Toast message={toast} onClose={() => setToast("")} />}
    </ManagementPage>
  )
}
