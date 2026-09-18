import { useState } from "react"
import ManagementDataTable from "@/components/management/ManagementDataTable"
import ManagementDetailDrawer from "@/components/management/ManagementDetailDrawer"
import ManagementPage from "@/components/management/ManagementPage"
import StatusBadge from "@/components/customer/StatusBadge"
import { getCurrentUser } from "@/utils/managementAuth"
import { getManagementScope } from "@/utils/managementScope"
import type { Tenant } from "@/types/management"

export default function ManagementTenantsPage() {
  const { tenants } = getManagementScope(getCurrentUser())
  const [selected, setSelected] = useState<Tenant | null>(null)
  return (
    <ManagementPage
      title="Khách thuê"
      description="Thông tin khách thuê được giới hạn theo phạm vi khu trọ được phân công."
    >
      <ManagementDataTable
        headers={[
          "Khách thuê",
          "Liên hệ",
          "Khu trọ / Phòng",
          "Hợp đồng",
          "Thanh toán",
          "Thao tác",
        ]}
      >
        <div className="divide-y divide-slate-100">
          {tenants.map((tenant) => (
            <div
              key={tenant.id}
              className="grid gap-3 px-5 py-5 lg:grid-cols-[1.2fr_1.3fr_1.2fr_1fr_1fr_.7fr] lg:items-center"
            >
              <div>
                <p className="font-semibold text-slate-800">{tenant.name}</p>
                <p className="mt-1 text-[12px] text-slate-400">{tenant.id}</p>
              </div>
              <div className="text-[13px] text-slate-600">
                <p>{tenant.phone}</p>
                <p className="mt-1 text-slate-400">{tenant.email}</p>
              </div>
              <span className="text-[13px] text-slate-600">
                {tenant.propertyName}
                <small className="block text-slate-400">
                  Phòng {tenant.roomCode}
                </small>
              </span>
              <span className="text-[13px] text-slate-600">
                {tenant.contractId}
              </span>
              <StatusBadge
                tone={tenant.paymentStatus === "Đúng hạn" ? "green" : "amber"}
              >
                {tenant.paymentStatus.toUpperCase()}
              </StatusBadge>
              <button
                onClick={() => setSelected(tenant)}
                className="w-fit rounded-lg bg-slate-100 px-3 py-2 text-[12px] font-bold text-slate-600"
              >
                Xem
              </button>
            </div>
          ))}
        </div>
      </ManagementDataTable>
      {selected && (
        <ManagementDetailDrawer
          title={selected.name}
          eyebrow={selected.email}
          onClose={() => setSelected(null)}
        >
          <div className="rounded-xl bg-slate-50 p-4 text-[14px]">
            <p>
              <span className="text-slate-400">Điện thoại:</span>{" "}
              <strong>{selected.phone}</strong>
            </p>
            <p className="mt-2">
              <span className="text-slate-400">Khu trọ:</span>{" "}
              <strong>{selected.propertyName}</strong>
            </p>
            <p className="mt-2">
              <span className="text-slate-400">Phòng:</span>{" "}
              <strong>{selected.roomCode}</strong>
            </p>
            <p className="mt-2">
              <span className="text-slate-400">Hợp đồng:</span>{" "}
              <strong>{selected.contractId}</strong>
            </p>
          </div>
          <h3 className="mt-7 font-display text-[19px] font-bold text-slate-900">
            Lịch sử thanh toán
          </h3>
          <p className="mt-2 text-[13px] text-slate-500">
            Đúng hạn tháng 08/2026 · 4.380.000đ
          </p>
          <h3 className="mt-7 font-display text-[19px] font-bold text-slate-900">
            Lịch sử bảo trì
          </h3>
          <p className="mt-2 text-[13px] text-slate-500">
            #MR00123 · Máy lạnh không hoạt động · Đang xử lý
          </p>
        </ManagementDetailDrawer>
      )}
    </ManagementPage>
  )
}
