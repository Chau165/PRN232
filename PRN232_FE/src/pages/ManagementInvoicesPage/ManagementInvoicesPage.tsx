import { useEffect, useState } from "react"
import ManagementDataTable from "@/components/management/ManagementDataTable"
import ManagementDetailDrawer from "@/components/management/ManagementDetailDrawer"
import ManagementPage from "@/components/management/ManagementPage"
import CreateInvoiceModal from "@/components/management/CreateInvoiceModal"
import InvoiceDetailDrawer from "@/components/customer/InvoiceDetailDrawer"
import StatusBadge from "@/components/customer/StatusBadge"
import Toast from "@/components/customer/Toast"
import { useAuth } from "@/auth/AuthContext"
import { getInvoices, recordCashPayment, type InvoiceDto } from "@/api/invoicesApi"
import {
  getInvoiceComplaints,
  updateInvoiceComplaintStatus,
  type ComplaintStatus,
  type InvoiceComplaintDto,
} from "@/api/invoiceComplaintsApi"
import { bookingErrorMessage } from "@/utils/bookingDisplay"
import { complaintStatusDisplay, complaintStatuses, formatComplaintDate } from "@/utils/complaintDisplay"
import { formatMonth, invoiceStatusDisplay } from "@/utils/invoiceDisplay"
import { localToday } from "@/utils/managementContracts"
import { formatVnd } from "@/utils/publicFormatting"

type InvoiceSection = "invoices" | "complaints"

export default function ManagementInvoicesPage() {
  const { accessToken } = useAuth()
  const today = localToday()
  const [section, setSection] = useState<InvoiceSection>("invoices")
  const [invoices, setInvoices] = useState<InvoiceDto[]>([])
  const [complaints, setComplaints] = useState<InvoiceComplaintDto[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [reload, setReload] = useState(0)
  const [creatingInvoice, setCreatingInvoice] = useState(false)
  const [selectedInvoice, setSelectedInvoice] = useState<InvoiceDto | null>(null)
  const [recordingId, setRecordingId] = useState<number | null>(null)
  const [selectedComplaint, setSelectedComplaint] = useState<InvoiceComplaintDto | null>(null)
  const [updatingComplaint, setUpdatingComplaint] = useState(false)
  const [toast, setToast] = useState("")

  // BE đã lọc theo phạm vi: quản lý chỉ nhận hoá đơn và khiếu nại của khu trọ mình sở hữu.
  useEffect(() => {
    if (!accessToken) return
    const controller = new AbortController()
    setLoading(true)
    setError(null)
    Promise.all([getInvoices(accessToken, controller.signal), getInvoiceComplaints(accessToken, controller.signal)])
      .then(([invoiceItems, complaintItems]) => {
        setInvoices(invoiceItems)
        setComplaints(complaintItems)
      })
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

  // Khách trả tiền mặt tại khu trọ: quản lý xác nhận để hoá đơn chuyển sang đã thanh toán.
  async function recordCash(invoice: InvoiceDto) {
    if (!accessToken || recordingId !== null) return
    if (!window.confirm(`Xác nhận đã thu ${formatVnd(invoice.total)} tiền mặt của ${invoice.customerName} cho hóa đơn tháng ${formatMonth(invoice.invoiceMonth)}?`)) return
    setRecordingId(invoice.invoiceId)
    try {
      const paid = await recordCashPayment(accessToken, invoice.invoiceId)
      setInvoices((items) => items.map((item) => (item.invoiceId === paid.invoiceId ? paid : item)))
      setToast(`Đã ghi nhận thanh toán hóa đơn #${paid.invoiceId} và báo cho khách.`)
    } catch (recordError) {
      setToast(bookingErrorMessage(recordError, "Hóa đơn này đã được thanh toán, vui lòng tải lại trang."))
    } finally {
      setRecordingId(null)
    }
  }

  async function changeComplaintStatus(status: ComplaintStatus) {
    if (!accessToken || !selectedComplaint || selectedComplaint.status === status || updatingComplaint) return
    setUpdatingComplaint(true)
    try {
      const updated = await updateInvoiceComplaintStatus(accessToken, selectedComplaint.complaintId, status)
      setSelectedComplaint(updated)
      setComplaints((items) => items.map((item) => (item.complaintId === updated.complaintId ? updated : item)))
      setToast("Đã cập nhật trạng thái khiếu nại và báo cho khách.")
    } catch (updateError) {
      setToast(bookingErrorMessage(updateError, "Không thể cập nhật khiếu nại, vui lòng tải lại trang."))
    } finally {
      setUpdatingComplaint(false)
    }
  }

  return (
    <ManagementPage
      title="Hóa đơn"
      description="Ghi chỉ số điện nước, lập hóa đơn tháng theo biểu giá của từng khu trọ và xử lý khiếu nại."
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
      {loading && <p role="status" className="text-[14px] text-slate-500">Đang tải dữ liệu hóa đơn...</p>}
      {!loading && error && (
        <div role="alert" className="rounded-2xl bg-white p-6 text-center shadow-[0_3px_18px_rgba(16,65,67,.05)]">
          <p className="text-[14px] text-slate-700">{error}</p>
          <button type="button" onClick={() => setReload((value) => value + 1)} className="mt-4 rounded-lg bg-[#087775] px-4 py-2 text-[13px] font-bold text-white">
            Thử lại
          </button>
        </div>
      )}
      {!loading && !error && section === "invoices" && (
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
                    <div className="flex flex-wrap gap-2">
                      <button
                        onClick={() => setSelectedInvoice(invoice)}
                        className="rounded-lg bg-slate-100 px-3 py-2 text-[12px] font-bold text-slate-600"
                      >
                        Xem
                      </button>
                      {invoice.status !== "Paid" && (
                        <button
                          disabled={recordingId !== null}
                          onClick={() => recordCash(invoice)}
                          className="rounded-lg bg-teal-50 px-3 py-2 text-[12px] font-bold text-[#087775] disabled:opacity-50"
                        >
                          {recordingId === invoice.invoiceId ? "Đang ghi..." : "Đã thu tiền mặt"}
                        </button>
                      )}
                    </div>
                  </div>
                )
              })}
            </div>
          )}
        </ManagementDataTable>
      )}
      {!loading && !error && section === "complaints" && (
        <ManagementDataTable headers={["Mã khiếu nại", "Hóa đơn", "Khách thuê", "Loại phí", "Nội dung", "Ngày gửi", "Trạng thái", "Thao tác"]}>
          {complaints.length > 0 ? (
            <div className="divide-y divide-slate-100">
              {complaints.map((complaint) => {
                const display = complaintStatusDisplay(complaint.status)
                return (
                  <div key={complaint.complaintId} className="grid gap-3 px-5 py-5 lg:grid-cols-8 lg:items-center">
                    <strong className="text-[13px] text-slate-700">KN #{complaint.complaintId}</strong>
                    <span className="text-[13px] text-slate-600">
                      HĐ #{complaint.invoiceId} · {formatMonth(complaint.invoiceMonth)}
                      <small className="block text-slate-400">{complaint.propertyName} · Phòng {complaint.roomCode}</small>
                    </span>
                    <span className="text-[13px] text-slate-600">{complaint.customerName}</span>
                    <span className="text-[13px] font-semibold text-slate-600">{complaint.feeType}</span>
                    <span title={complaint.content} className="truncate text-[13px] text-slate-500">{complaint.content}</span>
                    <span className="text-[13px] text-slate-600">{formatComplaintDate(complaint.createdAt)}</span>
                    <StatusBadge tone={display.tone}>{display.label}</StatusBadge>
                    <button
                      onClick={() => setSelectedComplaint(complaint)}
                      className="w-fit rounded-lg bg-slate-100 px-3 py-2 text-[12px] font-bold text-slate-600"
                    >
                      Xem
                    </button>
                  </div>
                )
              })}
            </div>
          ) : (
            <div className="px-5 py-12 text-center text-[13px] text-slate-500">Chưa có khiếu nại hóa đơn nào.</div>
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
          eyebrow={`KN #${selectedComplaint.complaintId}`}
          onClose={() => setSelectedComplaint(null)}
        >
          <div className="space-y-5">
            <div className="flex items-center justify-between gap-4">
              <div>
                <p className="text-[12px] text-slate-400">Trạng thái</p>
                <div className="mt-1">
                  <StatusBadge tone={complaintStatusDisplay(selectedComplaint.status).tone}>
                    {complaintStatusDisplay(selectedComplaint.status).label}
                  </StatusBadge>
                </div>
              </div>
              <p className="text-[13px] text-slate-500">Gửi ngày {formatComplaintDate(selectedComplaint.createdAt)}</p>
            </div>
            <fieldset disabled={updatingComplaint}>
              <legend className="text-[12px] font-semibold text-slate-500">Cập nhật trạng thái</legend>
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
                <strong className="text-slate-700">HĐ #{selectedComplaint.invoiceId} · {formatMonth(selectedComplaint.invoiceMonth)}</strong>
              </p>
              <p>
                <span className="block text-[12px] text-slate-400">Loại phí</span>
                <strong className="text-slate-700">{selectedComplaint.feeType}</strong>
              </p>
              <p>
                <span className="block text-[12px] text-slate-400">Khách thuê</span>
                <strong className="text-slate-700">{selectedComplaint.customerName}</strong>
              </p>
              <p>
                <span className="block text-[12px] text-slate-400">Phòng</span>
                <strong className="text-slate-700">{selectedComplaint.propertyName} · {selectedComplaint.roomCode}</strong>
              </p>
            </div>
            <div>
              <p className="text-[12px] font-semibold text-slate-500">Nội dung khiếu nại</p>
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
