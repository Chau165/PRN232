import { useEffect, useRef, useState } from "react"
import { createPortal } from "react-dom"
import type { MaintenanceLogDto, MaintenanceReportDto, MaintenanceRequestDto } from "@/types/maintenance"
import type { BackendUserDto } from "@/types/auth"
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
  report: MaintenanceReportDto | null
  reportLoading: boolean
  reportNotFound: boolean
  reportError: string
  onRetryReport: () => void
}

const labels: Record<MaintenanceRequestDto["status"], string> = {
  Pending: "Chờ xử lý",
  Approved: "Đã duyệt / Đã phân công",
  Completed: "Hoàn thành",
}

function formatVietnameseDate(value: string) {
  const timestamp = /(?:Z|[+-]\d{2}:?\d{2})$/i.test(value) ? value : `${value}Z`
  return new Intl.DateTimeFormat("vi-VN", {
    dateStyle: "medium",
    timeStyle: "short",
    timeZone: "Asia/Ho_Chi_Minh",
  }).format(new Date(timestamp))
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
  report,
  reportLoading,
  reportNotFound,
  reportError,
  onRetryReport,
}: Props) {
  const [fileError, setFileError] = useState("")
  const [selectedImageIndex, setSelectedImageIndex] = useState<number | null>(null)
  const [failedImages, setFailedImages] = useState<Record<number, boolean>>({})
  const lightboxCloseButton = useRef<HTMLButtonElement>(null)
  const returnFocusButton = useRef<HTMLButtonElement | null>(null)
  const images = report?.images ?? []

  useEffect(() => {
    setSelectedImageIndex(null)
    setFailedImages({})
  }, [report?.reportId])

  useEffect(() => {
    if (selectedImageIndex !== null) {
      lightboxCloseButton.current?.focus()
      return
    }
    returnFocusButton.current?.focus()
    returnFocusButton.current = null
  }, [selectedImageIndex])

  useEffect(() => {
    if (selectedImageIndex === null) return
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") setSelectedImageIndex(null)
      if (images.length > 1 && event.key === "ArrowRight") setSelectedImageIndex((index) => index === null ? null : (index + 1) % images.length)
      if (images.length > 1 && event.key === "ArrowLeft") setSelectedImageIndex((index) => index === null ? null : (index - 1 + images.length) % images.length)
    }
    window.addEventListener("keydown", onKeyDown)
    return () => window.removeEventListener("keydown", onKeyDown)
  }, [images.length, selectedImageIndex])

  const lightbox = selectedImageIndex !== null && images[selectedImageIndex] && createPortal(
    <div
      className="fixed inset-0 z-[1000] flex items-center justify-center bg-slate-950/90 p-4 sm:p-8"
      onMouseDown={(event) => { if (event.target === event.currentTarget) setSelectedImageIndex(null) }}
    >
      <section role="dialog" aria-modal="true" aria-label={`Ảnh hoàn thành sửa chữa ${selectedImageIndex + 1}`} onKeyDown={(event) => {
        if (event.key !== "Tab") return
        const buttons = Array.from(event.currentTarget.querySelectorAll<HTMLButtonElement>("button:not([disabled])"))
        if (event.shiftKey && document.activeElement === buttons[0]) {
          event.preventDefault()
          buttons.at(-1)?.focus()
        } else if (!event.shiftKey && document.activeElement === buttons.at(-1)) {
          event.preventDefault()
          buttons[0]?.focus()
        }
      }} className="relative flex h-full w-full max-w-6xl flex-col items-center justify-center">
        <button ref={lightboxCloseButton} type="button" aria-label="Đóng ảnh" onClick={() => setSelectedImageIndex(null)} className="absolute right-0 top-0 z-10 grid h-11 w-11 place-items-center rounded-full bg-white/15 text-3xl text-white hover:bg-white/25">×</button>
        {images.length > 1 && <button type="button" aria-label="Ảnh trước" onClick={() => setSelectedImageIndex((selectedImageIndex - 1 + images.length) % images.length)} className="absolute left-0 top-1/2 z-10 grid h-11 w-11 -translate-y-1/2 place-items-center rounded-full bg-white/15 text-2xl text-white hover:bg-white/25">‹</button>}
        {failedImages[images[selectedImageIndex].imageId]
          ? <p role="status" className="rounded-lg bg-white/10 px-4 py-3 text-sm text-white">Không thể tải ảnh</p>
          : <img src={images[selectedImageIndex].imageUrl} alt={`Ảnh hoàn thành sửa chữa ${selectedImageIndex + 1}`} className="max-h-[calc(100%-4rem)] max-w-full object-contain" onError={() => setFailedImages((current) => ({ ...current, [images[selectedImageIndex].imageId]: true }))} />}
        {images.length > 1 && <button type="button" aria-label="Ảnh tiếp theo" onClick={() => setSelectedImageIndex((selectedImageIndex + 1) % images.length)} className="absolute right-0 top-1/2 z-10 grid h-11 w-11 -translate-y-1/2 place-items-center rounded-full bg-white/15 text-2xl text-white hover:bg-white/25">›</button>}
        <p className="mt-3 text-sm text-white">{selectedImageIndex + 1} / {images.length}</p>
      </section>
    </div>,
    document.body,
  )

  return (
    <>
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
      {request.status === "Completed" && <section aria-label="Báo cáo sửa chữa và hình ảnh hoàn thành" className="mt-6 rounded-xl border border-teal-100 bg-teal-50/50 p-4">
        <h2 className="text-sm font-bold text-[#087775]">BÁO CÁO SỬA CHỮA &amp; HÌNH ẢNH HOÀN THÀNH</h2>
        {reportLoading && <div role="status" className="mt-4 space-y-3"><div className="h-4 w-2/3 animate-pulse rounded bg-slate-200" /><div className="h-16 animate-pulse rounded-lg bg-slate-100" /><div className="grid grid-cols-2 gap-3"><div className="aspect-video animate-pulse rounded-lg bg-slate-200" /><div className="aspect-video animate-pulse rounded-lg bg-slate-200" /></div></div>}
        {!reportLoading && reportNotFound && <p role="status" className="mt-3 text-sm text-slate-600">Chưa tìm thấy báo cáo hoàn thành.</p>}
        {!reportLoading && reportError && <div role="alert" className="mt-3 rounded-lg bg-rose-50 p-3 text-sm text-rose-700"><p>{reportError}</p><button type="button" onClick={onRetryReport} className="mt-2 font-semibold underline">Thử tải lại báo cáo</button></div>}
        {!reportLoading && !report && !reportNotFound && !reportError && <button type="button" onClick={onRetryReport} className="mt-3 text-sm font-semibold text-[#087775] underline">Tải báo cáo</button>}
        {!reportLoading && report && <>
          <div className="mt-4 grid gap-2 text-sm sm:grid-cols-2">
            <p><span className="text-slate-500">Manager thực hiện:</span> <strong className="text-slate-800">{report.reportedByName ?? "—"}</strong></p>
            <p><span className="text-slate-500">Hoàn thành:</span> <strong className="text-slate-800">{formatVietnameseDate(report.createdAt)}</strong></p>
          </div>
          <p className="mt-4 whitespace-pre-wrap text-sm leading-6 text-slate-700">{report.description}</p>
          <h3 className="mt-4 text-xs font-bold uppercase tracking-wide text-slate-600">Hình ảnh bằng chứng</h3>
          {images.length === 0 ? <p className="mt-2 text-sm text-slate-500">Báo cáo không có hình ảnh.</p> : <div className="mt-2 grid grid-cols-1 gap-3 sm:grid-cols-2">
            {images.map((image, index) => <button key={image.imageId} type="button" aria-label={`Phóng to ảnh ${index + 1}`} onClick={(event) => { returnFocusButton.current = event.currentTarget; setSelectedImageIndex(index) }} className="group relative overflow-hidden rounded-lg border border-slate-200 bg-slate-100 text-left focus:outline-none focus:ring-2 focus:ring-[#087775]">
              {failedImages[image.imageId] ? <span role="status" className="flex aspect-video items-center justify-center px-2 text-xs text-slate-500">Không thể tải ảnh</span> : <img src={image.imageUrl} alt={`Ảnh hoàn thành sửa chữa ${index + 1}`} loading="lazy" onError={() => setFailedImages((current) => ({ ...current, [image.imageId]: true }))} className="aspect-video w-full object-contain transition group-hover:opacity-90" />}
            </button>)}
          </div>}
        </>}
      </section>}
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
      {lightbox}
    </>
  )
}
