import type { Invoice } from "@/types/customer"
import PortalDrawer from "@/components/customer/PortalDrawer"
import StatusBadge from "@/components/customer/StatusBadge"

type InvoiceDetailDrawerProps = {
  invoice: Invoice
  onClose: () => void
  onPay?: () => void
}

export default function InvoiceDetailDrawer({
  invoice,
  onClose,
  onPay,
}: InvoiceDetailDrawerProps) {
  return (
    <PortalDrawer title={invoice.title} eyebrow={invoice.id} onClose={onClose}>
      <div className="flex items-center justify-between">
        <p className="text-[13px] text-slate-500">
          Hạn thanh toán {invoice.due}
        </p>
        <StatusBadge
          tone={invoice.status === "ĐÃ THANH TOÁN" ? "green" : "amber"}
        >
          {invoice.status}
        </StatusBadge>
      </div>
      <div className="mt-5 divide-y divide-slate-100 rounded-xl bg-slate-50 px-5 text-[14px]">
        {invoice.items.map((item) => (
          <div key={item.label} className="flex justify-between py-3.5">
            <span className="text-slate-500">{item.label}</span>
            <strong className="text-slate-700">{item.amount}</strong>
          </div>
        ))}
        <div className="flex justify-between py-4">
          <strong className="text-slate-800">Tổng cộng</strong>
          <strong className="text-[18px] text-[#087775]">
            {invoice.total}
          </strong>
        </div>
      </div>
      {invoice.status === "CHƯA THANH TOÁN" && onPay && (
        <button
          onClick={onPay}
          className="mt-6 w-full rounded-lg bg-[#087775] py-3 text-[13px] font-bold text-white shadow-sm hover:bg-[#066a68]"
        >
          Thanh toán
        </button>
      )}
    </PortalDrawer>
  )
}
