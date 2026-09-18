import { useState } from "react"
import CustomerPageShell from "@/components/customer/CustomerPageShell"
import InvoiceDetailDrawer from "@/components/customer/InvoiceDetailDrawer"
import StatusBadge from "@/components/customer/StatusBadge"
import Toast from "@/components/customer/Toast"
import { invoices, payments } from "@/data/customer"
import type { Invoice } from "@/types/customer"

type BillTab = "unpaid" | "paid" | "all"

export default function MyBillsPage() {
  const [tab, setTab] = useState<BillTab>("unpaid")
  const [selected, setSelected] = useState<Invoice | null>(null)
  const [toast, setToast] = useState("")
  const filtered = invoices.filter(
    (invoice) =>
      tab === "all" ||
      (tab === "unpaid"
        ? invoice.status === "CHƯA THANH TOÁN"
        : invoice.status === "ĐÃ THANH TOÁN"),
  )

  return (
    <CustomerPageShell
      title="Hóa đơn của tôi"
      description="Kiểm tra các khoản phí hàng tháng và lịch sử thanh toán phòng thuê."
    >
      <div className="flex flex-wrap gap-2 border-b border-slate-100">
        {[
          ["unpaid", "Chưa thanh toán"],
          ["paid", "Đã thanh toán"],
          ["all", "Tất cả"],
        ].map(([value, label]) => (
          <button
            key={value}
            onClick={() => setTab(value as BillTab)}
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
      <div className="mt-7 grid gap-4 lg:grid-cols-[1fr_320px]">
        <div className="space-y-4">
          {filtered.map((invoice) => (
            <article
              key={invoice.id}
              className="rounded-2xl bg-white p-6 shadow-[0_3px_18px_rgba(16,65,67,.07)]"
            >
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <p className="text-[12px] font-bold uppercase tracking-[.12em] text-[#087775]">
                    {invoice.id}
                  </p>
                  <h2 className="mt-2 font-display text-[20px] font-bold text-slate-900">
                    {invoice.title}
                  </h2>
                  <p className="mt-1 text-[13px] text-slate-500">
                    Hạn thanh toán: {invoice.due}
                  </p>
                </div>
                <StatusBadge
                  tone={invoice.status === "ĐÃ THANH TOÁN" ? "green" : "amber"}
                >
                  {invoice.status}
                </StatusBadge>
              </div>
              <div className="mt-5 flex items-end justify-between border-t border-slate-100 pt-5">
                <div>
                  <p className="text-[13px] text-slate-400">Tổng cộng</p>
                  <p className="mt-1 text-[22px] font-bold text-[#087775]">
                    {invoice.total}
                  </p>
                </div>
                <div className="flex gap-2">
                  <button
                    onClick={() => setSelected(invoice)}
                    className="rounded-lg bg-slate-100 px-3 py-2.5 text-[12px] font-bold text-slate-600"
                  >
                    Xem chi tiết
                  </button>
                  {invoice.status === "CHƯA THANH TOÁN" && (
                    <button
                      onClick={() =>
                        setToast("Thanh toán mock đã được ghi nhận")
                      }
                      className="rounded-lg bg-[#087775] px-3 py-2.5 text-[12px] font-bold text-white"
                    >
                      Thanh toán
                    </button>
                  )}
                </div>
              </div>
            </article>
          ))}
        </div>
        <aside className="h-fit rounded-2xl bg-[#f1f8f7] p-6">
          <p className="text-[11px] font-bold uppercase tracking-[.14em] text-[#087775]">
            Lịch sử thanh toán
          </p>
          <div className="mt-4 space-y-4">
            {payments.map((payment) => (
              <div
                key={payment.id}
                className="border-b border-teal-100 pb-4 last:border-0 last:pb-0"
              >
                <p className="text-[13px] font-semibold text-slate-700">
                  {payment.title}
                </p>
                <p className="mt-1 text-[12px] text-slate-500">
                  {payment.paidAt} · {payment.method}
                </p>
                <p className="mt-2 font-bold text-[#087775]">
                  {payment.amount}
                </p>
              </div>
            ))}
          </div>
        </aside>
      </div>
      {selected && (
        <InvoiceDetailDrawer
          invoice={selected}
          onClose={() => setSelected(null)}
          onPay={() => {
            setSelected(null)
            setToast("Thanh toán mock đã được ghi nhận")
          }}
        />
      )}
      {toast && <Toast message={toast} onClose={() => setToast("")} />}
    </CustomerPageShell>
  )
}
