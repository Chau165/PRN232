import { useEffect, useRef, useState } from "react"
import { useNavigate } from "react-router"
import { useAuth } from "@/auth/AuthContext"
import { createInvoicePaymentLink } from "@/services/invoicePaymentService"
import type { InvoiceDto } from "@/types/invoice"
import { invoicePaymentError, openPayOSCheckout, rememberInvoicePayment, validPayOSCheckout } from "@/utils/invoicePayment"
import { routes } from "@/constants/routes"
import { formatMonth } from "@/utils/invoiceDisplay"
import { formatVnd } from "@/utils/publicFormatting"

type Props = { invoice: InvoiceDto; onClose: () => void }

export default function PayInvoiceModal({ invoice, onClose }: Props) {
  const { accessToken, user } = useAuth()
  const navigate = useNavigate()
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)
  const locked = useRef(false)
  const mounted = useRef(true)
  useEffect(() => { mounted.current = true; return () => { mounted.current = false } }, [])
  const pay = async () => {
    if (!accessToken || !user || locked.current) return
    locked.current = true
    setPending(true)
    setError(null)
    try {
      const result = await createInvoicePaymentLink(accessToken, invoice.invoiceId)
      if (!mounted.current) return
      if (result.invoiceId !== invoice.invoiceId || !Number.isSafeInteger(result.paymentRequestId) || result.paymentRequestId <= 0)
        throw new Error("Invalid checkout response")
      const url = validPayOSCheckout(result.checkoutUrl)
      rememberInvoicePayment(user.id, result.paymentRequestId, invoice.invoiceId, url ? new URL(url).pathname.split("/")[2] : undefined)
      if (result.status === "Pending" && url && result.expiresAtUtc && Date.parse(result.expiresAtUtc) > Date.now()) {
        openPayOSCheckout(url)
      } else {
        navigate(`${routes.paymentResult}?requestId=${result.paymentRequestId}`)
      }
    } catch (failure) {
      if (mounted.current) setError(invoicePaymentError(failure))
    } finally {
      locked.current = false
      if (mounted.current) setPending(false)
    }
  }
  return (
    <div className="fixed inset-0 z-[70] grid place-items-center bg-slate-950/45 p-5">
      <section role="dialog" aria-modal="true" aria-labelledby="pay-invoice-title" className="w-full max-w-md space-y-5 rounded-2xl bg-white p-7 shadow-2xl">
        <div>
          <p className="text-xs font-bold uppercase tracking-wider text-[#087775]">HĐ #{invoice.invoiceId} · Phòng {invoice.roomCode}</p>
          <h2 id="pay-invoice-title" className="mt-2 font-display text-[22px] font-bold text-slate-900">Thanh toán hóa đơn tháng {formatMonth(invoice.invoiceMonth)}</h2>
          <p className="mt-2 text-sm text-slate-500">Số tiền: <strong className="text-[#087775]">{formatVnd(invoice.total)}</strong></p>
        </div>
        <p className="rounded-xl bg-teal-50 p-4 text-sm leading-6 text-slate-700">Bạn sẽ được chuyển đến payOS để thanh toán. Hóa đơn chỉ được ghi nhận sau khi hệ thống xác nhận đã nhận tiền.</p>
        {error && <p role="alert" className="text-sm text-rose-600">{error}</p>}
        <div className="flex gap-3">
          <button type="button" disabled={pending} onClick={onClose} className="rounded-lg bg-slate-100 px-4 py-3 text-sm font-bold text-slate-600 disabled:opacity-50">Đóng</button>
          <button type="button" disabled={pending} onClick={pay} className="flex-1 rounded-lg bg-[#087775] px-3 py-3 text-sm font-bold text-white disabled:opacity-60">{pending ? "Đang tạo liên kết..." : "Thanh toán qua payOS"}</button>
        </div>
      </section>
    </div>
  )
}
