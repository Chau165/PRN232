import { useState } from "react"
import ManagementDataTable from "@/components/management/ManagementDataTable"
import ManagementPage from "@/components/management/ManagementPage"
import StatusBadge from "@/components/customer/StatusBadge"
import Toast from "@/components/customer/Toast"
import { users } from "@/data/management"

export default function ManagementUsersPage() {
  const [toast, setToast] = useState("")
  return (
    <ManagementPage
      title="Người dùng"
      description="Quản lý người dùng và role trong hệ thống TrọViệt."
    >
      <ManagementDataTable
        headers={["Người dùng", "Email", "Role", "Trạng thái", "Thao tác"]}
      >
        <div className="divide-y divide-slate-100">
          {users.map((user) => (
            <div
              key={user.id}
              className="grid gap-3 px-5 py-5 lg:grid-cols-[1.3fr_1.7fr_1fr_1fr_1fr] lg:items-center"
            >
              <div>
                <p className="font-semibold text-slate-800">{user.name}</p>
                <p className="mt-1 text-[12px] text-slate-400">{user.id}</p>
              </div>
              <span className="text-[13px] text-slate-600">{user.email}</span>
              <StatusBadge
                tone={
                  user.role === "ADMIN"
                    ? "rose"
                    : user.role === "MANAGER"
                      ? "teal"
                      : "slate"
                }
              >
                {user.role}
              </StatusBadge>
              <StatusBadge tone="green">
                {user.status.toUpperCase()}
              </StatusBadge>
              <div className="flex gap-2">
                <button
                  onClick={() => setToast("Đã mở form đổi role mock")}
                  className="rounded-lg bg-teal-50 px-3 py-2 text-[12px] font-bold text-[#087775]"
                >
                  Đổi role
                </button>
                <button
                  onClick={() => setToast("Đã mở chi tiết user mock")}
                  className="rounded-lg bg-slate-100 px-3 py-2 text-[12px] font-bold text-slate-600"
                >
                  Xem
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
