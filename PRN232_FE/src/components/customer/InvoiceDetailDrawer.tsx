import type { InvoiceDto } from "@/types/invoice"
import PortalDrawer from "@/components/customer/PortalDrawer"
import StatusBadge from "@/components/customer/StatusBadge"
import { formatContractDate, localToday } from "@/utils/managementContracts"
import { formatMonth, invoiceLineLabel, invoiceStatusDisplay } from "@/utils/invoiceDisplay"
import { formatVnd } from "@/utils/publicFormatting"

type InvoiceDetailDrawerProps = {
  invoice: InvoiceDto
  onClose: () => void
}

// Dùng chung cho trang hoá đơn của khách và của quản lý.
export default function InvoiceDetailDrawer({ invoice, onClose }: InvoiceDetailDrawerProps) {
  const display = invoiceStatusDisplay(invoice, localToday())

  return (
    <PortalDrawer
      title={`Hóa đơn tháng ${formatMonth(invoice.invoiceMonth)}`}
      eyebrow={`HĐ #${invoice.invoiceId} · ${invoice.propertyName} · Phòng ${invoice.roomCode}`}
      onClose={onClose}
    >
      <div className="flex items-center justify-between">
        <p className="text-[13px] text-slate-500">Hạn thanh toán {formatContractDate(invoice.dueDate)}</p>
        <StatusBadge tone={display.tone}>{display.label}</StatusBadge>
      </div>
      <div className="mt-5 divide-y divide-slate-100 rounded-xl bg-slate-50 px-5 text-[14px]">
        {invoice.lines.map((line, index) => (
          <div key={`${line.itemType}-${index}`} className="flex justify-between gap-4 py-3.5">
            <span className="text-slate-500">{invoiceLineLabel(line)}</span>
            <strong className="shrink-0 text-slate-700">{formatVnd(line.amount)}</strong>
          </div>
        ))}
        <div className="flex justify-between py-4">
          <strong className="text-slate-800">Tổng cộng</strong>
          <strong className="text-[18px] text-[#087775]">{formatVnd(invoice.total)}</strong>
        </div>
      </div>
    </PortalDrawer>
  )
}
