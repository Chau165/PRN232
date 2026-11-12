import { useEffect } from "react"
import { Link, useSearchParams } from "react-router"
import { useAuth } from "@/auth/AuthContext"
import { routes } from "@/constants/routes"
import { useInvoicePaymentStatus } from "@/hooks/useInvoicePaymentStatus"
import { paymentResultText, positiveId, recalledInvoicePayment, rememberInvoicePayment, validPayOSCheckout } from "@/utils/invoicePayment"
import { formatVnd } from "@/utils/publicFormatting"

export default function InvoicePaymentResultPage() {
  const { accessToken, user } = useAuth()
  const [search] = useSearchParams()
  // payOS's id/orderCode/code/status/cancel query fields are never payment evidence or our internal request ID.
  const saved = user ? recalledInvoicePayment(user.id) : null
  const callbackMatches = !search.has("id") || (saved?.providerLinkId && search.get("id") === saved.providerLinkId)
  const requestId = search.has("requestId") ? positiveId(search.get("requestId"))
    : callbackMatches ? saved?.requestId ?? null : null
  const { data, error, checking, paused, refresh } = useInvoicePaymentStatus(accessToken, requestId)
  useEffect(() => {
    const url = validPayOSCheckout(data?.checkoutUrl ?? null)
    if (user && data) rememberInvoicePayment(user.id, data.paymentRequestId, data.invoiceId, url ? new URL(url).pathname.split("/")[2] : undefined)
  }, [user, data])
  const display = data ? paymentResultText(data) : null
  const checkout = data?.canOpenCheckout && !data.isExpired && data.status === "Pending" && data.invoiceStatus !== "Paid" &&
    data.expiresAtUtc && Date.parse(data.expiresAtUtc) > Date.now() && !error && !checking ? validPayOSCheckout(data.checkoutUrl) : null
  return (
    <main className="grid min-h-screen place-items-center bg-[#fcfdfd] px-5 py-12">
      <section aria-labelledby="payment-result-title" className="w-full max-w-lg rounded-3xl bg-white p-7 text-center shadow-[0_12px_42px_rgba(16,65,67,.10)] sm:p-10">
        <div aria-hidden="true" className={`mx-auto grid h-16 w-16 place-items-center rounded-full text-3xl ${display?.tone === "success" ? "bg-emerald-100 text-emerald-700" : "bg-teal-50 text-[#087775]"}`}>{display?.tone === "success" ? "✓" : "↻"}</div>
        <p className="mt-5 text-xs font-bold uppercase tracking-wider text-[#087775]">Thanh toán hóa đơn</p>
        <h1 id="payment-result-title" className="mt-2 font-display text-2xl font-bold text-slate-900">{!requestId ? "Chưa xác định được yêu cầu thanh toán" : display?.title ?? (error ? "Chưa xác nhận được thanh toán" : "Đang kiểm tra thanh toán")}</h1>
        <p role="status" aria-live="polite" className="mt-3 text-sm leading-6 text-slate-600">{!requestId
          ? "Vui lòng quay lại hóa đơn để kiểm tra. Nếu đã chuyển tiền, không thanh toán thêm khi chưa có xác nhận."
          : display?.detail ?? (error ? "Vui lòng kiểm tra lại hoặc quay về hóa đơn. Nếu đã trả tiền, không thanh toán thêm." : "Đang lấy kết quả đã xác nhận từ hệ thống. Vui lòng chờ.")}</p>
        {data && <p className="mt-4 text-sm text-slate-600">Hóa đơn #{data.invoiceId} · <strong>{formatVnd(data.amount)}</strong></p>}
        {error && <p role="alert" className="mt-4 text-sm text-rose-700">{error}</p>}
        {paused && <p className="mt-4 text-sm text-amber-800">Chưa có kết quả cuối cùng. Đã tạm dừng kiểm tra tự động; bạn có thể kiểm tra lại sau. Nếu đã trả tiền, vui lòng không chuyển thêm.</p>}
        <div className="mt-7 flex flex-col gap-3">
          {checkout && <a href={checkout} className="rounded-lg bg-[#087775] px-5 py-3 text-sm font-bold text-white">Mở checkout payOS</a>}
          {requestId && <button type="button" disabled={checking} onClick={refresh} className="rounded-lg border border-[#087775] px-5 py-3 text-sm font-bold text-[#087775] disabled:opacity-50">{checking ? "Đang kiểm tra..." : "Kiểm tra lại trạng thái"}</button>}
          <Link to={routes.myBills} className="rounded-lg bg-slate-100 px-5 py-3 text-sm font-bold text-slate-600">Về hóa đơn của tôi</Link>
        </div>
      </section>
    </main>
  )
}
