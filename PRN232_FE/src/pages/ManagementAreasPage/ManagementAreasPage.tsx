import { useState } from "react"
import ManagementDataTable from "@/components/management/ManagementDataTable"
import ManagementPage from "@/components/management/ManagementPage"
import StatusBadge from "@/components/customer/StatusBadge"
import Toast from "@/components/customer/Toast"
import { getCurrentUser } from "@/utils/managementAuth"
import { getManagementScope } from "@/utils/managementScope"

export default function ManagementAreasPage() {
  const { areas, properties } = getManagementScope(getCurrentUser())
  const [toast, setToast] = useState("")
  return (
    <ManagementPage
      title="Khu vực"
      description="Tổ chức các khu trọ theo khu vực và theo dõi phạm vi vận hành."
    >
      <div className="mb-5 flex justify-end">
        <button
          onClick={() => setToast("Form tạo khu vực mock sẽ được mở")}
          className="rounded-lg bg-[#087775] px-4 py-2.5 text-[13px] font-bold text-white"
        >
          + Tạo khu vực
        </button>
      </div>
      <ManagementDataTable
        headers={[
          "Khu vực",
          "Khu trọ",
          "Số phòng",
          "Manager",
          "Trạng thái",
          "Thao tác",
        ]}
      >
        <div className="divide-y divide-slate-100">
          {areas.map((area) => (
            <div
              key={area.id}
              className="grid gap-3 px-5 py-5 lg:grid-cols-[1fr_1.5fr_1fr_1.3fr_1fr_1fr] lg:items-center"
            >
              <strong className="text-slate-800">{area.name}</strong>
              <span className="text-[13px] text-slate-600">
                {area.propertyIds
                  .map(
                    (id) =>
                      properties.find((property) => property.id === id)?.name,
                  )
                  .join(", ")}
              </span>
              <span className="text-[13px] text-slate-600">
                {area.propertyIds.reduce(
                  (sum, id) =>
                    sum +
                    (properties.find((property) => property.id === id)
                      ?.totalRooms ?? 0),
                  0,
                )}{" "}
                phòng
              </span>
              <span className="text-[13px] text-slate-600">
                {area.managerNames.join(", ")}
              </span>
              <StatusBadge tone="green">
                {area.status.toUpperCase()}
              </StatusBadge>
              <div className="flex gap-2">
                <button
                  onClick={() => setToast(`Đã mở khu vực ${area.name}`)}
                  className="rounded-lg bg-slate-100 px-3 py-2 text-[12px] font-bold text-slate-600"
                >
                  Xem
                </button>
                <button
                  onClick={() => setToast("Đã mở form chỉnh sửa mock")}
                  className="rounded-lg bg-teal-50 px-3 py-2 text-[12px] font-bold text-[#087775]"
                >
                  Sửa
                </button>
              </div>
            </div>
          ))}
        </div>
      </ManagementDataTable>
      {toast && <Toast message={toast} onClose={() => setToast("")} />}
    </ManagementPage>
  )
}
