import { useState } from "react"
import ContractDetailDrawer from "@/components/customer/ContractDetailDrawer"
import CustomerPageShell from "@/components/customer/CustomerPageShell"
import StatusBadge from "@/components/customer/StatusBadge"
import Toast from "@/components/customer/Toast"
import { contracts } from "@/data/customer"
import type { Contract } from "@/types/customer"

export default function MyContractsPage() {
  const [selected, setSelected] = useState<Contract | null>(null)
  const [toast, setToast] = useState("")

  return (
    <CustomerPageShell
      title="Hợp đồng của tôi"
      description="Theo dõi các hợp đồng đang hiệu lực và lịch sử thuê phòng của bạn."
    >
      <div className="space-y-4">
        {contracts.map((contract) => (
          <article
            key={contract.id}
            className="rounded-2xl bg-white p-6 shadow-[0_3px_18px_rgba(16,65,67,.07)]"
          >
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <p className="text-[12px] font-bold uppercase tracking-[.12em] text-[#087775]">
                  {contract.id}
                </p>
                <h2 className="mt-2 font-display text-[22px] font-bold text-slate-900">
                  {contract.propertyName}
                </h2>
                <p className="mt-1 text-[14px] text-slate-500">
                  Phòng {contract.roomCode} · {contract.tenantName}
                </p>
              </div>
              <StatusBadge
                tone={contract.status === "Đang hiệu lực" ? "green" : "slate"}
              >
                {contract.status.toUpperCase()}
              </StatusBadge>
            </div>
            <div className="mt-6 grid gap-4 border-t border-slate-100 pt-5 text-[14px] sm:grid-cols-3">
              <div>
                <p className="text-slate-400">Bắt đầu</p>
                <p className="mt-1 font-semibold text-slate-700">
                  {contract.startDate}
                </p>
              </div>
              <div>
                <p className="text-slate-400">Kết thúc</p>
                <p className="mt-1 font-semibold text-slate-700">
                  {contract.endDate}
                </p>
              </div>
              <div>
                <p className="text-slate-400">Giá thuê</p>
                <p className="mt-1 font-bold text-[#087775]">
                  {contract.monthlyRent}
                </p>
              </div>
            </div>
            <div className="mt-5 flex flex-wrap gap-3">
              <button
                onClick={() => setSelected(contract)}
                className="rounded-lg bg-[#087775] px-4 py-3 text-[13px] font-bold text-white"
              >
                Xem chi tiết
              </button>
              {contract.status === "Đang hiệu lực" && (
                <button
                  onClick={() => setToast("Yêu cầu gia hạn đã được ghi nhận")}
                  className="rounded-lg bg-teal-50 px-4 py-3 text-[13px] font-bold text-[#087775]"
                >
                  Yêu cầu gia hạn
                </button>
              )}
            </div>
          </article>
        ))}
      </div>
      {selected && (
        <ContractDetailDrawer
          contract={selected}
          onClose={() => setSelected(null)}
        />
      )}
      {toast && <Toast message={toast} onClose={() => setToast("")} />}
    </CustomerPageShell>
  )
}
