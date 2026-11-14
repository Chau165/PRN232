import { useEffect, useState } from "react"
import { Link, useLocation } from "react-router"
import ManagementDataTable from "@/components/management/ManagementDataTable"
import ManagementPage from "@/components/management/ManagementPage"
import StatusBadge from "@/components/customer/StatusBadge"
import Toast from "@/components/customer/Toast"
import { useAuth } from "@/auth/AuthContext"
import {
  approveViewingSchedule,
  getAvailableViewingRooms,
  rejectViewingSchedule,
  selectViewingRoom,
} from "@/services/viewingScheduleService"
import { getRoles, getUsers } from "@/services/userService"
import { getPropertyManagerAssignments } from "@/services/propertyManagerAssignmentService"
import type { BackendUserDto } from "@/types/auth"
import type { ViewingAvailableRoomDto, ViewingScheduleDto } from "@/types/viewingSchedule"
import { useViewingSchedules } from "@/hooks/useViewingSchedules"
import { bookingErrorMessage } from "@/utils/bookingDisplay"
import { formatVnd } from "@/utils/publicFormatting"
import { availableRoomsPath } from "@/constants/routes"
import { localInputToIso, toLocalInput, viewingStatusDisplay, viewingTimeLabel } from "@/utils/viewingDisplay"

function hasUpcomingTime(viewing: ViewingScheduleDto) {
  return viewing.scheduledAt !== null && Date.parse(viewing.scheduledAt) > Date.now()
}

function positiveQueryId(search: string, key: string): number | null {
  const value = new URLSearchParams(search).get(key)
  if (!value || !/^[1-9]\d*$/.test(value)) return null
  const id = Number(value)
  return Number.isSafeInteger(id) ? id : null
}

export default function ManagementViewingsPage() {
  const { accessToken, user } = useAuth()
  const isAdmin = user?.role === "ADMIN"
  const isManager = user?.role === "MANAGER"
  const location = useLocation()
  const selectedViewingId = positiveQueryId(location.search, "viewingId")
  const { viewings, loading, error, retry, replace } = useViewingSchedules()
  const [managers, setManagers] = useState<BackendUserDto[]>([])
  const [managerError, setManagerError] = useState<string | null>(null)
  const [managersLoading, setManagersLoading] = useState(false)
  const [busyId, setBusyId] = useState<number | null>(null)
  const [picker, setPicker] = useState<ViewingScheduleDto | null>(null)
  const [pickerTime, setPickerTime] = useState("")
  const [assignedTo, setAssignedTo] = useState("")
  const [managerNote, setManagerNote] = useState("")
  const [pickerError, setPickerError] = useState<string | null>(null)
  const [roomPicker, setRoomPicker] = useState<ViewingScheduleDto | null>(null)
  const [availableRooms, setAvailableRooms] = useState<ViewingAvailableRoomDto[]>([])
  const [selectedRoomId, setSelectedRoomId] = useState("")
  const [roomsLoading, setRoomsLoading] = useState(false)
  const [roomPickerError, setRoomPickerError] = useState<string | null>(null)
  const [rejecting, setRejecting] = useState<ViewingScheduleDto | null>(null)
  const [rejectReason, setRejectReason] = useState("")
  const [rejectError, setRejectError] = useState<string | null>(null)
  const [toast, setToast] = useState("")
  const pickerPropertyId = picker?.propertyId

  useEffect(() => {
    if (!isAdmin || !accessToken || pickerPropertyId === undefined) return
    let active = true
    setManagers([])
    setManagerError(null)
    setManagersLoading(true)
    void Promise.all([getRoles(), getPropertyManagerAssignments(accessToken, pickerPropertyId)])
      .then(async ([roles, assignments]) => {
        const managerRole = roles.find((role) => role.roleName.trim().toLowerCase() === "manager")
        if (!managerRole) throw new Error("Backend chưa khai báo role Manager.")
        const rows = await getUsers(accessToken, managerRole.roleId)
        const assignedIds = new Set(assignments
          .filter((assignment) => assignment.propertyId === pickerPropertyId && assignment.managerIsActive)
          .map((assignment) => assignment.managerId))
        return rows.filter((manager) => assignedIds.has(Number(manager.userId))
          && manager.roleName.trim().toUpperCase() === "MANAGER" && manager.isActive === true)
      })
      .then((rows) => {
        if (active) setManagers(rows)
      })
      .catch((cause: unknown) => {
        if (active) setManagerError(cause instanceof Error ? cause.message : "Không thể tải danh sách Manager.")
      })
      .finally(() => { if (active) setManagersLoading(false) })
    return () => { active = false }
  }, [accessToken, isAdmin, pickerPropertyId])

  useEffect(() => {
    if (selectedViewingId === null || loading) return
    document.getElementById(`viewing-${selectedViewingId}`)?.scrollIntoView?.({ behavior: "smooth", block: "center" })
  }, [loading, selectedViewingId, viewings])

  const run = async (
    viewing: ViewingScheduleDto,
    action: (token: string) => Promise<ViewingScheduleDto>,
    success: string,
  ) => {
    if (!accessToken || busyId !== null) return
    setBusyId(viewing.viewingId)
    try {
      replace(await action(accessToken))
      setToast(success)
      setPicker(null)
      setRoomPicker(null)
      setRejecting(null)
    } catch (actionError) {
      setToast(bookingErrorMessage(actionError, "Yêu cầu này vừa được xử lý, vui lòng tải lại trang."))
      retry()
    } finally {
      setBusyId(null)
    }
  }

  const openPicker = (viewing: ViewingScheduleDto) => {
    setManagers([])
    setManagerError(null)
    setManagersLoading(true)
    setPicker(viewing)
    setPickerTime(hasUpcomingTime(viewing) ? toLocalInput(new Date(viewing.scheduledAt!)) : "")
    setAssignedTo("")
    setManagerNote("")
    setPickerError(null)
  }

  const approve = () => {
    if (!picker || managersLoading || managerError) return
    const managerId = Number(assignedTo)
    if (!Number.isSafeInteger(managerId) || managerId < 1 || !managers.some((manager) => Number(manager.userId) === managerId)) {
      setPickerError("Vui lòng chọn Manager đang hoạt động được phân quyền cho khu trọ này.")
      return
    }
    if (!pickerTime || Date.parse(pickerTime) <= Date.now()) {
      setPickerError("Vui lòng chọn thời điểm xem trong tương lai.")
      return
    }
    if (managerNote.length > 500) {
      setPickerError("Ghi chú tối đa 500 ký tự.")
      return
    }
    void run(picker, (token) => approveViewingSchedule(token, picker.viewingId, {
      scheduledAt: localInputToIso(pickerTime),
      assignedTo: managerId,
      managerNote: managerNote.trim() || null,
    }), `Đã phân công Manager và xác nhận lịch xem ${picker.roomTypeName}.`)
  }

  const reject = () => {
    if (!rejecting) return
    const reason = rejectReason.trim()
    if (!reason) {
      setRejectError("Vui lòng nhập lý do từ chối.")
      return
    }
    if (reason.length > 500) {
      setRejectError("Lý do tối đa 500 ký tự.")
      return
    }
    void run(rejecting, (token) => rejectViewingSchedule(token, rejecting.viewingId, reason),
      `Đã từ chối lịch xem phòng của ${rejecting.customerName}.`)
  }

  async function openRoomPicker(viewing: ViewingScheduleDto) {
    if (!accessToken) return
    setRoomPicker(viewing)
    setAvailableRooms([])
    setSelectedRoomId("")
    setRoomPickerError(null)
    setRoomsLoading(true)
    try {
      setAvailableRooms(await getAvailableViewingRooms(accessToken, viewing.viewingId))
    } catch (cause) {
      setRoomPickerError(bookingErrorMessage(cause, "Không thể tải các phòng còn phù hợp cho lịch xem này."))
    } finally {
      setRoomsLoading(false)
    }
  }

  function confirmSelectedRoom() {
    if (!roomPicker || !accessToken) return
    const roomId = Number(selectedRoomId)
    if (!Number.isSafeInteger(roomId) || roomId < 1 || !availableRooms.some((room) => room.roomId === roomId)) {
      setRoomPickerError("Chọn một phòng trong danh sách còn khả dụng.")
      return
    }
    void run(
      roomPicker,
      (token) => selectViewingRoom(token, roomPicker.viewingId, roomId),
      "Đã xác nhận phòng dẫn khách xem.",
    )
  }

  return (
    <ManagementPage
      title="Lịch xem phòng"
      description={isAdmin
        ? "Xem mọi yêu cầu, phân công Manager và xác nhận hoặc từ chối lịch hẹn."
        : "Các lịch được phân công cho bạn. Chọn phòng cụ thể trước khi khách có thể đặt cọc."}
    >
      {loading && <p role="status" className="text-[14px] text-slate-500">Đang tải lịch xem phòng...</p>}
      {!loading && error && (
        <div role="alert" className="rounded-2xl bg-white p-6 text-center shadow-[0_3px_18px_rgba(16,65,67,.05)]">
          <p className="text-[14px] text-slate-700">{error}</p>
          <button type="button" onClick={retry} className="mt-4 rounded-lg bg-[#087775] px-4 py-2 text-[13px] font-bold text-white">Thử lại</button>
        </div>
      )}
      {!loading && !error && (
        <ManagementDataTable headers={["Khách đặt", "Số điện thoại", "Ngày xem", "Khu trọ / phòng", "Trạng thái", ...(isAdmin || isManager ? ["Thao tác"] : [])]}>
          {viewings.length === 0 ? (
            <p className="px-5 py-8 text-center text-[13px] text-slate-500">
              {isAdmin ? "Chưa có yêu cầu xem phòng nào." : "Chưa có lịch xem phòng được phân công cho bạn."}
            </p>
          ) : (
            <div className="divide-y divide-slate-100">
              {viewings.map((viewing) => {
                const display = viewingStatusDisplay(viewing)
                const pending = viewing.status === "Pending"
                const selected = viewing.viewingId === selectedViewingId
                return (
                  <article
                    id={`viewing-${viewing.viewingId}`}
                    key={viewing.viewingId}
                    className={`grid gap-3 px-5 py-5 lg:grid-cols-6 lg:items-center ${selected ? "bg-teal-50 ring-2 ring-inset ring-teal-300" : ""}`}
                  >
                    <span className="text-[13px] font-semibold text-slate-700">
                      {viewing.customerName}<small className="block font-normal text-slate-400">#{viewing.viewingId}</small>
                    </span>
                    <a className="text-[13px] text-slate-600 hover:text-[#087775]" href={`tel:${viewing.customerPhone}`}>{viewing.customerPhone || "—"}</a>
                    <span className="text-[13px] text-slate-600">{viewingTimeLabel(viewing)}</span>
                    <span className="text-[13px] text-slate-600">
                      {viewing.propertyName} · {viewing.roomTypeName}
                      {viewing.currentMonthlyRent !== null && <small className="mt-1 block text-slate-500">Giá hiện tại: {formatVnd(viewing.currentMonthlyRent)}/tháng</small>}
                      {viewing.roomId === null ? (
                        <small className="mt-1 block text-amber-700">Manager chưa chọn phòng cụ thể.</small>
                      ) : (
                        <Link to={`${availableRoomsPath(viewing.propertyId)}?roomId=${viewing.roomId}`} className="mt-1 block text-[#087775] underline underline-offset-2">
                          Phòng {viewing.roomCode} · Xem phòng
                        </Link>
                      )}
                    </span>
                    <div>
                      <StatusBadge tone={display.tone}>{display.label}</StatusBadge>
                      {viewing.assignedManagerName && <p className="mt-2 text-xs text-slate-500">Manager: {viewing.assignedManagerName}{viewing.assignedManagerPhone ? ` · ${viewing.assignedManagerPhone}` : ""}</p>}
                      {viewing.managerNote && <p className="mt-2 whitespace-pre-wrap text-xs text-slate-600">Ghi chú: {viewing.managerNote}</p>}
                    </div>
                    {isAdmin && (
                      <div className="flex flex-wrap gap-2">
                        {pending ? <>
                          <button type="button" disabled={busyId !== null} onClick={() => openPicker(viewing)} className="rounded-lg bg-teal-50 px-3 py-2 text-[12px] font-bold text-[#087775] disabled:opacity-40">Phân công & duyệt</button>
                          <button type="button" disabled={busyId !== null} onClick={() => { setRejecting(viewing); setRejectReason(""); setRejectError(null) }} className="rounded-lg bg-rose-50 px-3 py-2 text-[12px] font-bold text-rose-600 disabled:opacity-40">Từ chối</button>
                        </> : <span className="text-xs text-slate-400">Đã xử lý</span>}
                      </div>
                    )}
                    {isManager && viewing.status === "Approved" && viewing.roomId === null && (
                      <button type="button" disabled={busyId !== null} onClick={() => void openRoomPicker(viewing)} className="rounded-lg bg-teal-50 px-3 py-2 text-[12px] font-bold text-[#087775] disabled:opacity-40">
                        Chọn phòng dẫn khách xem
                      </button>
                    )}
                  </article>
                )
              })}
            </div>
          )}
        </ManagementDataTable>
      )}

      {picker && isAdmin && (
        <div className="fixed inset-0 z-50 grid place-items-center bg-slate-950/40 p-6">
          <div role="dialog" aria-modal="true" aria-labelledby="viewing-picker-title" className="w-full max-w-lg rounded-2xl bg-white p-6 shadow-2xl">
            <h2 id="viewing-picker-title" className="font-display text-xl font-bold text-slate-900">Phân công và xác nhận lịch</h2>
            <p className="mt-2 text-sm text-slate-500">{picker.customerName} · {picker.propertyName} · {picker.roomTypeName}</p>
            <label className="mt-5 block text-sm font-semibold text-slate-700" htmlFor="viewing-manager">Manager phụ trách</label>
            <select id="viewing-manager" required disabled={managersLoading || !!managerError || managers.length === 0} value={assignedTo} onChange={(event) => setAssignedTo(event.target.value)} className="mt-2 h-11 w-full rounded-xl border border-slate-200 bg-white px-3 text-sm">
              <option value="">{managersLoading ? "Đang tải Manager của khu trọ..." : "Chọn Manager được phân quyền cho khu trọ"}</option>
              {managers.map((manager) => <option key={manager.userId} value={manager.userId}>{manager.fullName}{manager.phoneNumber ? ` · ${manager.phoneNumber}` : ""}</option>)}
            </select>
            {managerError && <p role="alert" className="mt-2 text-xs text-rose-600">{managerError}</p>}
            {!managersLoading && managers.length === 0 && !managerError && <p className="mt-2 text-xs text-amber-700">Khu trọ này chưa có Manager đang hoạt động được phân quyền.</p>}
            <label className="mt-4 block text-sm font-semibold text-slate-700" htmlFor="viewing-time">Thời gian xác nhận</label>
            <input id="viewing-time" type="datetime-local" required min={toLocalInput()} value={pickerTime} onChange={(event) => setPickerTime(event.target.value)} className="mt-2 h-11 w-full rounded-xl border border-slate-200 px-3 text-sm" />
            <label className="mt-4 block text-sm font-semibold text-slate-700" htmlFor="viewing-note">Ghi chú cho Manager / khách (không bắt buộc)</label>
            <textarea id="viewing-note" maxLength={500} rows={3} value={managerNote} onChange={(event) => setManagerNote(event.target.value)} className="mt-2 w-full rounded-xl border border-slate-200 px-3 py-2 text-sm" />
            <p className="mt-1 text-right text-xs text-slate-400">{managerNote.length}/500</p>
            {pickerError && <p role="alert" className="mt-3 text-sm text-rose-600">{pickerError}</p>}
            <div className="mt-5 flex gap-2">
              <button type="button" onClick={() => setPicker(null)} className="flex-1 rounded-lg bg-slate-100 py-3 text-sm font-bold text-slate-600">Huỷ</button>
              <button type="button" disabled={busyId !== null || managersLoading || !!managerError || managers.length === 0} onClick={approve} className="flex-1 rounded-lg bg-[#087775] py-3 text-sm font-bold text-white disabled:opacity-40">Xác nhận & thông báo</button>
            </div>
          </div>
        </div>
      )}

      {rejecting && isAdmin && (
        <div className="fixed inset-0 z-50 grid place-items-center bg-slate-950/40 p-6">
          <div role="dialog" aria-modal="true" aria-labelledby="viewing-reject-title" className="w-full max-w-md rounded-2xl bg-white p-6 shadow-2xl">
            <h2 id="viewing-reject-title" className="font-display text-xl font-bold text-slate-900">Từ chối yêu cầu xem phòng</h2>
            <p className="mt-2 text-sm text-slate-500">Yêu cầu #{rejecting.viewingId} · {rejecting.customerName}</p>
            <label className="mt-5 block text-sm font-semibold text-slate-700" htmlFor="viewing-reject-reason">Lý do từ chối</label>
            <textarea id="viewing-reject-reason" required maxLength={500} rows={4} value={rejectReason} onChange={(event) => setRejectReason(event.target.value)} className="mt-2 w-full rounded-xl border border-slate-200 px-3 py-2 text-sm" />
            <p className="mt-1 text-right text-xs text-slate-400">{rejectReason.length}/500</p>
            {rejectError && <p role="alert" className="mt-3 text-sm text-rose-600">{rejectError}</p>}
            <div className="mt-5 flex gap-2">
              <button type="button" onClick={() => setRejecting(null)} className="flex-1 rounded-lg bg-slate-100 py-3 text-sm font-bold text-slate-600">Huỷ</button>
              <button type="button" disabled={busyId !== null} onClick={reject} className="flex-1 rounded-lg bg-rose-600 py-3 text-sm font-bold text-white disabled:opacity-40">Từ chối & thông báo</button>
            </div>
          </div>
        </div>
      )}
      {roomPicker && isManager && (
        <div className="fixed inset-0 z-50 grid place-items-center bg-slate-950/40 p-6">
          <div role="dialog" aria-modal="true" aria-labelledby="viewing-room-title" className="w-full max-w-lg rounded-2xl bg-white p-6 shadow-2xl">
            <h2 id="viewing-room-title" className="font-display text-xl font-bold text-slate-900">Chọn phòng dẫn khách xem</h2>
            <p className="mt-2 text-sm text-slate-500">{roomPicker.propertyName} · {roomPicker.roomTypeName}</p>
            {roomsLoading ? <p role="status" className="mt-5 text-sm text-slate-500">Đang kiểm tra phòng khả dụng...</p> : (
              <label className="mt-5 block text-sm font-semibold text-slate-700">
                Phòng đủ điều kiện
                <select aria-label="Phòng đủ điều kiện" value={selectedRoomId} onChange={(event) => setSelectedRoomId(event.target.value)} disabled={availableRooms.length === 0} className="mt-2 h-11 w-full rounded-xl border border-slate-200 bg-white px-3 text-sm font-normal">
                  <option value="">{availableRooms.length ? "Chọn phòng" : "Không có phòng khả dụng"}</option>
                  {availableRooms.map((room) => <option key={room.roomId} value={room.roomId}>{room.roomCode} · {room.areaSqm} m² · {room.status}</option>)}
                </select>
              </label>
            )}
            {roomPickerError && <p role="alert" className="mt-3 text-sm text-rose-600">{roomPickerError}</p>}
            <div className="mt-5 flex gap-2">
              <button type="button" onClick={() => setRoomPicker(null)} className="flex-1 rounded-lg bg-slate-100 py-3 text-sm font-bold text-slate-600">Đóng</button>
              <button type="button" disabled={busyId !== null || roomsLoading || availableRooms.length === 0} onClick={confirmSelectedRoom} className="flex-1 rounded-lg bg-[#087775] py-3 text-sm font-bold text-white disabled:opacity-40">Xác nhận phòng</button>
            </div>
          </div>
        </div>
      )}
      {toast && <Toast message={toast} onClose={() => setToast("")} />}
    </ManagementPage>
  )
}
