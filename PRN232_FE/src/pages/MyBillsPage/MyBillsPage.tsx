import { useEffect, useState } from "react"
import CustomerPageShell from "@/components/customer/CustomerPageShell"
import InvoiceComplaintModal from "@/components/customer/InvoiceComplaintModal"
import InvoiceDetailDrawer from "@/components/customer/InvoiceDetailDrawer"
import PayInvoiceModal from "@/components/customer/PayInvoiceModal"
import StatusBadge from "@/components/customer/StatusBadge"
import Toast from "@/components/customer/Toast"
import { useAuth } from "@/auth/AuthContext"
import { getInvoices } from "@/services/invoiceService"
import { getInvoiceComplaints } from "@/services/invoiceComplaintService"
import type { InvoiceDto } from "@/types/invoice"
import type { InvoiceComplaintDto } from "@/types/invoiceComplaint"
import { complaintStatusDisplay, formatComplaintDate } from "@/utils/complaintDisplay"
import { formatMonth, invoiceStatusDisplay } from "@/utils/invoiceDisplay"
import { formatContractDate, localToday } from "@/utils/managementContracts"
import { formatVnd } from "@/utils/publicFormatting"

type BillTab = "unpaid" | "paid" | "all"

const tabs: Array<[BillTab, string]> = [
  ["unpaid", "Chưa thanh toán"],
  ["paid", "Đã thanh toán"],
  ["all", "Tất cả"],
]

export default function MyBillsPage() {
  const { accessToken } = useAuth()
  const today = localToday()
  const [tab, setTab] = useState<BillTab>("unpaid")
  const [invoices, setInvoices] = useState<InvoiceDto[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [reload, setReload] = useState(0)
  const [selected, setSelected] = useState<InvoiceDto | null>(null)
  const [paying, setPaying] = useState<InvoiceDto | null>(null)
  const [complaining, setComplaining] = useState<InvoiceDto | null>(null)
  const [complaints, setComplaints] = useState<InvoiceComplaintDto[]>([])
  const [toast, setToast] = useState("")

  useEffect(() => {
    if (!accessToken) return
    const controller = new AbortController()
    getInvoiceComplaints(accessToken, controller.signal)
      .then(setComplaints)
      // Danh sách khiếu nại chỉ là thông tin phụ: lỗi tải không được che mất danh sách hoá đơn.
      .catch(() => undefined)
    return () => controller.abort()
  }, [accessToken])

  useEffect(() => {
    if (!accessToken) return
    const controller = new AbortController()
    setLoading(true)
    setError(null)
    getInvoices(accessToken, controller.signal)
      .then(setInvoices)
      .catch((loadError: unknown) => {
        if (!controller.signal.aborted) {
          setError(loadError instanceof Error ? loadError.message : "Không thể tải hóa đơn.")
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })
    return () => controller.abort()
  }, [accessToken, reload])

  const filtered = invoices.filter((invoice) =>
    tab === "all" || (tab === "paid" ? invoice.status === "Paid" : invoice.status !== "Paid"))

  return (
    <CustomerPageShell
      title="Hóa đơn của tôi"
      description="Kiểm tra các khoản phí hàng tháng của phòng thuê."
    >
      <div className="flex flex-wrap gap-2 border-b border-slate-100">
        {tabs.map(([value, label]) => (
          <button
            key={value}
            onClick={() => setTab(value)}
            className={`border-b-2 px-3 pb-3 text-[14px] font-semibold ${
              tab === value
                ? "border-[#087775] text-[#087775]"
                : "border-transparent text-slate-400"
            }`}
          >
            {label}
          </button>
        ))}
      </div>
      <div className="mt-7 max-w-3xl space-y-4">
        {loading && <p role="status" className="text-sm text-slate-500">Đang tải hóa đơn...</p>}
        {!loading && error && (
          <div role="alert" className="rounded-2xl bg-white p-6 text-center shadow-[0_3px_18px_rgba(16,65,67,.07)]">
            <p className="text-sm text-slate-700">{error}</p>
            <button type="button" onClick={() => setReload((value) => value + 1)} className="mt-4 rounded-lg bg-[#087775] px-4 py-2 text-sm font-semibold text-white">
              Thử lại
            </button>
          </div>
        )}
        {!loading && !error && filtered.length === 0 && (
          <p className="rounded-2xl bg-white p-6 text-center text-sm text-slate-500 shadow-[0_3px_18px_rgba(16,65,67,.07)]">
            Không có hóa đơn nào.
          </p>
        )}
        {!loading && !error && filtered.map((invoice) => {
          const display = invoiceStatusDisplay(invoice, today)
          return (
            <article key={invoice.invoiceId} className="rounded-2xl bg-white p-6 shadow-[0_3px_18px_rgba(16,65,67,.07)]">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <p className="text-[12px] font-bold uppercase tracking-[.12em] text-[#087775]">HĐ #{invoice.invoiceId}</p>
                  <h2 className="mt-2 font-display text-[20px] font-bold text-slate-900">
                    Hóa đơn tháng {formatMonth(invoice.invoiceMonth)} · Phòng {invoice.roomCode}
                  </h2>
                  <p className="mt-1 text-[13px] text-slate-500">Hạn thanh toán: {formatContractDate(invoice.dueDate)}</p>
                </div>
                <StatusBadge tone={display.tone}>{display.label}</StatusBadge>
              </div>
              <div className="mt-5 flex items-end justify-between border-t border-slate-100 pt-5">
                <div>
                  <p className="text-[13px] text-slate-400">Tổng cộng</p>
                  <p className="mt-1 text-[22px] font-bold text-[#087775]">{formatVnd(invoice.total)}</p>
                </div>
                <div className="flex gap-2">
                  <button
                    onClick={() => setSelected(invoice)}
                    className="rounded-lg bg-slate-100 px-3 py-2.5 text-[12px] font-bold text-slate-600"
                  >
                    Xem chi tiết
                  </button>
                  <button
                    onClick={() => setComplaining(invoice)}
                    className="rounded-lg border border-[#087775] px-3 py-2.5 text-[12px] font-bold text-[#087775]"
                  >
                    Khiếu nại
                  </button>
                  {invoice.status !== "Paid" && (
                    <button
                      onClick={() => setPaying(invoice)}
                      className="rounded-lg bg-[#087775] px-3 py-2.5 text-[12px] font-bold text-white"
                    >
                      Thanh toán
                    </button>
                  )}
                </div>
              </div>
            </article>
          )
        })}
      </div>
      {complaints.length > 0 && (
        <section className="mt-10 max-w-3xl" aria-labelledby="my-complaints-title">
          <h2 id="my-complaints-title" className="mb-4 font-display text-[20px] font-bold text-slate-900">Khiếu nại đã gửi</h2>
          <div className="space-y-3">
            {complaints.map((complaint) => {
              const display = complaintStatusDisplay(complaint.status)
              return (
                <article key={complaint.complaintId} className="flex flex-wrap items-start justify-between gap-3 rounded-2xl bg-white p-5 shadow-[0_3px_18px_rgba(16,65,67,.07)]">
                  <div className="min-w-0">
                    <p className="text-[13px] font-semibold text-slate-800">
                      Hóa đơn tháng {formatMonth(complaint.invoiceMonth)} · {complaint.feeType}
                    </p>
                    <p className="mt-1 text-[13px] text-slate-500">{complaint.content}</p>
                    <p className="mt-1 text-[12px] text-slate-400">Gửi ngày {formatComplaintDate(complaint.createdAt)}</p>
                  </div>
                  <StatusBadge tone={display.tone}>{display.label}</StatusBadge>
                </article>
              )
            })}
          </div>
        </section>
      )}
      {selected && <InvoiceDetailDrawer invoice={selected} onClose={() => setSelected(null)} />}
      {complaining && (
        <InvoiceComplaintModal
          invoice={complaining}
          onClose={() => setComplaining(null)}
          onCreated={(complaint) => {
            setComplaints((items) => [complaint, ...items])
            setComplaining(null)
            setToast("Khiếu nại của bạn đã được gửi tới chủ trọ.")
          }}
        />
      )}
      {paying && (
        <PayInvoiceModal
          invoice={paying}
          onClose={() => setPaying(null)}
          onPaid={(paid) => {
            setInvoices((items) => items.map((item) => (item.invoiceId === paid.invoiceId ? paid : item)))
            setPaying(null)
            setToast(`Đã thanh toán hóa đơn tháng ${formatMonth(paid.invoiceMonth)}.`)
          }}
        />
      )}
      {toast && <Toast message={toast} onClose={() => setToast("")} />}
    </CustomerPageShell>
  )
}
