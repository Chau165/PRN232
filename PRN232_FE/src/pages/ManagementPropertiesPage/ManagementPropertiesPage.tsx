import { useState } from "react"
import ManagementDataTable from "@/components/management/ManagementDataTable"
import ManagementPage from "@/components/management/ManagementPage"
import StatusBadge from "@/components/customer/StatusBadge"
import Toast from "@/components/customer/Toast"
import { getCurrentUser, hasPermission } from "@/utils/managementAuth"
import { getManagementScope } from "@/utils/managementScope"

export default function ManagementPropertiesPage() {
  const user = getCurrentUser()
  const { properties } = getManagementScope(user)
  const [toast, setToast] = useState("")
  return (
    <ManagementPage
      title="Khu trọ"
      description={
        user.role === "ADMIN"
          ? "Quản lý toàn bộ khu trọ và phân công vận hành."
          : "Các khu trọ được Admin giao cho bạn quản lý."
      }
    >
      <div className="mb-5 flex flex-wrap justify-end gap-2">
        {hasPermission(user, "createProperty") && (
          <button
            onClick={() => setToast("Form tạo khu trọ mock sẽ được mở")}
            className="rounded-lg bg-[#087775] px-4 py-2.5 text-[13px] font-bold text-white"
          >
            + Tạo khu trọ
          </button>
        )}
      </div>
      <ManagementDataTable
        headers={[
          "Khu trọ",
          "Khu vực",
          "Phòng",
          "Quản lý",
          "Trạng thái",
          "Thao tác",
        ]}
      >
        <div className="divide-y divide-slate-100">
          {properties.map((property) => (
            <div
              key={property.id}
              className="grid gap-3 px-5 py-5 lg:grid-cols-[1.5fr_1fr_1fr_1fr_1fr_1.2fr] lg:items-center"
            >
              <div className="flex items-center gap-3">
                <img
                  src={property.image}
                  alt={property.name}
                  className="h-12 w-16 rounded-lg object-cover"
                />
                <div>
                  <p className="font-semibold text-slate-800">
                    {property.name}
                  </p>
                  <p className="mt-1 text-[12px] text-slate-500">
                    {property.address}
                  </p>
                </div>
              </div>
              <span className="text-[13px] text-slate-600">
                {property.area}
              </span>
              <span className="text-[13px] text-slate-600">
                {property.totalRooms} phòng{" "}
                <small className="block text-slate-400">
                  {property.availableRooms} trống
                </small>
              </span>
              <span className="text-[13px] text-slate-600">
                {property.managerName ?? "Chưa phân công"}
              </span>
              <StatusBadge tone="green">
                {property.status.toUpperCase()}
              </StatusBadge>
              <div className="flex gap-2">
                <button
                  onClick={() => setToast(`Đang mở ${property.name}`)}
                  className="rounded-lg bg-slate-100 px-3 py-2 text-[12px] font-bold text-slate-600"
                >
                  Xem
                </button>
                <button
                  onClick={() =>
                    setToast(
                      user.role === "ADMIN"
                        ? "Đã mở form chỉnh sửa mock"
                        : "Đã mở thông tin vận hành mock",
                    )
                  }
                  className="rounded-lg bg-teal-50 px-3 py-2 text-[12px] font-bold text-[#087775]"
                >
                  Sửa
                </button>
                {user.role === "ADMIN" && (
                  <button
                    onClick={() =>
                      setToast("Tính năng xóa mock chưa thực hiện")
                    }
                    className="rounded-lg bg-rose-50 px-3 py-2 text-[12px] font-bold text-rose-600"
                  >
                    Xóa
                  </button>
                )}
              </div>
            </div>
          ))}
        </div>
      </ManagementDataTable>
      {toast && <Toast message={toast} onClose={() => setToast("")} />}
    </ManagementPage>
  )
}
