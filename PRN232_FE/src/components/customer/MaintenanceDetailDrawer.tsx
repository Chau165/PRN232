import type { MaintenanceLogDto, MaintenanceRequestDto } from "@/api/maintenanceApi"
import PortalDrawer from "@/components/customer/PortalDrawer"
import StatusBadge from "@/components/customer/StatusBadge"

type Props = {
  request: MaintenanceRequestDto
  logs: MaintenanceLogDto[]
  loading: boolean
  error: string | null
  onRetry: () => void
  onClose: () => void
}

const statusLabels: Record<MaintenanceRequestDto["status"], string> = {
  Pending: "Đã gửi · Chờ duyệt",
  Approved: "Đã được duyệt · Đã phân công",
  Completed: "Hoàn thành",
}

export default function MaintenanceDetailDrawer({ request, logs, loading, error, onRetry, onClose }: Props) {
  const tone = request.status === "Completed" ? "green" : request.status === "Approved" ? "teal" : "amber"
  return (
    <PortalDrawer title={`Yêu cầu #${request.requestId}`} eyebrow={`Phòng ${request.roomCode ?? "—"}`} onClose={onClose}>
      <StatusBadge tone={tone}>{statusLabels[request.status]}</StatusBadge>
      <div className="mt-5 rounded-xl bg-slate-50 p-4 text-[14px]">
        <p><span className="text-slate-400">Thiết bị:</span> <strong>{request.equipmentName ?? "Báo hỏng chung"}</strong></p>
        <p className="mt-2"><span className="text-slate-400">Mã phòng:</span> <strong>{request.roomCode ?? "—"}</strong></p>
        <p className="mt-4 leading-6 text-slate-700">{request.description}</p>
        <p className="mt-4"><span className="text-slate-400">Người được phân công:</span> <strong>{request.assignedStaffName ?? "Chưa phân công"}</strong></p>
      </div>
      <div className="mt-8">
        <h3 className="font-display text-[19px] font-bold text-slate-900">Lịch sử trạng thái</h3>
        {loading ? <p role="status" className="mt-4 text-[13px] text-slate-500">Đang tải lịch sử...</p> : error ? (
          <div role="alert" className="mt-4 text-[13px] text-rose-600"><p>{error}</p><button onClick={onRetry} className="mt-2 font-semibold">Thử lại</button></div>
        ) : logs.length === 0 ? <p className="mt-4 text-[13px] text-slate-500">Chưa có lịch sử thay đổi.</p> : (
          <ol className="mt-5 space-y-4">
            {logs.map((log) => (
              <li key={log.logId} className="flex gap-3">
                <span className="mt-1 h-2.5 w-2.5 shrink-0 rounded-full bg-[#087775]" />
                <div>
                  <p className="text-[14px] font-semibold text-slate-700">{statusLabels[log.newStatus as MaintenanceRequestDto["status"]] ?? log.newStatus}</p>
                  <p className="mt-0.5 text-[12px] text-slate-400">{new Date(log.changedAt).toLocaleString("vi-VN")}{log.changedByName ? ` · ${log.changedByName}` : ""}</p>
                </div>
              </li>
            ))}
          </ol>
        )}
      </div>
    </PortalDrawer>
  )
}
