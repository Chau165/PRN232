import { useState } from "react"
import { useAuth } from "@/auth/AuthContext"
import type { PaymentMethod } from "@/api/bookingsApi"
import { payInvoice, type InvoiceDto } from "@/api/invoicesApi"
import PaymentMethodPicker from "@/components/customer/PaymentMethodPicker"
import { bookingErrorMessage } from "@/utils/bookingDisplay"
import { formatMonth } from "@/utils/invoiceDisplay"
import { formatVnd } from "@/utils/publicFormatting"

type PayInvoiceModalProps = {
  invoice: InvoiceDto
  onClose: () => void
  onPaid: (invoice: InvoiceDto) => void
}

// Số tiền hiển thị chỉ để khách đối chiếu; BE tự cộng lại từ các dòng hoá đơn khi trừ tiền.
export default function PayInvoiceModal({ invoice, onClose, onPaid }: PayInvoiceModalProps) {
  const { accessToken } = useAuth()
  const [method, setMethod] = useState<PaymentMethod | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)

  const pay = async () => {
    if (!accessToken || pending) return
    if (!method) {
      setError("Vui lòng chọn phương thức thanh toán.")
      return
    }
    setPending(true)
    setError(null)
    try {
      onPaid(await payInvoice(accessToken, invoice.invoiceId, method))
    } catch (payError) {
      setError(bookingErrorMessage(payError, "Hóa đơn này đã được thanh toán."))
      setPending(false)
    }
  }

  return (
    <div className="fixed inset-0 z-[70] grid place-items-center bg-slate-950/45 p-5">
      <div role="dialog" aria-modal="true" aria-labelledby="pay-invoice-title" className="w-full max-w-md space-y-5 rounded-2xl bg-white p-7 shadow-2xl">
        <div>
          <p className="text-[12px] font-bold uppercase tracking-[.12em] text-[#087775]">HĐ #{invoice.invoiceId} · Phòng {invoice.roomCode}</p>
          <h2 id="pay-invoice-title" className="mt-2 font-display text-[22px] font-bold text-slate-900">
            Thanh toán hóa đơn tháng {formatMonth(invoice.invoiceMonth)}
          </h2>
          <p className="mt-2 text-sm text-slate-500">Số tiền: <strong className="text-[#087775]">{formatVnd(invoice.total)}</strong></p>
        </div>
        <PaymentMethodPicker value={method} onChange={(value) => { setMethod(value); setError(null) }} />
        {error && <p role="alert" className="text-sm text-rose-600">{error}</p>}
        <div className="flex gap-3">
          <button type="button" onClick={onClose} className="flex-1 rounded-lg bg-slate-100 py-3 text-sm font-bold text-slate-600">Hủy</button>
          <button type="button" disabled={pending} onClick={pay} className="flex-1 rounded-lg bg-[#087775] py-3 text-sm font-bold text-white disabled:opacity-60">
            {pending ? "Đang thanh toán..." : `Thanh toán ${formatVnd(invoice.total)}`}
          </button>
        </div>
      </div>
    </div>
  )
}
