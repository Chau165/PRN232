import { useState } from "react"
import ManagementDataTable from "@/components/management/ManagementDataTable"
import ManagementDetailDrawer from "@/components/management/ManagementDetailDrawer"
import ManagementPage from "@/components/management/ManagementPage"
import StatusBadge from "@/components/customer/StatusBadge"
import Toast from "@/components/customer/Toast"
import { getCurrentUser } from "@/utils/managementAuth"
import { getManagementScope } from "@/utils/managementScope"
import type { ManagedContract } from "@/types/management"

export default function ManagementContractsPage() {
  const { contracts } = getManagementScope(getCurrentUser())
  const [selected, setSelected] = useState<ManagedContract | null>(null)
  const [toast, setToast] = useState("")
  return (
    <ManagementPage
      title="Hợp đồng"
      description="Quản lý hợp đồng hiện tại và theo dõi các hợp đồng sắp hết hạn."
    >
      <ManagementDataTable
        headers={[
          "Hợp đồng",
          "Khách thuê",
          "Khu trọ / Phòng",
          "Thời hạn",
          "Giá thuê",
          "Trạng thái",
          "Thao tác",
        ]}
      >
        <div className="divide-y divide-slate-100">
          {contracts.map((contract) => (
            <div
              key={contract.id}
              className="grid gap-3 px-5 py-5 lg:grid-cols-[1fr_1fr_1.2fr_1.3fr_1fr_1fr_1fr] lg:items-center"
            >
              <strong className="text-[13px] text-slate-700">
                {contract.id}
              </strong>
              <span className="text-[13px] text-slate-600">
                {contract.tenantName}
              </span>
              <span className="text-[13px] text-slate-600">
                {contract.propertyName}
                <small className="block text-slate-400">
                  Phòng {contract.roomCode}
                </small>
              </span>
              <span className="text-[13px] text-slate-600">
                {contract.startDate}
                <small className="block text-slate-400">
                  đến {contract.endDate}
                </small>
              </span>
              <span className="font-semibold text-[#087775]">
                {contract.monthlyRent}
              </span>
              <StatusBadge tone="green">
                {contract.status.toUpperCase()}
              </StatusBadge>
              <div className="flex gap-2">
                <button
                  onClick={() => setSelected(contract)}
                  className="rounded-lg bg-slate-100 px-3 py-2 text-[12px] font-bold text-slate-600"
                >
                  Xem
                </button>
                <button
                  onClick={() => setToast("Đã mở form gia hạn mock")}
                  className="rounded-lg bg-teal-50 px-3 py-2 text-[12px] font-bold text-[#087775]"
                >
                  Gia hạn
                </button>
              </div>
            </div>
          ))}
        </div>
      </ManagementDataTable>
      {selected && (
        <ManagementDetailDrawer
          title="Chi tiết hợp đồng"
          eyebrow={selected.id}
          onClose={() => setSelected(null)}
        >
          <div className="rounded-xl bg-slate-50 p-5 text-[14px]">
            <p>
              Khách thuê: <strong>{selected.tenantName}</strong>
            </p>
            <p className="mt-3">
              Khu trọ: <strong>{selected.propertyName}</strong>
            </p>
            <p className="mt-3">
              Phòng: <strong>{selected.roomCode}</strong>
            </p>
            <p className="mt-3">
              Thời hạn:{" "}
              <strong>
                {selected.startDate} - {selected.endDate}
              </strong>
            </p>
            <p className="mt-3">
              Giá thuê:{" "}
              <strong className="text-[#087775]">{selected.monthlyRent}</strong>
            </p>
            <p className="mt-3">
              Tiền cọc: <strong>{selected.deposit}</strong>
            </p>
          </div>
        </ManagementDetailDrawer>
      )}
      {toast && <Toast message={toast} onClose={() => setToast("")} />}
    </ManagementPage>
  )
}
