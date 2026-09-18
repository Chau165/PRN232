import type { ManagedMaintenanceRequest } from "@/types/management"
import ManagementDetailDrawer from "@/components/management/ManagementDetailDrawer"
import StatusBadge from "@/components/customer/StatusBadge"

type MaintenanceManagementDrawerProps = {
  request: ManagedMaintenanceRequest
  onClose: () => void
  onUpdateStatus: (
    id: string,
    status: ManagedMaintenanceRequest["status"],
  ) => void
}

const statuses: ManagedMaintenanceRequest["status"][] = [
  "ĐÃ GỬI",
  "ĐÃ TIẾP NHẬN",
  "ĐANG XỬ LÝ",
  "HOÀN THÀNH",
]

export default function MaintenanceManagementDrawer({
  request,
  onClose,
  onUpdateStatus,
}: MaintenanceManagementDrawerProps) {
  return (
    <ManagementDetailDrawer
      title={request.problem}
      eyebrow={request.id}
      onClose={onClose}
    >
      <div className="flex flex-wrap gap-2">
        <StatusBadge tone={request.status === "HOÀN THÀNH" ? "green" : "amber"}>
          {request.status}
        </StatusBadge>
        <StatusBadge tone={request.priority === "Khẩn cấp" ? "rose" : "slate"}>
          {request.priority}
        </StatusBadge>
      </div>
      <div className="mt-5 grid gap-3 rounded-xl bg-slate-50 p-4 text-[14px] sm:grid-cols-2">
        <p>
          <span className="text-slate-400">Khách thuê:</span>{" "}
          <strong>{request.customerName}</strong>
        </p>
        <p>
          <span className="text-slate-400">Phòng:</span>{" "}
          <strong>{request.roomCode}</strong>
        </p>
        <p>
          <span className="text-slate-400">Khu trọ:</span>{" "}
          <strong>{request.propertyName}</strong>
        </p>
        <p>
          <span className="text-slate-400">Thiết bị:</span>{" "}
          <strong>{request.equipmentName}</strong>
        </p>
        <p>
          <span className="text-slate-400">Ngày tạo:</span>{" "}
          <strong>{request.createdDate}</strong>
        </p>
        <p>
          <span className="text-slate-400">Người phụ trách:</span>{" "}
          <strong>{request.assignedTo}</strong>
        </p>
      </div>
      <p className="mt-6 text-[14px] leading-7 text-slate-600">
        {request.description}
      </p>
      <div className="mt-6 rounded-xl border border-slate-100 p-4 text-[13px]">
        <p className="text-slate-400">Bảo hành</p>
        <p className="mt-1 font-semibold text-slate-700">
          {request.warrantyStatus}
        </p>
        <p className="mt-4 text-slate-400">Chi phí sửa chữa</p>
        <p className="mt-1 font-semibold text-slate-700">
          {request.repairCost}
        </p>
        <p className="mt-4 text-slate-400">Ghi chú nội bộ</p>
        <p className="mt-1 text-slate-600">{request.internalNotes}</p>
      </div>
      <div className="mt-7">
        <p className="text-[13px] font-semibold text-slate-700">
          Cập nhật trạng thái
        </p>
        <div className="mt-2 grid grid-cols-2 gap-2">
          {statuses.map((status) => (
            <button
              key={status}
              onClick={() => onUpdateStatus(request.id, status)}
              className={`rounded-lg px-2 py-2.5 text-[11px] font-bold ${
                request.status === status
                  ? "bg-[#087775] text-white"
                  : "bg-slate-100 text-slate-500 hover:bg-teal-50 hover:text-[#087775]"
              }`}
            >
              {status}
            </button>
          ))}
        </div>
      </div>
    </ManagementDetailDrawer>
  )
}
