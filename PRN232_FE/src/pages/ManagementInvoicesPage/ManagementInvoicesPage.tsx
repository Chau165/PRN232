import { useEffect, useState } from "react"
import ManagementDataTable from "@/components/management/ManagementDataTable"
import ManagementDetailDrawer from "@/components/management/ManagementDetailDrawer"
import ManagementPage from "@/components/management/ManagementPage"
import CreateInvoiceModal from "@/components/management/CreateInvoiceModal"
import InvoiceDetailDrawer from "@/components/customer/InvoiceDetailDrawer"
import StatusBadge from "@/components/customer/StatusBadge"
import Toast from "@/components/customer/Toast"
import { useAuth } from "@/auth/AuthContext"
import { getInvoices, type InvoiceDto } from "@/api/invoicesApi"
import { getCurrentUser } from "@/utils/managementAuth"
import { getManagementScope } from "@/utils/managementScope"
import {
  getInvoiceComplaints,
  updateInvoiceComplaintStatus,
} from "@/utils/invoiceComplaints"
import { formatMonth, invoiceStatusDisplay } from "@/utils/invoiceDisplay"
import { localToday } from "@/utils/managementContracts"
import { formatVnd } from "@/utils/publicFormatting"
import type {
  InvoiceComplaintStatus,
  ManagedInvoiceComplaint,
} from "@/types/management"

type InvoiceSection = "invoices" | "complaints"

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
  const { accessToken } = useAuth()
  // Khiếu nại vẫn là dữ liệu mock (bước 6 sẽ làm thật), nên vẫn lọc theo khu trọ của mock.
  const { propertyIds } = getManagementScope(getCurrentUser())
  const complaints = getInvoiceComplaints().filter((complaint) =>
    propertyIds.includes(complaint.propertyId),
  )
  const today = localToday()
  const [section, setSection] = useState<InvoiceSection>("invoices")
  const [invoices, setInvoices] = useState<InvoiceDto[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [reload, setReload] = useState(0)
  const [creatingInvoice, setCreatingInvoice] = useState(false)
  const [selectedInvoice, setSelectedInvoice] = useState<InvoiceDto | null>(null)
  const [selectedComplaint, setSelectedComplaint] =
    useState<ManagedInvoiceComplaint | null>(null)
  const [, setComplaintsVersion] = useState(0)
  const [toast, setToast] = useState("")

  useEffect(() => {
    if (!accessToken) return
    const controller = new AbortController()
    setLoading(true)
    setError(null)
    getInvoices(accessToken, controller.signal)
      .then(setInvoices)
      .catch((loadError: unknown) => {
        if (!controller.signal.aborted) {
          setError(loadError instanceof Error ? loadError.message : "Không thể tải danh sách hóa đơn.")
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })
    return () => controller.abort()
  }, [accessToken, reload])

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
      description="Ghi chỉ số điện nước và lập hóa đơn tháng theo biểu giá của từng khu trọ."
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
            onClick={() => setCreatingInvoice(true)}
            className="rounded-lg bg-[#087775] px-4 py-2.5 text-[13px] font-bold text-white"
          >
            + Tạo hóa đơn
          </button>
        )}
      </div>
      {section === "invoices" ? (
        <>
          {loading && <p role="status" className="text-[14px] text-slate-500">Đang tải danh sách hóa đơn...</p>}
          {!loading && error && (
            <div role="alert" className="rounded-2xl bg-white p-6 text-center shadow-[0_3px_18px_rgba(16,65,67,.05)]">
              <p className="text-[14px] text-slate-700">{error}</p>
              <button type="button" onClick={() => setReload((value) => value + 1)} className="mt-4 rounded-lg bg-[#087775] px-4 py-2 text-[13px] font-bold text-white">
                Thử lại
              </button>
            </div>
          )}
          {!loading && !error && (
            <ManagementDataTable headers={["Hóa đơn", "Khu trọ / Phòng", "Khách thuê", "Kỳ", "Tổng tiền", "Trạng thái", "Thao tác"]}>
              {invoices.length === 0 ? (
                <p className="px-5 py-8 text-center text-[13px] text-slate-500">Chưa có hóa đơn nào.</p>
              ) : (
                <div className="divide-y divide-slate-100">
                  {invoices.map((invoice) => {
                    const display = invoiceStatusDisplay(invoice, today)
                    return (
                      <div key={invoice.invoiceId} className="grid gap-3 px-5 py-5 lg:grid-cols-7 lg:items-center">
                        <strong className="text-[13px] text-slate-700">HĐ #{invoice.invoiceId}</strong>
                        <span className="text-[13px] text-slate-600">
                          {invoice.propertyName}
                          <small className="block text-slate-400">Phòng {invoice.roomCode}</small>
                        </span>
                        <span className="text-[13px] text-slate-600">{invoice.customerName}</span>
                        <span className="text-[13px] text-slate-600">{formatMonth(invoice.invoiceMonth)}</span>
                        <span className="font-bold text-[#087775]">{formatVnd(invoice.total)}</span>
                        <StatusBadge tone={display.tone}>{display.label}</StatusBadge>
                        <div>
                          <button
                            onClick={() => setSelectedInvoice(invoice)}
                            className="rounded-lg bg-slate-100 px-3 py-2 text-[12px] font-bold text-slate-600"
                          >
                            Xem
                          </button>
                        </div>
                      </div>
                    )
                  })}
                </div>
              )}
            </ManagementDataTable>
          )}
        </>
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
      {creatingInvoice && (
        <CreateInvoiceModal
          onClose={() => setCreatingInvoice(false)}
          onCreated={(invoice) => {
            setInvoices((items) => [invoice, ...items])
            setCreatingInvoice(false)
            setToast(`Đã tạo hóa đơn #${invoice.invoiceId} (${formatVnd(invoice.total)}) và báo cho khách.`)
          }}
        />
      )}
      {selectedInvoice && (
        <InvoiceDetailDrawer invoice={selectedInvoice} onClose={() => setSelectedInvoice(null)} />
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
