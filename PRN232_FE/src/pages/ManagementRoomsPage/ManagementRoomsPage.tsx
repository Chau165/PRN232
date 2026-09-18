import { useState } from "react"
import ManagementDataTable from "@/components/management/ManagementDataTable"
import ManagementPage from "@/components/management/ManagementPage"
import StatusBadge from "@/components/customer/StatusBadge"
import Toast from "@/components/customer/Toast"
import { getCurrentUser } from "@/utils/managementAuth"
import { getManagementScope } from "@/utils/managementScope"
import type { ManagedRoomStatus } from "@/types/management"

const tabs: Array<["Tất cả" | ManagedRoomStatus, string]> = [
  ["Tất cả", "Tất cả"],
  ["Trống", "Trống"],
  ["Đã đặt cọc", "Đã đặt cọc"],
  ["Đang thuê", "Đang thuê"],
  ["Đang sửa chữa", "Đang sửa chữa"],
]
function tone(status: ManagedRoomStatus) {
  return status === "Trống"
    ? "green"
    : status === "Đang sửa chữa"
      ? "rose"
      : status === "Đã đặt cọc"
        ? "amber"
        : "teal"
}

export default function ManagementRoomsPage() {
  const user = getCurrentUser()
  const { rooms, properties } = getManagementScope(user)
  const [tab, setTab] = useState<"Tất cả" | ManagedRoomStatus>("Tất cả")
  const [toast, setToast] = useState("")
  const filtered = rooms.filter(
    (room) => tab === "Tất cả" || room.status === tab,
  )
  return (
    <ManagementPage
      title="Phòng"
      description="Theo dõi trạng thái phòng và thông tin vận hành theo từng khu trọ."
    >
      <div className="mb-5 flex flex-wrap gap-2">
        {tabs.map(([value, label]) => (
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
      <ManagementDataTable
        headers={[
          "Phòng",
          "Khu trọ",
          "Giá",
          "Khách thuê",
          "Sức chứa",
          "Trạng thái",
          "Thao tác",
        ]}
      >
        <div className="divide-y divide-slate-100">
          {filtered.map((room) => (
            <div
              key={room.id}
              className="grid gap-3 px-5 py-5 lg:grid-cols-[.8fr_1.3fr_1fr_1fr_1fr_1fr_.8fr] lg:items-center"
            >
              <strong className="text-slate-800">Phòng {room.code}</strong>
              <span className="text-[13px] text-slate-600">
                {
                  properties.find((property) => property.id === room.propertyId)
                    ?.name
                }
              </span>
              <span className="text-[13px] font-semibold text-[#087775]">
                {room.price}
              </span>
              <span className="text-[13px] text-slate-600">
                {room.tenantName ?? "-"}
              </span>
              <span className="text-[13px] text-slate-600">
                {room.occupancy}
              </span>
              <StatusBadge tone={tone(room.status)}>
                {room.status.toUpperCase()}
              </StatusBadge>
              <button
                onClick={() => setToast(`Đã mở phòng ${room.code}`)}
                className="w-fit rounded-lg bg-slate-100 px-3 py-2 text-[12px] font-bold text-slate-600"
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
