import { useState } from "react"
import type { MaintenanceLogDto } from "@/api/maintenanceApi"
import type { BackendUserDto } from "@/types/auth"
import type { MaintenanceRequestDto } from "@/api/maintenanceApi"
import ManagementDetailDrawer from "@/components/management/ManagementDetailDrawer"
import StatusBadge from "@/components/customer/StatusBadge"

type Request = MaintenanceRequestDto & { logs: MaintenanceLogDto[] }
type Props = {
  request: Request
  propertyName: string | null
  detailLoading: boolean
  onClose: () => void
  managers: BackendUserDto[]
  managerRoleLoading: boolean
  canAssign: boolean
  canComplete: boolean
  submitting: boolean
  actionError: string
  assignmentNote: string
  onAssignmentNoteChange: (value: string) => void
  onManagerChange: (value: string) => void
  onAssign: () => void
  reportDescription: string
  reportImages: File[]
  onReportDescriptionChange: (value: string) => void
  onReportImagesChange: (files: File[]) => void
  onComplete: () => void
}

const labels: Record<MaintenanceRequestDto["status"], string> = {
  Pending: "Chờ xử lý",
  Approved: "Đã duyệt / Đã phân công",
  Completed: "Hoàn thành",
}

export default function MaintenanceManagementDrawer({
  request,
  propertyName,
  detailLoading,
  onClose,
  managers,
  managerRoleLoading,
  canAssign,
  canComplete,
  submitting,
  actionError,
  assignmentNote,
  onAssignmentNoteChange,
  onManagerChange,
  onAssign,
  reportDescription,
  reportImages,
  onReportDescriptionChange,
  onReportImagesChange,
  onComplete,
}: Props) {
  const [fileError, setFileError] = useState("")
  return (
    <ManagementDetailDrawer title={`Yêu cầu #${request.requestId}`} eyebrow="BẢO TRÌ / SỬA CHỮA" onClose={onClose}>
      <div className="flex flex-wrap gap-2"><StatusBadge tone={request.status === "Completed" ? "green" : request.status === "Pending" ? "amber" : "slate"}>{labels[request.status]}</StatusBadge></div>
      <section aria-label="Thông tin yêu cầu" className="mt-5 grid gap-3 rounded-xl bg-slate-50 p-4 text-[14px] sm:grid-cols-2">
        <p><span className="text-slate-400">Khách thuê:</span> <strong>{request.customerName ?? "—"}</strong></p>
        <p><span className="text-slate-400">Phòng:</span> <strong>{request.roomCode ?? `#${request.roomId}`}</strong></p>
        <p><span className="text-slate-400">Khu trọ:</span> <strong>{propertyName ?? "—"}</strong></p>
        <p><span className="text-slate-400">Thiết bị:</span> <strong>{request.equipmentName ?? "Không xác định"}</strong></p>
        <p><span className="text-slate-400">Tình trạng thiết bị:</span> <strong>{request.equipmentStatus ?? "—"}</strong></p>
        <p><span className="text-slate-400">Người phụ trách:</span> <strong>{request.assignedStaffName ?? "Chưa phân công"}</strong></p>
      </section>
      {detailLoading && <p role="status" className="mt-3 text-xs text-slate-500">Đang tải thông tin mới nhất...</p>}
      <section className="mt-5"><h2 className="text-sm font-bold text-slate-800">Mô tả sự cố</h2><p className="mt-2 whitespace-pre-wrap text-sm leading-6 text-slate-600">{request.description}</p></section>
      {request.assignmentNote && <section className="mt-5 rounded-xl border border-amber-200 bg-amber-50 p-4"><h2 className="text-sm font-bold text-amber-900">Ghi chú phân công (nội bộ)</h2><p className="mt-2 whitespace-pre-wrap text-sm leading-6 text-amber-900">{request.assignmentNote}</p></section>}
      {request.status === "Pending" && canAssign && (
        <section className="mt-6 border-t border-slate-100 pt-5">
          <h2 className="text-sm font-bold text-slate-800">Phân công Manager</h2>
          <label htmlFor="maintenance-manager" className="mt-3 block text-xs font-semibold text-slate-600">Manager phụ trách</label>
          <select id="maintenance-manager" onChange={(event) => onManagerChange(event.target.value)} defaultValue="" disabled={submitting || managerRoleLoading} className="mt-1 h-11 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"><option value="">{managerRoleLoading ? "Đang tải Manager..." : "Chọn Manager đang hoạt động"}</option>{managers.filter((manager) => manager.isActive).map((manager) => <option key={manager.userId} value={manager.userId}>{manager.fullName}</option>)}</select>
          <label htmlFor="maintenance-assignment-note" className="mt-4 block text-xs font-semibold text-slate-600">Ghi chú phân công</label>
          <textarea id="maintenance-assignment-note" value={assignmentNote} onChange={(event) => onAssignmentNoteChange(event.target.value)} maxLength={1000} rows={3} disabled={submitting} className="mt-1 w-full rounded-lg border border-slate-200 px-3 py-2 text-sm" placeholder="Hướng dẫn công việc cho Manager" />
          <p className="mt-1 text-right text-[11px] text-slate-400">{assignmentNote.length}/1.000</p>
          <button onClick={onAssign} disabled={submitting || managerRoleLoading || managers.every((manager) => !manager.isActive)} className="mt-3 w-full rounded-lg bg-[#087775] px-4 py-3 text-sm font-bold text-white disabled:opacity-50">{submitting ? "Đang phân công..." : "Phân công Manager"}</button>
        </section>
      )}
      {request.status === "Approved" && canComplete && (
        <section className="mt-6 border-t border-slate-100 pt-5">
          <h2 className="text-sm font-bold text-slate-800">Báo cáo hoàn thành</h2>
          <p className="mt-2 text-xs text-slate-500">Chỉ Manager được phân công mới có thể gửi báo cáo. Tải 1–5 ảnh JPEG, PNG hoặc WebP, tối đa 5 MB mỗi ảnh.</p>
          <label htmlFor="maintenance-report-description" className="mt-4 block text-xs font-semibold text-slate-600">Mô tả công việc</label>
          <textarea id="maintenance-report-description" value={reportDescription} onChange={(event) => onReportDescriptionChange(event.target.value)} maxLength={4000} rows={4} disabled={submitting} className="mt-1 w-full rounded-lg border border-slate-200 px-3 py-2 text-sm" />
          <label htmlFor="maintenance-report-images" className="mt-4 block text-xs font-semibold text-slate-600">Ảnh hoàn thành</label>
          <input id="maintenance-report-images" type="file" accept="image/jpeg,image/png,image/webp" multiple disabled={submitting} onChange={(event) => {
            const files = Array.from(event.target.files ?? [])
            const allowed = new Set(["image/jpeg", "image/png", "image/webp"])
            if (files.length < 1 || files.length > 5 || files.some((file) => !allowed.has(file.type) || file.size === 0 || file.size > 5 * 1024 * 1024)) {
              setFileError("Chọn từ 1 đến 5 ảnh JPEG, PNG hoặc WebP; mỗi ảnh tối đa 5 MB.")
              onReportImagesChange([])
              return
            }
            setFileError("")
            onReportImagesChange(files)
          }} className="mt-1 block w-full text-sm text-slate-600 file:mr-3 file:rounded-lg file:border-0 file:bg-teal-50 file:px-3 file:py-2 file:text-xs file:font-bold file:text-[#087775]" />
          {fileError && <p role="alert" className="mt-2 text-xs text-rose-600">{fileError}</p>}
          {reportImages.length > 0 && <p className="mt-2 text-xs text-slate-500">Đã chọn {reportImages.length} ảnh.</p>}
          <button onClick={onComplete} disabled={submitting} className="mt-4 w-full rounded-lg bg-[#087775] px-4 py-3 text-sm font-bold text-white disabled:opacity-50">{submitting ? "Đang gửi báo cáo..." : "Gửi báo cáo hoàn thành"}</button>
        </section>
      )}
      {request.status === "Completed" && <p className="mt-6 rounded-lg bg-emerald-50 p-3 text-sm font-semibold text-emerald-800">Yêu cầu đã hoàn thành.</p>}
      {actionError && <p role="alert" className="mt-4 rounded-lg bg-rose-50 p-3 text-sm text-rose-700">{actionError}</p>}
      <section className="mt-7 border-t border-slate-100 pt-5"><h2 className="text-sm font-bold text-slate-800">Lịch sử yêu cầu</h2>{request.logs.length === 0 ? <p className="mt-3 text-xs text-slate-500">Chưa có lịch sử trạng thái.</p> : <ol className="mt-3 space-y-3">{request.logs.map((log) => <li key={log.logId} className="border-l-2 border-teal-200 pl-3"><p className="text-xs font-semibold text-slate-700">{log.oldStatus ? `${labels[log.oldStatus as MaintenanceRequestDto["status"]] ?? log.oldStatus} → ` : ""}{labels[log.newStatus as MaintenanceRequestDto["status"]] ?? log.newStatus}</p><p className="mt-1 text-[11px] text-slate-500">{log.changedByName ?? "Người dùng"} · {new Date(log.changedAt).toLocaleString()}</p></li>)}</ol>}</section>
    </ManagementDetailDrawer>
  )
}
