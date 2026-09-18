import { useState } from "react"
import AssignManagerModal from "@/components/management/AssignManagerModal"
import ManagementDataTable from "@/components/management/ManagementDataTable"
import ManagementPage from "@/components/management/ManagementPage"
import StatusBadge from "@/components/customer/StatusBadge"
import Toast from "@/components/customer/Toast"
import { managers, managementProperties } from "@/data/management"

export default function ManagementManagersPage() {
  const [selected, setSelected] = useState<string | null>(null)
  const [toast, setToast] = useState("")
  return (
    <ManagementPage
      title="Phân công Manager"
      description="Giao một hoặc nhiều khu trọ cho Manager vận hành."
    >
      <div className="mb-5 flex justify-end">
        <button
          onClick={() => setToast("Form tạo Manager mock sẽ được mở")}
          className="rounded-lg bg-[#087775] px-4 py-2.5 text-[13px] font-bold text-white"
        >
          + Thêm Manager
        </button>
      </div>
      <ManagementDataTable
        headers={[
          "Manager",
          "Liên hệ",
          "Khu trọ phụ trách",
          "Trạng thái",
          "Thao tác",
        ]}
      >
        <div className="divide-y divide-slate-100">
          {managers.map((manager) => (
            <div
              key={manager.id}
              className="grid gap-3 px-5 py-5 lg:grid-cols-[1.2fr_1.5fr_2fr_1fr_1fr] lg:items-center"
            >
              <div>
                <p className="font-semibold text-slate-800">{manager.name}</p>
                <p className="mt-1 text-[12px] text-slate-400">{manager.id}</p>
              </div>
              <div className="text-[13px] text-slate-600">
                <p>{manager.email}</p>
                <p className="mt-1 text-slate-400">{manager.phone}</p>
              </div>
              <div className="flex flex-wrap gap-1.5">
                {manager.assignedPropertyIds.map((id) => (
                  <span
                    key={id}
                    className="rounded-full bg-teal-50 px-2.5 py-1 text-[11px] font-semibold text-[#087775]"
                  >
                    {
                      managementProperties.find(
                        (property) => property.id === id,
                      )?.name
                    }
                  </span>
                ))}
              </div>
              <StatusBadge tone="green">
                {manager.status.toUpperCase()}
              </StatusBadge>
              <div className="flex gap-2">
                <button
                  onClick={() => setSelected(manager.id)}
                  className="rounded-lg bg-[#087775] px-3 py-2 text-[12px] font-bold text-white"
                >
                  Phân công
                </button>
                <button
                  onClick={() => setToast("Đã mở thông tin Manager mock")}
                  className="rounded-lg bg-slate-100 px-3 py-2 text-[12px] font-bold text-slate-600"
                >
                  Xem
                </button>
              </div>
            </div>
          ))}
        </div>
      </ManagementDataTable>
      {selected && (
        <AssignManagerModal
          managerId={selected}
          onClose={() => setSelected(null)}
          onSave={() => {
            setSelected(null)
            setToast("Đã lưu phân công mock")
          }}
        />
      )}
      {toast && <Toast message={toast} onClose={() => setToast("")} />}
    </ManagementPage>
  )
}
