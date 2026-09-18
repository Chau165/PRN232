import { useState } from "react"
import ManagementDataTable from "@/components/management/ManagementDataTable"
import ManagementPage from "@/components/management/ManagementPage"
import StatusBadge from "@/components/customer/StatusBadge"
import Toast from "@/components/customer/Toast"
import { getCurrentUser } from "@/utils/managementAuth"
import { getManagementScope } from "@/utils/managementScope"

export default function ManagementEquipmentPage() {
  const { equipment } = getManagementScope(getCurrentUser())
  const [toast, setToast] = useState("")
  return (
    <ManagementPage
      title="Thiết bị"
      description="Theo dõi thiết bị theo phòng, thời hạn bảo hành và lịch sử sửa chữa."
    >
      <div className="mb-5 flex justify-end">
        <button
          onClick={() => setToast("Form thêm thiết bị mock sẽ được mở")}
          className="rounded-lg bg-[#087775] px-4 py-2.5 text-[13px] font-bold text-white"
        >
          + Thêm thiết bị
        </button>
      </div>
      <ManagementDataTable
        headers={[
          "Thiết bị",
          "Phòng / Khu trọ",
          "Lắp đặt",
          "Bảo hành đến",
          "Nhà cung cấp",
          "Trạng thái",
          "Thao tác",
        ]}
      >
        <div className="divide-y divide-slate-100">
          {equipment.map((item) => (
            <div
              key={item.id}
              className="grid gap-3 px-5 py-5 lg:grid-cols-[1.2fr_1.3fr_1fr_1fr_1.2fr_1fr_.7fr] lg:items-center"
            >
              <strong className="text-[13px] text-slate-700">
                {item.name}
              </strong>
              <span className="text-[13px] text-slate-600">
                Phòng {item.roomCode}
                <small className="block text-slate-400">
                  {item.propertyName}
                </small>
              </span>
              <span className="text-[13px] text-slate-600">
                {item.installedDate}
              </span>
              <span className="text-[13px] text-slate-600">
                {item.warrantyEnd}
              </span>
              <span className="text-[13px] text-slate-600">
                {item.supplier}
              </span>
              <StatusBadge
                tone={item.status === "Đang sử dụng" ? "green" : "amber"}
              >
                {item.status.toUpperCase()}
              </StatusBadge>
              <button
                onClick={() => setToast(`Đã mở ${item.name}`)}
                className="rounded-lg bg-slate-100 px-3 py-2 text-[12px] font-bold text-slate-600"
              >
                Xem
              </button>
            </div>
          ))}
        </div>
      </ManagementDataTable>
      {toast && <Toast message={toast} onClose={() => setToast("")} />}
    </ManagementPage>
  )
}
