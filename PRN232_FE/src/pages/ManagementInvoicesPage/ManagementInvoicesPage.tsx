import { useState } from "react"
import ManagementDataTable from "@/components/management/ManagementDataTable"
import ManagementDetailDrawer from "@/components/management/ManagementDetailDrawer"
import ManagementPage from "@/components/management/ManagementPage"
import StatusBadge from "@/components/customer/StatusBadge"
import Toast from "@/components/customer/Toast"
import { getCurrentUser } from "@/utils/managementAuth"
import { getManagementScope } from "@/utils/managementScope"
import type { ManagedInvoice } from "@/types/management"

function invoiceTone(status: ManagedInvoice["status"]) {
  return status === "PAID"
    ? "green" as const
    : status === "OVERDUE"
      ? "rose" as const
      : status === "DRAFT"
        ? "slate" as const
        : "amber" as const
}
function invoiceLabel(status: ManagedInvoice["status"]) {
  return status === "PAID"
    ? "ĐÃ THANH TOÁN"
    : status === "OVERDUE"
      ? "QUÁ HẠN"
      : status === "DRAFT"
        ? "BẢN NHÁP"
        : "CHƯA THANH TOÁN"
}
export default function ManagementInvoicesPage() {
  const { invoices } = getManagementScope(getCurrentUser())
  const [selected, setSelected] = useState<ManagedInvoice | null>(null)
  const [toast, setToast] = useState("")
  return (
    <ManagementPage
      title="Hóa đơn"
      description="Tạo và theo dõi hóa đơn theo phòng, có hỗ trợ nhập chỉ số điện nước mock."
    >
      <div className="mb-5 flex justify-end">
        <button
          onClick={() =>
            setToast("Form tạo hóa đơn và nhập chỉ số mock sẽ được mở")
          }
          className="rounded-lg bg-[#087775] px-4 py-2.5 text-[13px] font-bold text-white"
        >
          + Tạo hóa đơn
        </button>
      </div>
      <ManagementDataTable
        headers={[
          "Hóa đơn",
          "Khu trọ / Phòng",
          "Khách thuê",
          "Kỳ",
          "Tổng tiền",
          "Trạng thái",
          "Thao tác",
        ]}
      >
        <div className="divide-y divide-slate-100">
          {invoices.map((invoice) => (
            <div
              key={invoice.id}
              className="grid gap-3 px-5 py-5 lg:grid-cols-[1.3fr_1.2fr_1fr_.7fr_1fr_1fr_1fr] lg:items-center"
            >
              <strong className="text-[13px] text-slate-700">
                {invoice.id}
              </strong>
              <span className="text-[13px] text-slate-600">
                {invoice.propertyName}
                <small className="block text-slate-400">
                  Phòng {invoice.roomCode}
                </small>
              </span>
              <span className="text-[13px] text-slate-600">
                {invoice.tenantName}
              </span>
              <span className="text-[13px] text-slate-600">
                {invoice.period}
              </span>
              <span className="font-bold text-[#087775]">{invoice.total}</span>
              <StatusBadge tone={invoiceTone(invoice.status)}>
                {invoiceLabel(invoice.status)}
              </StatusBadge>
              <div className="flex gap-2">
                <button
                  onClick={() => setSelected(invoice)}
                  className="rounded-lg bg-slate-100 px-3 py-2 text-[12px] font-bold text-slate-600"
                >
                  Xem
                </button>
                {invoice.status !== "PAID" && (
                  <button
                    onClick={() =>
                      setToast("Hóa đơn đã được đánh dấu đã thanh toán mock")
                    }
                    className="rounded-lg bg-teal-50 px-3 py-2 text-[12px] font-bold text-[#087775]"
                  >
                    Đã thu
                  </button>
                )}
              </div>
            </div>
          ))}
        </div>
      </ManagementDataTable>
      {selected && (
        <ManagementDetailDrawer
          title={`Hóa đơn ${selected.period}`}
          eyebrow={selected.id}
          onClose={() => setSelected(null)}
        >
          <div className="divide-y divide-slate-100 rounded-xl bg-slate-50 px-5 text-[14px]">
            {[
              ["Tiền phòng", selected.roomRent],
              ["Điện", selected.electricity],
              ["Nước", selected.water],
              ["Internet", selected.internet],
              ["Phí khác", selected.otherFees],
            ].map(([label, amount]) => (
              <div key={label} className="flex justify-between py-3.5">
                <span className="text-slate-500">{label}</span>
                <strong>{amount}</strong>
              </div>
            ))}
            <div className="flex justify-between py-4">
              <strong>Tổng cộng</strong>
              <strong className="text-[#087775]">{selected.total}</strong>
            </div>
          </div>
        </ManagementDetailDrawer>
      )}
      {toast && <Toast message={toast} onClose={() => setToast("")} />}
    </ManagementPage>
  )
}
