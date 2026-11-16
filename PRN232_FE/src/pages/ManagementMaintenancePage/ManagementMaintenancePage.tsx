import { useCallback, useEffect, useMemo, useRef, useState } from "react"
import { useAuth } from "@/auth/AuthContext"
import { ApiError } from "@/api/apiClient"
import {
  assignMaintenanceRequest,
  getMaintenanceLogs,
  getMaintenanceRequest,
  getMaintenanceReport,
  getMaintenanceRequests,
  submitMaintenanceReport,
} from "@/services/maintenanceService"
import { getMaintenanceRooms } from "@/services/userService"
import { getPropertyManagerAssignments } from "@/services/propertyManagerAssignmentService"
import type { PropertyManagerAssignmentDto } from "@/types/propertyManagerAssignment"
import type { MaintenanceLogDto, MaintenanceReportDto, MaintenanceRequestDto } from "@/types/maintenance"
import type { MaintenanceRoomDto } from "@/types/user"
import MaintenanceManagementDrawer from "@/components/management/MaintenanceManagementDrawer"
import ManagementDataTable from "@/components/management/ManagementDataTable"
import ManagementPage from "@/components/management/ManagementPage"
import {
  ManagementEmptyState,
  ManagementErrorState,
  ManagementLoadingState,
} from "@/components/management/ManagementState"
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
  const [roomsLoaded, setRoomsLoaded] = useState(false)
  const [managerAssignments, setManagerAssignments] = useState<PropertyManagerAssignmentDto[]>([])
  const [managerAssignmentsPropertyId, setManagerAssignmentsPropertyId] = useState<number | null>(null)
  const [managerAssignmentsRequestId, setManagerAssignmentsRequestId] = useState<number | null>(null)
  const [managerAssignmentsLoading, setManagerAssignmentsLoading] = useState(false)
  const [managerAssignmentsError, setManagerAssignmentsError] = useState<{ requestId: number; message: string } | null>(null)
  const [managerAssignmentsReload, setManagerAssignmentsReload] = useState(0)
  const [selected, setSelected] = useState<MaintenanceDetail | null>(null)
  const [report, setReport] = useState<MaintenanceReportDto | null>(null)
  const [reportLoading, setReportLoading] = useState(false)
  const [reportNotFound, setReportNotFound] = useState(false)
  const [reportError, setReportError] = useState("")
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
  const managerAssignmentVersion = useRef(0)
  const managerAssignmentController = useRef<AbortController | null>(null)
  const detailController = useRef<AbortController | null>(null)
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
    detailVersion.current++
    detailController.current?.abort()
    detailController.current = null
    setRequests([])
    setRooms([])
    setRoomsLoaded(false)
    setManagerAssignments([])
    setManagerAssignmentsPropertyId(null)
    setManagerAssignmentsRequestId(null)
    setManagerAssignmentsLoading(false)
    setManagerAssignmentsError(null)
    setSelected(null)
    setReport(null)
    setReportError("")
    setReportNotFound(false)
    setError("")
    if (!accessToken) {
      setLoading(false)
      return
    }
    const controller = new AbortController()
    void refreshRequests(controller.signal)
    void getMaintenanceRooms(accessToken, controller.signal).then((items) => {
      if (!controller.signal.aborted && tokenRef.current === accessToken) {
        setRooms(items)
        setRoomsLoaded(true)
      }
    }).catch((cause: unknown) => {
      if (!controller.signal.aborted && tokenRef.current === accessToken) {
        setRoomsLoaded(true)
        setError(apiFailure(cause))
      }
    })
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
      detailController.current?.abort()
      managerAssignmentController.current?.abort()
      managerAssignmentVersion.current++
      detailVersion.current++
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
  const selectedPropertyId = selected
    ? roomById.get(selected.roomId)?.propertyId ?? null
    : null
  const managerAssignmentsAreCurrent = Boolean(
    selected &&
      managerAssignmentsRequestId === selected.requestId &&
      managerAssignmentsPropertyId === selectedPropertyId,
  )
  const selectedManagerAssignments = managerAssignmentsAreCurrent
    ? managerAssignments.filter(
        (assignment) =>
          assignment.propertyId === selectedPropertyId &&
          assignment.managerIsActive,
      )
    : []
  const selectedManagerAssignmentError =
    selected && managerAssignmentsError?.requestId === selected.requestId
      ? managerAssignmentsError.message
      : ""
  const selectedManagerAssignmentsLoading = Boolean(
    isAdmin &&
      selected?.status === "Pending" &&
      (!roomsLoaded ||
        (selectedPropertyId === null
          ? !selectedManagerAssignmentError
          : !managerAssignmentsAreCurrent || managerAssignmentsLoading)),
  )

  useEffect(() => {
    const version = ++managerAssignmentVersion.current
    managerAssignmentController.current?.abort()
    managerAssignmentController.current = null

    if (!isAdmin || !selected || selected.status !== "Pending") {
      setManagerAssignments([])
      setManagerAssignmentsPropertyId(null)
      setManagerAssignmentsRequestId(null)
      setManagerAssignmentsLoading(false)
      setManagerAssignmentsError(null)
      return
    }

    setManagerAssignments([])
    setManagerAssignmentsPropertyId(null)
    setManagerAssignmentsRequestId(selected.requestId)
    setManagerAssignmentsError(null)
    if (!roomsLoaded) {
      setManagerAssignmentsLoading(true)
      return
    }

    const propertyId = roomById.get(selected.roomId)?.propertyId
    if (propertyId === undefined) {
      setManagerAssignmentsLoading(false)
      setManagerAssignmentsError({
        requestId: selected.requestId,
        message: "Không xác định được khu trọ của phòng trong yêu cầu.",
      })
      return
    }
    if (!accessToken) {
      setManagerAssignmentsLoading(false)
      setManagerAssignmentsError({
        requestId: selected.requestId,
        message: "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
      })
      return
    }

    const controller = new AbortController()
    managerAssignmentController.current = controller
    setManagerAssignmentsPropertyId(propertyId)
    setManagerAssignmentsLoading(true)
    void getPropertyManagerAssignments(
      accessToken,
      propertyId,
      controller.signal,
    )
      .then((assignments) => {
        if (controller.signal.aborted || version !== managerAssignmentVersion.current) return
        setManagerAssignments(
          assignments.filter(
            (assignment) =>
              assignment.propertyId === propertyId &&
              assignment.managerIsActive,
          ),
        )
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || version !== managerAssignmentVersion.current) return
        setManagerAssignments([])
        setManagerAssignmentsError({
          requestId: selected.requestId,
          message: apiFailure(cause),
        })
      })
      .finally(() => {
        if (!controller.signal.aborted && version === managerAssignmentVersion.current) {
          setManagerAssignmentsLoading(false)
          managerAssignmentController.current = null
        }
      })

    return () => {
      controller.abort()
      if (managerAssignmentController.current === controller) {
        managerAssignmentController.current = null
      }
    }
  }, [
    accessToken,
    isAdmin,
    managerAssignmentsReload,
    roomById,
    roomsLoaded,
    selected,
  ])

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
    detailController.current?.abort()
    managerAssignmentController.current?.abort()
    managerAssignmentVersion.current++
    managerAssignmentController.current = null
    const controller = new AbortController()
    detailController.current = controller
    setDetailError("")
    setReport(null)
    setReportLoading(false)
    setReportNotFound(false)
    setReportError("")
    setActionError("")
    setDetailLoading(true)
    setAssignmentManager("")
    setAssignmentNote("")
    setManagerAssignments([])
    setManagerAssignmentsPropertyId(null)
    setManagerAssignmentsRequestId(null)
    setManagerAssignmentsLoading(false)
    setManagerAssignmentsError(null)
    setReportDescription("")
    setReportImages([])
    setSelected(null)
    try {
      const [detail, logs] = await Promise.all([
        getMaintenanceRequest(accessToken, requestId, controller.signal),
        getMaintenanceLogs(accessToken, requestId, controller.signal),
      ])
      if (controller.signal.aborted || version !== detailVersion.current) return
      setSelected({ ...detail, logs })
      if (detail.status === "Completed" && (isAdmin || user?.role === "MANAGER")) {
        await loadReport(requestId, version, controller.signal)
      }
    } catch (cause) {
      if (!controller.signal.aborted && version === detailVersion.current) setDetailError(apiFailure(cause))
    } finally {
      if (!controller.signal.aborted && version === detailVersion.current) setDetailLoading(false)
    }
  }

  async function loadReport(requestId: number, version = detailVersion.current, signal = detailController.current?.signal) {
    if (!accessToken || !(isAdmin || user?.role === "MANAGER")) return
    setReport(null)
    setReportNotFound(false)
    setReportError("")
    setReportLoading(true)
    try {
      const result = await getMaintenanceReport(accessToken, requestId, signal)
      if (!signal?.aborted && version === detailVersion.current) setReport(result)
    } catch (cause) {
      if (signal?.aborted || version !== detailVersion.current) return
      if (cause instanceof ApiError && cause.status === 404) setReportNotFound(true)
      else setReportError(`Không thể tải báo cáo: ${apiFailure(cause)}`)
    } finally {
      if (!signal?.aborted && version === detailVersion.current) setReportLoading(false)
    }
  }

  async function reloadSelected(requestId: number, expectedVersion: number) {
    if (!accessToken) return
    if (detailVersion.current !== expectedVersion) return
    const version = expectedVersion
    const signal = detailController.current?.signal
    const [detail, logs] = await Promise.all([
      getMaintenanceRequest(accessToken, requestId, signal),
      getMaintenanceLogs(accessToken, requestId, signal),
    ])
    if (signal?.aborted || version !== detailVersion.current) return
    setSelected({ ...detail, logs })
    if (detail.status === "Completed" && (isAdmin || user?.role === "MANAGER")) {
      await loadReport(requestId, version, signal)
    } else {
      setReport(null)
      setReportNotFound(false)
      setReportError("")
    }
  }

  async function assignRequest() {
    if (!accessToken || !selected || selected.status !== "Pending" || actionLock.current) return
    const assignedTo = Number(assignmentManager)
    if (
      !Number.isSafeInteger(assignedTo) ||
      !selectedPropertyId ||
      !managerAssignmentsAreCurrent ||
      !selectedManagerAssignments.some(
        (assignment) =>
          assignment.propertyId === selectedPropertyId &&
          assignment.managerId === assignedTo &&
          assignment.managerIsActive,
      )
    ) {
      setActionError("Chọn Manager đang hoạt động được phân công cho khu trọ này.")
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
    const selectedVersion = detailVersion.current
    try {
      await assignMaintenanceRequest(accessToken, requestId, { assignedTo, assignmentNote })
      setToast("Đã phân công Manager thành công.")
      try { await Promise.all([refreshRequests(), reloadSelected(requestId, selectedVersion)]) } catch (cause) { if (detailVersion.current === selectedVersion) setDetailError(apiFailure(cause)) }
    } catch (cause) {
      if (detailVersion.current === selectedVersion) {
        setActionError(apiFailure(cause))
        setAssignmentManager("")
        managerAssignmentController.current?.abort()
        managerAssignmentController.current = null
        setManagerAssignments([])
        setManagerAssignmentsPropertyId(null)
        setManagerAssignmentsRequestId(null)
        setManagerAssignmentsError(null)
        setManagerAssignmentsReload((value) => value + 1)
      }
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
    const selectedVersion = detailVersion.current
    try {
      await submitMaintenanceReport(accessToken, requestId, form)
      setToast("Báo cáo hoàn thành đã được gửi.")
      setReportDescription("")
      setReportImages([])
      try { await Promise.all([refreshRequests(), reloadSelected(requestId, selectedVersion)]) } catch (cause) { if (detailVersion.current === selectedVersion) setDetailError(apiFailure(cause)) }
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
      {error && (
        <ManagementErrorState
          variant="banner"
          message={error}
          onRetry={() => setRetryVersion((value) => value + 1)}
        />
      )}
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
        {loading && requests.length === 0 ? (
          <ManagementLoadingState message="Đang tải yêu cầu bảo trì..." />
        ) : filtered.length === 0 ? (
          <ManagementEmptyState
            icon="wrench"
            message={
              requests.length === 0
                ? "Chưa có yêu cầu bảo trì."
                : "Không có yêu cầu khớp bộ lọc."
            }
          />
        ) : (
          <div className="divide-y divide-slate-100">{filtered.map((request) => <button key={request.requestId} onClick={() => void openRequest(request.requestId)} className="grid w-full gap-3 px-5 py-4 text-left hover:bg-slate-50 lg:grid-cols-[.8fr_1.1fr_1fr_2fr_1.1fr] lg:items-center"><strong className="text-[13px] text-[#087775]">#{request.requestId}</strong><span className="text-[13px] text-slate-700">{request.customerName ?? "Khách thuê"}</span><span className="text-[13px] text-slate-600">{request.roomCode ? `Phòng ${request.roomCode}` : `Phòng #${request.roomId}`}<small className="block text-slate-400">{roomById.get(request.roomId)?.propertyName ?? ""}</small></span><span className="text-[13px] text-slate-700">{request.equipmentName ?? "Thiết bị không xác định"}<small className="mt-1 block text-slate-500">{request.description}</small></span><StatusBadge tone={request.status === "Completed" ? "green" : request.status === "Pending" ? "amber" : "slate"}>{statusLabels[request.status]}</StatusBadge></button>)}</div>
        )}
      </ManagementDataTable>
      {detailLoading && <div role="status" className="fixed right-4 top-20 z-[110] rounded-xl border border-slate-200 bg-white p-4 text-sm text-slate-600 shadow-xl">Đang tải chi tiết và lịch sử...</div>}
      {detailError && <div role="alert" className="fixed right-4 top-20 z-[110] max-w-sm rounded-xl border border-rose-200 bg-white p-4 text-sm text-rose-700 shadow-xl"><p>{detailError}</p><button onClick={() => { setDetailError(""); if (selected) void openRequest(selected.requestId) }} className="mt-2 font-semibold">Thử lại</button></div>}
      {selected && <MaintenanceManagementDrawer
        request={selected}
        propertyName={roomById.get(selected.roomId)?.propertyName ?? null}
        detailLoading={detailLoading}
        onClose={() => {
          detailVersion.current++
          managerAssignmentVersion.current++
          detailController.current?.abort()
          managerAssignmentController.current?.abort()
          managerAssignmentController.current = null
          setSelected(null)
          setReport(null)
          setAssignmentManager("")
          setAssignmentNote("")
        }}
        managers={isAdmin ? selectedManagerAssignments : []}
        managerLoading={selectedManagerAssignmentsLoading}
        managerLoadError={selectedManagerAssignmentError}
        assignmentManager={assignmentManager}
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
        report={report}
        reportLoading={reportLoading}
        reportNotFound={reportNotFound}
        reportError={reportError}
        onRetryReport={() => void loadReport(selected.requestId)}
       />}
      {toast && <Toast message={toast} onClose={() => setToast("")} />}
    </ManagementPage>
  )
}
