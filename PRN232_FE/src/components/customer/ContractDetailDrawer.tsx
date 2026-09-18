import type { Contract } from "@/types/customer"
import PortalDrawer from "@/components/customer/PortalDrawer"
import StatusBadge from "@/components/customer/StatusBadge"

type ContractDetailDrawerProps = {
  contract: Contract
  onClose: () => void
}

export default function ContractDetailDrawer({
  contract,
  onClose,
}: ContractDetailDrawerProps) {
  return (
    <PortalDrawer
      title="Chi tiết hợp đồng"
      eyebrow={contract.id}
      onClose={onClose}
    >
      <div className="flex items-center justify-between rounded-xl bg-[#f1f8f7] p-4">
        <div>
          <p className="font-semibold text-slate-800">
            {contract.propertyName}
          </p>
          <p className="mt-1 text-[13px] text-slate-500">
            Phòng {contract.roomCode}
          </p>
        </div>
        <StatusBadge
          tone={contract.status === "Đang hiệu lực" ? "green" : "slate"}
        >
          {contract.status.toUpperCase()}
        </StatusBadge>
      </div>
      <dl className="mt-6 divide-y divide-slate-100 rounded-xl bg-slate-50 px-5 text-[14px]">
        <div className="flex justify-between py-4">
          <dt className="text-slate-500">Người thuê</dt>
          <dd className="font-semibold text-slate-700">
            {contract.tenantName}
          </dd>
        </div>
        <div className="flex justify-between py-4">
          <dt className="text-slate-500">Thời hạn</dt>
          <dd className="font-semibold text-slate-700">
            {contract.startDate} - {contract.endDate}
          </dd>
        </div>
        <div className="flex justify-between py-4">
          <dt className="text-slate-500">Giá thuê</dt>
          <dd className="font-semibold text-[#087775]">
            {contract.monthlyRent} / tháng
          </dd>
        </div>
        <div className="flex justify-between py-4">
          <dt className="text-slate-500">Tiền cọc</dt>
          <dd className="font-semibold text-slate-700">{contract.deposit}</dd>
        </div>
      </dl>
      <button
        onClick={onClose}
        className="mt-7 w-full rounded-lg bg-teal-50 py-3 text-[13px] font-bold text-[#087775]"
      >
        Đóng
      </button>
    </PortalDrawer>
  )
}
