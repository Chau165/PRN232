import { useState } from "react"
import ManagementDataTable from "@/components/management/ManagementDataTable"
import ManagementPage from "@/components/management/ManagementPage"
import StatusBadge from "@/components/customer/StatusBadge"
import Toast from "@/components/customer/Toast"
import { useAuth } from "@/auth/AuthContext"
import { approveViewingSchedule, rejectViewingSchedule } from "@/services/viewingScheduleService"
import type { ViewingScheduleDto } from "@/types/viewingSchedule"
import { useViewingSchedules } from "@/hooks/useViewingSchedules"
import { bookingErrorMessage } from "@/utils/bookingDisplay"
import { localInputToIso, toLocalInput, viewingStatusDisplay, viewingTimeLabel } from "@/utils/viewingDisplay"

// Khách để chủ trọ sắp xếp, hoặc giờ khách chọn đã qua: phải chọn giờ mới khi chấp thuận.
function hasUpcomingTime(viewing: ViewingScheduleDto) {
  return viewing.scheduledAt !== null && Date.parse(viewing.scheduledAt) > Date.now()
}

export default function ManagementViewingsPage() {
  const { accessToken } = useAuth()
  const { viewings, loading, error, retry, replace } = useViewingSchedules()
  const [busyId, setBusyId] = useState<number | null>(null)
  const [picker, setPicker] = useState<ViewingScheduleDto | null>(null)
  const [pickerTime, setPickerTime] = useState("")
  const [pickerError, setPickerError] = useState<string | null>(null)
  const [toast, setToast] = useState("")

  const run = async (viewing: ViewingScheduleDto, action: (token: string) => Promise<ViewingScheduleDto>, success: string) => {
    if (!accessToken || busyId !== null) return
    setBusyId(viewing.viewingId)
    try {
      replace(await action(accessToken))
      setToast(success)
      setPicker(null)
    } catch (actionError) {
      setToast(bookingErrorMessage(actionError, "Yêu cầu này vừa được xử lý, vui lòng tải lại trang."))
    } finally {
      setBusyId(null)
    }
  }

  const openPicker = (viewing: ViewingScheduleDto) => {
    setPicker(viewing)
    setPickerTime(hasUpcomingTime(viewing) ? toLocalInput(new Date(viewing.scheduledAt!)) : "")
    setPickerError(null)
  }

  const approveWithPickedTime = () => {
    if (!picker) return
    if (!pickerTime || Date.parse(pickerTime) <= Date.now()) {
      setPickerError("Vui lòng chọn thời điểm xem trong tương lai.")
      return
    }
    void run(picker, (token) => approveViewingSchedule(token, picker.viewingId, localInputToIso(pickerTime)),
      `Đã hẹn ${picker.customerName} xem phòng ${picker.roomCode}.`)
  }

  const reject = (viewing: ViewingScheduleDto) => {
    if (!window.confirm(`Từ chối lịch xem phòng ${viewing.roomCode} của ${viewing.customerName}?`)) return
    void run(viewing, (token) => rejectViewingSchedule(token, viewing.viewingId),
      `Đã từ chối lịch xem phòng của ${viewing.customerName}.`)
  }

  return (
    <ManagementPage title="Lịch xem phòng" description="Xem và xử lý các lịch hẹn xem phòng từ khách thuê.">
      {loading && <p role="status" className="text-[14px] text-slate-500">Đang tải lịch xem phòng...</p>}
      {!loading && error && (
        <div role="alert" className="rounded-2xl bg-white p-6 text-center shadow-[0_3px_18px_rgba(16,65,67,.05)]">
          <p className="text-[14px] text-slate-700">{error}</p>
          <button type="button" onClick={retry} className="mt-4 rounded-lg bg-[#087775] px-4 py-2 text-[13px] font-bold text-white">
            Thử lại
          </button>
        </div>
      )}
      {!loading && !error && (
        <ManagementDataTable headers={["Người đặt", "Số điện thoại", "Ngày xem", "Phòng", "Trạng thái", "Thao tác"]}>
          {viewings.length === 0 ? (
            <p className="px-5 py-8 text-center text-[13px] text-slate-500">Chưa có lịch xem phòng nào.</p>
          ) : (
            <div className="divide-y divide-slate-100">
              {viewings.map((viewing) => {
                const display = viewingStatusDisplay(viewing)
                const pending = viewing.status === "Pending"
                return (
                  <div key={viewing.viewingId} className="grid gap-3 px-5 py-5 lg:grid-cols-6 lg:items-center">
                    <span className="text-[13px] font-semibold text-slate-700">
                      {viewing.customerName}
                      <small className="block font-normal text-slate-400">#{viewing.viewingId}</small>
                    </span>
                    <span className="text-[13px] text-slate-600">{viewing.customerPhone || "—"}</span>
                    <span className="text-[13px] text-slate-600">{viewingTimeLabel(viewing)}</span>
                    <span className="text-[13px] text-slate-600">
                      {viewing.propertyName}
                      <small className="block text-slate-400">Phòng {viewing.roomCode}</small>
                    </span>
                    <StatusBadge tone={display.tone}>{display.label}</StatusBadge>
                    {pending && (
                      <div className="flex flex-wrap gap-2">
                        <button
                          type="button"
                          disabled={busyId !== null}
                          onClick={() => openPicker(viewing)}
                          className="rounded-lg bg-slate-100 px-3 py-2 text-[12px] font-bold text-slate-600 disabled:opacity-40"
                        >
                          Chọn lịch
                        </button>
                        <button
                          type="button"
                          disabled={busyId !== null}
                          onClick={() => reject(viewing)}
                          className="rounded-lg bg-rose-50 px-3 py-2 text-[12px] font-bold text-rose-600 disabled:opacity-40"
                        >
                          Từ chối
                        </button>
                        {hasUpcomingTime(viewing) && (
                          <button
                            type="button"
                            disabled={busyId !== null}
                            onClick={() => void run(viewing, (token) => approveViewingSchedule(token, viewing.viewingId, null),
                              `Đã chấp thuận lịch xem phòng của ${viewing.customerName}.`)}
                            className="rounded-lg bg-teal-50 px-3 py-2 text-[12px] font-bold text-[#087775] disabled:opacity-40"
                          >
                            Chấp thuận
                          </button>
                        )}
                      </div>
                    )}
                  </div>
                )
              })}
            </div>
          )}
        </ManagementDataTable>
      )}
      {picker && (
        <div className="fixed inset-0 z-50 grid place-items-center bg-slate-950/40 p-6">
          <div role="dialog" aria-modal="true" aria-labelledby="viewing-picker-title" className="w-full max-w-sm rounded-2xl bg-white p-6 shadow-2xl">
            <h2 id="viewing-picker-title" className="font-display text-xl font-bold text-slate-900">Chọn lịch xem phòng</h2>
            <p className="mt-2 text-sm text-slate-500">
              Hẹn {picker.customerName} xem phòng {picker.roomCode}. Lưu lịch sẽ chấp thuận yêu cầu và báo cho khách.
            </p>
            <input
              type="datetime-local"
              aria-label="Ngày giờ xem phòng"
              min={toLocalInput()}
              value={pickerTime}
              onChange={(event) => setPickerTime(event.target.value)}
              className="mt-5 h-11 w-full rounded-xl border border-slate-200 px-3 text-sm"
            />
            {pickerError && <p role="alert" className="mt-3 text-sm text-rose-600">{pickerError}</p>}
            <div className="mt-5 flex gap-2">
              <button type="button" onClick={() => setPicker(null)} className="flex-1 rounded-lg bg-slate-100 py-3 text-sm font-bold text-slate-600">
                Huỷ
              </button>
              <button
                type="button"
                disabled={busyId !== null}
                onClick={approveWithPickedTime}
                className="flex-1 rounded-lg bg-[#087775] py-3 text-sm font-bold text-white disabled:opacity-40"
              >
                Lưu lịch
              </button>
            </div>
          </div>
        </div>
      )}
      {toast && <Toast message={toast} onClose={() => setToast("")} />}
    </ManagementPage>
  )
}
