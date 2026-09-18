import { useState } from "react"
import MaintenanceManagementDrawer from "@/components/management/MaintenanceManagementDrawer"
import ManagementDataTable from "@/components/management/ManagementDataTable"
import ManagementPage from "@/components/management/ManagementPage"
import StatusBadge from "@/components/customer/StatusBadge"
import Toast from "@/components/customer/Toast"
import { getCurrentUser } from "@/utils/managementAuth"
import { getManagementScope } from "@/utils/managementScope"
import type { ManagedMaintenanceRequest } from "@/types/management"

const statuses: Array<["Tất cả" | ManagedMaintenanceRequest["status"], string]> =
  [
    ["Tất cả", "Tất cả"],
    ["ĐÃ GỬI", "Chờ xử lý"],
    ["ĐÃ TIẾP NHẬN", "Đã tiếp nhận"],
    ["ĐANG XỬ LÝ", "Đang xử lý"],
    ["HOÀN THÀNH", "Hoàn thành"],
  ]
export default function ManagementMaintenancePage() {
  const { maintenance, properties } = getManagementScope(getCurrentUser())
  const [tab, setTab] = useState<typeof statuses[number][0]>("Tất cả")
  const [selected, setSelected] = useState<ManagedMaintenanceRequest | null>(
    null,
  )
  const [requests, setRequests] = useState(maintenance)
  const [toast, setToast] = useState("")
  const filtered = requests.filter(
    (request) => tab === "Tất cả" || request.status === tab,
  )
  function updateStatus(
    id: string,
    status: ManagedMaintenanceRequest["status"],
  ) {
    setRequests((items) =>
      items.map((item) => (item.id === id ? { ...item, status } : item)),
    )
    setSelected((item) => (item ? { ...item, status } : null))
    setToast(`Đã cập nhật ${id} thành ${status}`)
  }
  return (
    <ManagementPage
      title="Bảo trì / Sửa chữa"
      description="Tiếp nhận, phân công và cập nhật tiến độ yêu cầu sửa chữa từ khách thuê."
    >
      <div className="mb-5 flex flex-wrap gap-2">
        {statuses.map(([value, label]) => (
          <button
            key={value}
            onClick={() => setTab(value)}
            className={`rounded-full px-3 py-1.5 text-[12px] font-semibold ${
              tab === value
                ? "bg-[#087775] text-white"
                : "bg-slate-100 text-slate-500"
            }`}
          >
            {label}
          </button>
        ))}
      </div>
      <div className="mb-5 grid gap-3 md:grid-cols-3">
        <select className="h-11 rounded-lg border border-slate-200 bg-white px-3 text-[13px] text-slate-600">
          <option>Tất cả khu trọ</option>
          {properties.map((property) => (
            <option key={property.id}>{property.name}</option>
          ))}
        </select>
        <select className="h-11 rounded-lg border border-slate-200 bg-white px-3 text-[13px] text-slate-600">
          <option>Tất cả mức độ</option>
          <option>Bình thường</option>
          <option>Quan trọng</option>
          <option>Khẩn cấp</option>
        </select>
        <select className="h-11 rounded-lg border border-slate-200 bg-white px-3 text-[13px] text-slate-600">
          <option>Tất cả phòng</option>
          {requests.map((request) => (
            <option key={request.id}>{request.roomCode}</option>
          ))}
        </select>
      </div>
      <ManagementDataTable
        headers={[
          "Yêu cầu",
          "Ưu tiên",
          "Khách thuê",
          "Khu trọ / Phòng",
          "Vấn đề",
          "Ngày tạo",
          "Trạng thái",
        ]}
      >
        <div className="divide-y divide-slate-100">
          {filtered.map((request) => (
            <button
              key={request.id}
              onClick={() => setSelected(request)}
              className="grid w-full gap-3 px-5 py-5 text-left hover:bg-slate-50 lg:grid-cols-[.9fr_.9fr_1fr_1.2fr_1.5fr_1fr_1fr] lg:items-center"
            >
              <strong className="text-[13px] text-[#087775]">
                {request.id}
              </strong>
              <StatusBadge
                tone={
                  request.priority === "Khẩn cấp"
                    ? "rose"
                    : request.priority === "Quan trọng"
                      ? "amber"
                      : "slate"
                }
              >
                {request.priority}
              </StatusBadge>
              <span className="text-[13px] text-slate-600">
                {request.customerName}
              </span>
              <span className="text-[13px] text-slate-600">
                {request.propertyName}
                <small className="block text-slate-400">
                  Phòng {request.roomCode}
                </small>
              </span>
              <span className="text-[13px] font-semibold text-slate-700">
                {request.problem}
                <small className="mt-1 block font-normal text-slate-400">
                  {request.equipmentName}
                </small>
              </span>
              <span className="text-[13px] text-slate-500">
                {request.createdDate}
              </span>
              <StatusBadge
                tone={request.status === "HOÀN THÀNH" ? "green" : "amber"}
              >
                {request.status}
              </StatusBadge>
            </button>
          ))}
        </div>
      </ManagementDataTable>
      {selected && (
        <MaintenanceManagementDrawer
          request={selected}
          onClose={() => setSelected(null)}
          onUpdateStatus={updateStatus}
        />
      )}
      {toast && <Toast message={toast} onClose={() => setToast("")} />}
    </ManagementPage>
  )
}
