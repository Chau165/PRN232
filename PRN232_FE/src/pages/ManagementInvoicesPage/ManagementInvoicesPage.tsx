import { useState } from "react"
import ManagementDataTable from "@/components/management/ManagementDataTable"
import ManagementDetailDrawer from "@/components/management/ManagementDetailDrawer"
import ManagementPage from "@/components/management/ManagementPage"
import StatusBadge from "@/components/customer/StatusBadge"
import Toast from "@/components/customer/Toast"
import { getCurrentUser } from "@/utils/managementAuth"
import { getManagementScope } from "@/utils/managementScope"
import {
  getInvoiceComplaints,
  updateInvoiceComplaintStatus,
} from "@/utils/invoiceComplaints"
import type {
  InvoiceComplaintStatus,
  ManagedInvoice,
  ManagedInvoiceComplaint,
} from "@/types/management"

type InvoiceSection = "invoices" | "complaints"

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

function complaintTone(status: ManagedInvoiceComplaint["status"]) {
  return status === "ĐÃ XỬ LÝ" ? "green" as const : "amber" as const
}

const complaintStatuses: Array<{
  value: InvoiceComplaintStatus
  label: string
}> = [
  { value: "MỚI", label: "Mới" },
  { value: "ĐANG XỬ LÝ", label: "Đang xử lý" },
  { value: "ĐÃ XỬ LÝ", label: "Đã xử lý" },
]

export default function ManagementInvoicesPage() {
  const { invoices, propertyIds } = getManagementScope(getCurrentUser())
  const complaints = getInvoiceComplaints().filter((complaint) =>
    propertyIds.includes(complaint.propertyId),
  )
  const [section, setSection] = useState<InvoiceSection>("invoices")
  const [selectedInvoice, setSelectedInvoice] = useState<ManagedInvoice | null>(null)
  const [selectedComplaint, setSelectedComplaint] =
    useState<ManagedInvoiceComplaint | null>(null)
  const [, setComplaintsVersion] = useState(0)
  const [toast, setToast] = useState("")

  function changeComplaintStatus(status: InvoiceComplaintStatus) {
    if (!selectedComplaint || selectedComplaint.status === status) return
    const updatedComplaint = updateInvoiceComplaintStatus(
      selectedComplaint.id,
      status,
    )
    if (!updatedComplaint) return

    setSelectedComplaint(updatedComplaint)
    setComplaintsVersion((version) => version + 1)
    setToast("Đã cập nhật trạng thái khiếu nại")
  }

  return (
    <ManagementPage
      title="Hóa đơn"
      description="Tạo và theo dõi hóa đơn theo phòng, có hỗ trợ nhập chỉ số điện nước mock."
    >
      <div className="mb-5 flex flex-wrap items-center justify-between gap-3">
        <div
          className="inline-flex rounded-xl bg-slate-100 p-1"
          role="tablist"
          aria-label="Nội dung hóa đơn"
        >
          <button
            role="tab"
            aria-selected={section === "invoices"}
            onClick={() => setSection("invoices")}
            className={`rounded-lg px-4 py-2 text-[12px] font-bold transition-colors ${
              section === "invoices"
                ? "bg-[#087775] text-white shadow-sm"
                : "text-slate-500 hover:text-[#087775]"
            }`}
          >
            Hóa đơn
          </button>
          <button
            role="tab"
            aria-selected={section === "complaints"}
            onClick={() => setSection("complaints")}
            className={`rounded-lg px-4 py-2 text-[12px] font-bold transition-colors ${
              section === "complaints"
                ? "bg-[#087775] text-white shadow-sm"
                : "text-slate-500 hover:text-[#087775]"
            }`}
          >
            Khiếu nại hóa đơn
          </button>
        </div>
        {section === "invoices" && (
          <button
            onClick={() =>
              setToast("Form tạo hóa đơn và nhập chỉ số mock sẽ được mở")
            }
            className="rounded-lg bg-[#087775] px-4 py-2.5 text-[13px] font-bold text-white"
          >
            + Tạo hóa đơn
          </button>
        )}
      </div>
      {section === "invoices" ? (
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
                    onClick={() => setSelectedInvoice(invoice)}
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
      ) : (
        <ManagementDataTable
          headers={[
            "Mã khiếu nại",
            "Hóa đơn",
            "Khách thuê",
            "Loại phí",
            "Nội dung",
            "Ngày gửi",
            "Trạng thái",
            "Thao tác",
          ]}
        >
          {complaints.length > 0 ? (
            <div className="divide-y divide-slate-100">
              {complaints.map((complaint) => (
                <div
                  key={complaint.id}
                  className="grid gap-3 px-5 py-5 lg:grid-cols-[1fr_1.1fr_.85fr_.9fr_1.6fr_.75fr_.9fr_.6fr] lg:items-center"
                >
                  <strong className="text-[13px] text-slate-700">
                    {complaint.id}
                  </strong>
                  <span className="text-[13px] text-slate-600">
                    {complaint.invoiceId}
                    <small className="block text-slate-400">
                      {complaint.propertyName} · Phòng {complaint.roomCode}
                    </small>
                  </span>
                  <span className="text-[13px] text-slate-600">
                    {complaint.tenantName}
                  </span>
                  <span className="text-[13px] font-semibold text-slate-600">
                    {complaint.feeType}
                  </span>
                  <span
                    title={complaint.content}
                    className="truncate text-[13px] text-slate-500"
                  >
                    {complaint.content}
                  </span>
                  <span className="text-[13px] text-slate-600">
                    {complaint.createdAt}
                  </span>
                  <StatusBadge tone={complaintTone(complaint.status)}>
                    {complaint.status}
                  </StatusBadge>
                  <button
                    onClick={() => setSelectedComplaint(complaint)}
                    className="w-fit rounded-lg bg-slate-100 px-3 py-2 text-[12px] font-bold text-slate-600"
                  >
                    Xem
                  </button>
                </div>
              ))}
            </div>
          ) : (
            <div className="px-5 py-12 text-center text-[13px] text-slate-500">
              Chưa có khiếu nại hóa đơn nào.
            </div>
          )}
        </ManagementDataTable>
      )}
      {selectedInvoice && (
        <ManagementDetailDrawer
          title={`Hóa đơn ${selectedInvoice.period}`}
          eyebrow={selectedInvoice.id}
          onClose={() => setSelectedInvoice(null)}
        >
          <div className="divide-y divide-slate-100 rounded-xl bg-slate-50 px-5 text-[14px]">
            {[
              ["Tiền phòng", selectedInvoice.roomRent],
              ["Điện", selectedInvoice.electricity],
              ["Nước", selectedInvoice.water],
              ["Internet", selectedInvoice.internet],
              ["Phí khác", selectedInvoice.otherFees],
            ].map(([label, amount]) => (
              <div key={label} className="flex justify-between py-3.5">
                <span className="text-slate-500">{label}</span>
                <strong>{amount}</strong>
              </div>
            ))}
            <div className="flex justify-between py-4">
              <strong>Tổng cộng</strong>
              <strong className="text-[#087775]">{selectedInvoice.total}</strong>
            </div>
          </div>
        </ManagementDetailDrawer>
      )}
      {selectedComplaint && (
        <ManagementDetailDrawer
          title="Khiếu nại hóa đơn"
          eyebrow={selectedComplaint.id}
          onClose={() => setSelectedComplaint(null)}
        >
          <div className="space-y-5">
            <div className="flex items-center justify-between gap-4">
              <div>
                <p className="text-[12px] text-slate-400">Trạng thái</p>
                <div className="mt-1">
                  <StatusBadge tone={complaintTone(selectedComplaint.status)}>
                    {selectedComplaint.status}
                  </StatusBadge>
                </div>
              </div>
              <p className="text-[13px] text-slate-500">
                Gửi ngày {selectedComplaint.createdAt}
              </p>
            </div>
            <fieldset>
              <legend className="text-[12px] font-semibold text-slate-500">
                Cập nhật trạng thái
              </legend>
              <div className="mt-2 flex flex-wrap gap-2">
                {complaintStatuses.map(({ value, label }) => (
                  <button
                    key={value}
                    type="button"
                    onClick={() => changeComplaintStatus(value)}
                    aria-pressed={selectedComplaint.status === value}
                    className={`rounded-lg px-3.5 py-2.5 text-[12px] font-bold transition-colors ${
                      selectedComplaint.status === value
                        ? "bg-[#087775] text-white shadow-sm"
                        : "bg-slate-100 text-slate-600 hover:bg-teal-50 hover:text-[#087775]"
                    }`}
                  >
                    {label}
                  </button>
                ))}
              </div>
            </fieldset>
            <div className="grid gap-3 rounded-xl bg-slate-50 p-5 text-[14px] sm:grid-cols-2">
              <p>
                <span className="block text-[12px] text-slate-400">Hóa đơn</span>
                <strong className="text-slate-700">{selectedComplaint.invoiceId}</strong>
              </p>
              <p>
                <span className="block text-[12px] text-slate-400">Loại phí</span>
                <strong className="text-slate-700">{selectedComplaint.feeType}</strong>
              </p>
              <p>
                <span className="block text-[12px] text-slate-400">Khách thuê</span>
                <strong className="text-slate-700">{selectedComplaint.tenantName}</strong>
              </p>
              <p>
                <span className="block text-[12px] text-slate-400">Phòng</span>
                <strong className="text-slate-700">
                  {selectedComplaint.propertyName} · {selectedComplaint.roomCode}
                </strong>
              </p>
            </div>
            <div>
              <p className="text-[12px] font-semibold text-slate-500">
                Nội dung khiếu nại
              </p>
              <p className="mt-2 rounded-xl border border-slate-100 px-4 py-3 text-[14px] leading-6 text-slate-700">
                {selectedComplaint.content}
              </p>
            </div>
          </div>
        </ManagementDetailDrawer>
      )}
      {toast && <Toast message={toast} onClose={() => setToast("")} />}
    </ManagementPage>
  )
}
