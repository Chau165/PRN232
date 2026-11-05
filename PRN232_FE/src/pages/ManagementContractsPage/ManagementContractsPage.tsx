import { useEffect, useState } from "react"
import { useLocation } from "react-router"
import ManagementDataTable from "@/components/management/ManagementDataTable"
import ManagementDetailDrawer from "@/components/management/ManagementDetailDrawer"
import ManagementPage from "@/components/management/ManagementPage"
import StatusBadge from "@/components/customer/StatusBadge"
import Toast from "@/components/customer/Toast"
import { useAuth } from "@/auth/AuthContext"
import {
  extendContract,
  getContractBillingTimeline,
  getContracts,
  markTerminatedRoomReady,
  terminateContract,
} from "@/services/contractService"
import type { ContractBillingTimelineDto, ContractDto } from "@/types/contract"
import type { InvoiceDto } from "@/types/invoice"
import { formatMonth } from "@/utils/invoiceDisplay"
import { addContractMonths, formatContractDate, localToday } from "@/utils/managementContracts"
import { formatVnd } from "@/utils/publicFormatting"

type ContractAction = { kind: "extend" | "terminate"; contract: ContractDto }
type TimelineState = { loading: boolean; error: string | null; timeline: ContractBillingTimelineDto | null }

function statusLabel(status: string) {
  switch (status) {
    case "Active": return "Đang hiệu lực"
    case "Upcoming": return "Sắp có hiệu lực"
    case "ExpiredPendingCheckout": return "Hết hạn — Chờ trả phòng"
    case "Terminated": return "Đã kết thúc / Đã trả phòng"
    case "Ended": return "Đã kết thúc"
    default: return status
  }
}

function statusTone(status: string) {
  if (status === "Active") return "green"
  if (status === "Upcoming" || status === "ExpiredPendingCheckout") return "amber"
  return "slate"
}

function remainingAmount(invoice: InvoiceDto) {
  return invoice.remainingPayable ?? invoice.netPayable ?? invoice.total
}

function addDays(value: string, days: number) {
  const date = new Date(`${value}T00:00:00Z`)
  date.setUTCDate(date.getUTCDate() + days)
  return date.toISOString().slice(0, 10)
}

export default function ManagementContractsPage() {
  const { accessToken, user } = useAuth()
  const location = useLocation()
  const [contracts, setContracts] = useState<ContractDto[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [reload, setReload] = useState(0)
  const [selected, setSelected] = useState<ContractDto | null>(null)
  const [action, setAction] = useState<ContractAction | null>(null)
  const [timelineState, setTimelineState] = useState<TimelineState | null>(null)
  const [newEndDate, setNewEndDate] = useState("")
  const [checkoutConfirmed, setCheckoutConfirmed] = useState(false)
  const [roomReadyForRental, setRoomReadyForRental] = useState(false)
  const [outstandingDebtAcknowledged, setOutstandingDebtAcknowledged] = useState(false)
  const [savingAction, setSavingAction] = useState(false)
  const [markingRoomReadyId, setMarkingRoomReadyId] = useState<number | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  // Trang "Tạo hợp đồng" chuyển sang đây kèm id hợp đồng vừa tạo để báo thành công.
  const createdContractId = (location.state as { createdContractId?: number } | null)?.createdContractId
  const [toast, setToast] = useState(createdContractId ? `Đã tạo hợp đồng #${createdContractId} thành công.` : "")
  const isManager = user?.role === "MANAGER"

  useEffect(() => {
    if (!accessToken) return
    const controller = new AbortController()
    setLoading(true)
    setError(null)
    getContracts(accessToken, controller.signal)
      .then(setContracts)
      .catch((fetchError: unknown) => {
        if (!controller.signal.aborted) {
          setError(fetchError instanceof Error ? fetchError.message : "Không thể tải danh sách hợp đồng.")
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })
    return () => controller.abort()
  }, [accessToken, reload])

  useEffect(() => {
    if (!accessToken || !action) {
      setTimelineState(null)
      return
    }

    const controller = new AbortController()
    setTimelineState({ loading: true, error: null, timeline: null })
    setActionError(null)
    getContractBillingTimeline(accessToken, action.contract.contractId, controller.signal)
      .then((timeline) => {
        if (!controller.signal.aborted) setTimelineState({ loading: false, error: null, timeline })
      })
      .catch((loadError: unknown) => {
        if (!controller.signal.aborted) {
          setTimelineState({
            loading: false,
            error: loadError instanceof Error ? loadError.message : "Không thể tải hóa đơn của hợp đồng.",
            timeline: null,
          })
        }
      })
    return () => controller.abort()
  }, [accessToken, action])

  function openAction(contract: ContractDto, kind: ContractAction["kind"]) {
    setNewEndDate(contract.endDate)
    setCheckoutConfirmed(false)
    setRoomReadyForRental(false)
    setOutstandingDebtAcknowledged(false)
    setActionError(null)
    setAction({ contract, kind })
  }

  async function submitAction() {
    if (!accessToken || !action || !timelineState?.timeline || savingAction) return
    setSavingAction(true)
    setActionError(null)
    try {
      if (action.kind === "extend") {
        await extendContract(accessToken, action.contract.contractId, { newEndDate })
        setToast(`Đã gia hạn hợp đồng #${action.contract.contractId} đến ${formatContractDate(newEndDate)}.`)
      } else {
        const result = await terminateContract(accessToken, action.contract.contractId, {
          checkoutConfirmed,
          roomReadyForRental,
          outstandingDebtAcknowledged,
        })
        setToast(result.outstandingDebt > 0
          ? `Đã kết thúc hợp đồng #${action.contract.contractId}. Công nợ ${formatVnd(result.outstandingDebt)} vẫn được giữ nguyên.`
          : `Đã kết thúc hợp đồng #${action.contract.contractId} và xác nhận trả phòng.`)
      }
      setAction(null)
      setReload((value) => value + 1)
    } catch (saveError) {
      setActionError(saveError instanceof Error ? saveError.message : "Không thể cập nhật hợp đồng.")
    } finally {
      setSavingAction(false)
    }
  }

  async function markRoomReady(contract: ContractDto) {
    if (!accessToken || markingRoomReadyId !== null) return
    if (!window.confirm(`Xác nhận phòng ${contract.roomCode} đã được dọn dẹp, không còn tài sản và sẵn sàng cho thuê lại?`)) return

    setMarkingRoomReadyId(contract.contractId)
    try {
      await markTerminatedRoomReady(accessToken, contract.contractId)
      setToast(`Phòng ${contract.roomCode} đã được đánh dấu sẵn sàng cho thuê.`)
      setReload((value) => value + 1)
    } catch (saveError) {
      setToast(saveError instanceof Error ? saveError.message : "Không thể cập nhật trạng thái phòng.")
    } finally {
      setMarkingRoomReadyId(null)
    }
  }

  const activeInvoices = timelineState?.timeline?.issuedInvoices.filter((invoice) => remainingAmount(invoice) > 0) ?? []
  const outstandingDebt = activeInvoices.reduce((total, invoice) => total + remainingAmount(invoice), 0)
  const unissuedPeriods = timelineState?.timeline?.plannedBillingPeriods.filter(
    (period) => period.invoiceMonth <= (action?.contract.endDate ?? ""),
  ) ?? []
  const earliestValidEndDate = action
    ? [addDays(localToday(), 1), addDays(action.contract.endDate, 1)].sort().at(-1) ?? ""
    : ""
  const validNewEndDate = Boolean(action && newEndDate > action.contract.endDate && newEndDate > localToday())
  const canSubmit = Boolean(
    action &&
    timelineState?.timeline &&
    !timelineState.loading &&
    !timelineState.error &&
    (action.kind === "extend"
      ? validNewEndDate
      : checkoutConfirmed && (outstandingDebt === 0 || outstandingDebtAcknowledged)),
  )

  return (
    <ManagementPage title="Hợp đồng" description="Quản lý hợp đồng thuê được lập từ các booking đã đặt cọc.">
      {loading && <p role="status" className="text-[14px] text-slate-500">Đang tải danh sách hợp đồng...</p>}
      {!loading && error && (
        <div role="alert" className="rounded-2xl bg-white p-6 text-center shadow-[0_3px_18px_rgba(16,65,67,.05)]">
          <p className="text-[14px] text-slate-700">{error}</p>
          <button type="button" onClick={() => setReload((value) => value + 1)} className="mt-4 rounded-lg bg-[#087775] px-4 py-2 text-[13px] font-bold text-white">
            Thử lại
          </button>
        </div>
      )}
      {!loading && !error && (
        <ManagementDataTable headers={["Hợp đồng", "Khách thuê", "Khu trọ / Phòng", "Thời hạn", "Giá thuê", "Trạng thái", "Thao tác"]}>
          {contracts.length === 0 ? (
            <p className="px-5 py-8 text-center text-[13px] text-slate-500">Chưa có hợp đồng nào.</p>
          ) : (
            <div className="divide-y divide-slate-100">
              {contracts.map((contract) => {
                const displayStatus = contract.displayStatus || contract.status
                const canManage = isManager && contract.status === "Active"
                return (
                  <div key={contract.contractId} className="grid gap-3 px-5 py-5 lg:grid-cols-7 lg:items-center">
                    <strong className="text-[13px] text-slate-700">HĐ #{contract.contractId}</strong>
                    <span className="text-[13px] text-slate-600">{contract.customerName}</span>
                    <span className="text-[13px] text-slate-600">
                      {contract.propertyName}
                      <small className="block text-slate-400">Phòng {contract.roomCode}</small>
                    </span>
                    <span className="text-[13px] text-slate-600">
                      {formatContractDate(contract.startDate)}
                      <small className="block text-slate-400">đến {formatContractDate(contract.endDate)}</small>
                    </span>
                    <span className="font-semibold text-[#087775]">{formatVnd(contract.monthlyRent)}</span>
                    <StatusBadge tone={statusTone(displayStatus)}>{statusLabel(displayStatus)}</StatusBadge>
                    <div className="flex flex-wrap gap-2">
                      <button
                        type="button"
                        onClick={() => setSelected(contract)}
                        className="rounded-lg bg-slate-100 px-3 py-2 text-[12px] font-bold text-slate-600"
                      >
                        Xem
                      </button>
                      {canManage && (
                        <>
                          <button
                            type="button"
                            onClick={() => openAction(contract, "extend")}
                            className="rounded-lg border border-[#087775] px-3 py-2 text-[12px] font-bold text-[#087775]"
                          >
                            Gia hạn hợp đồng
                          </button>
                          <button
                            type="button"
                            onClick={() => openAction(contract, "terminate")}
                            className="rounded-lg bg-rose-50 px-3 py-2 text-[12px] font-bold text-rose-700"
                          >
                            Xác nhận kết thúc / Trả phòng
                          </button>
                        </>
                      )}
                      {isManager && contract.status === "Terminated" && (
                        <button
                          type="button"
                          disabled={markingRoomReadyId === contract.contractId}
                          onClick={() => void markRoomReady(contract)}
                          className="rounded-lg bg-emerald-50 px-3 py-2 text-[12px] font-bold text-emerald-800 disabled:opacity-50"
                        >
                          {markingRoomReadyId === contract.contractId ? "Đang cập nhật..." : "Đánh dấu phòng sẵn sàng"}
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
      {selected && (
        <ManagementDetailDrawer title="Chi tiết hợp đồng" eyebrow={`HĐ #${selected.contractId}`} onClose={() => setSelected(null)}>
          <dl className="space-y-3 rounded-xl bg-slate-50 p-5 text-[14px]">
            <Detail label="Khách thuê" value={selected.customerName} />
            <Detail label="Số điện thoại" value={selected.customerPhone || "Chưa có thông tin"} />
            {selected.bookingId && <Detail label="Booking" value={`#${selected.bookingId}`} />}
            <Detail label="Khu trọ" value={selected.propertyName} />
            <Detail label="Phòng" value={selected.roomCode} />
            <Detail label="Thời hạn" value={`${formatContractDate(selected.startDate)} - ${formatContractDate(selected.endDate)}`} />
            <Detail label="Giá thuê" value={`${formatVnd(selected.monthlyRent)}/tháng`} />
            <Detail label="Tiền cọc" value={formatVnd(selected.depositAmount)} />
            <Detail label="Trạng thái" value={statusLabel(selected.displayStatus || selected.status)} />
            {selected.processedByStaffName && <Detail label="Người lập" value={selected.processedByStaffName} />}
          </dl>
        </ManagementDetailDrawer>
      )}
      {action && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/50 p-4" role="presentation">
          <section
            role="dialog"
            aria-modal="true"
            aria-labelledby="contract-action-title"
            className="max-h-[92vh] w-full max-w-2xl overflow-y-auto rounded-2xl bg-white p-6 shadow-2xl"
          >
            <div className="flex items-start justify-between gap-4">
              <div>
                <p className="text-xs font-bold uppercase tracking-wider text-[#087775]">Thông tin hợp đồng #{action.contract.contractId}</p>
                <h2 id="contract-action-title" className="mt-1 font-display text-xl font-bold text-slate-900">
                  {action.kind === "extend" ? "Gia hạn hợp đồng" : "Xác nhận kết thúc / Trả phòng"}
                </h2>
              </div>
              <button type="button" onClick={() => setAction(null)} className="rounded-lg px-3 py-2 text-sm font-semibold text-slate-500">
                Đóng
              </button>
            </div>

            <dl className="mt-5 grid gap-x-6 gap-y-3 rounded-xl bg-slate-50 p-4 text-sm sm:grid-cols-2">
              <Detail label="Khu trọ" value={action.contract.propertyName} />
              <Detail label="Phòng" value={action.contract.roomCode} />
              <Detail label="Khách thuê" value={action.contract.customerName} />
              <Detail label="Trạng thái" value={statusLabel(action.contract.displayStatus || action.contract.status)} />
              <Detail label="Ngày bắt đầu" value={formatContractDate(action.contract.startDate)} />
              <Detail label="Ngày kết thúc hiện tại" value={formatContractDate(action.contract.endDate)} />
              <Detail label="Giá thuê hiện tại" value={`${formatVnd(action.contract.monthlyRent)}/tháng`} />
            </dl>

            {timelineState?.loading && <p role="status" className="mt-5 text-sm text-slate-500">Đang tải hóa đơn và công nợ...</p>}
            {timelineState?.error && (
              <p role="alert" className="mt-5 rounded-lg bg-rose-50 p-3 text-sm text-rose-700">{timelineState.error}</p>
            )}
            {timelineState?.timeline && (
              <section className="mt-5" aria-label="Công nợ hợp đồng">
                <h3 className="font-bold text-slate-800">Hóa đơn chưa thanh toán</h3>
                {activeInvoices.length === 0 ? (
                  <p className="mt-2 rounded-lg bg-emerald-50 p-3 text-sm text-emerald-800">Không có công nợ từ các hóa đơn đã phát hành.</p>
                ) : (
                  <>
                    <ul className="mt-3 space-y-2">
                      {activeInvoices.map((invoice) => <OutstandingInvoice key={invoice.invoiceId} invoice={invoice} />)}
                    </ul>
                    <p className="mt-3 rounded-lg bg-amber-50 p-3 text-sm font-bold text-amber-950">
                      Khách thuê còn công nợ {formatVnd(outstandingDebt)}. Xác nhận trả phòng sẽ không xóa các hóa đơn này. Khách vẫn có trách nhiệm thanh toán sau khi hợp đồng kết thúc.
                    </p>
                  </>
                )}
                {unissuedPeriods.length > 0 && (
                  <p className="mt-3 rounded-lg bg-orange-50 p-3 text-sm text-orange-900">
                    Còn kỳ thanh toán chưa phát hành. Hãy hoàn tất hóa đơn kỳ cuối phù hợp trước khi trả phòng; hệ thống không tự tạo hóa đơn hoặc tính prorate.
                  </p>
                )}
              </section>
            )}

            {action.kind === "extend" && (
              <div className="mt-5">
                <label htmlFor="new-contract-end-date" className="block text-sm font-semibold text-slate-700">Ngày kết thúc mới</label>
                <input
                  id="new-contract-end-date"
                  type="date"
                  min={earliestValidEndDate}
                  value={newEndDate}
                  onChange={(event) => setNewEndDate(event.target.value)}
                  className="mt-2 w-full rounded-lg border border-slate-200 px-3 py-2"
                />
                <div className="mt-3 flex flex-wrap gap-2">
                  {[3, 6, 12].map((months) => {
                    const option = addContractMonths(action.contract.endDate, months)
                    return option > localToday() && (
                      <button
                        key={months}
                        type="button"
                        onClick={() => setNewEndDate(option)}
                        className="rounded-full bg-slate-100 px-3 py-1.5 text-xs font-semibold text-slate-700"
                      >
                        Thêm {months} tháng
                      </button>
                    )
                  })}
                </div>
                {!validNewEndDate && <p className="mt-2 text-xs text-rose-600">Ngày mới phải sau ngày kết thúc hiện tại và là ngày tương lai.</p>}
              </div>
            )}

            {action.kind === "terminate" && (
              <div className="mt-5 space-y-3 text-sm">
                <label className="flex items-start gap-3 rounded-lg border border-slate-200 p-3">
                  <input type="checkbox" checked={checkoutConfirmed} onChange={(event) => setCheckoutConfirmed(event.target.checked)} />
                  <span>Manager xác nhận Customer đã thực tế bàn giao phòng.</span>
                </label>
                <label className="flex items-start gap-3 rounded-lg border border-slate-200 p-3">
                  <input type="checkbox" checked={roomReadyForRental} onChange={(event) => setRoomReadyForRental(event.target.checked)} />
                  <span>Phòng đã sẵn sàng cho thuê lại. Nếu chưa chọn, phòng sẽ được giữ ở trạng thái bảo trì, không cho Booking.</span>
                </label>
                {outstandingDebt > 0 && (
                  <label className="flex items-start gap-3 rounded-lg border border-amber-300 bg-amber-50 p-3 text-amber-950">
                    <input type="checkbox" checked={outstandingDebtAcknowledged} onChange={(event) => setOutstandingDebtAcknowledged(event.target.checked)} />
                    <span>Tôi đã đọc cảnh báo công nợ {formatVnd(outstandingDebt)} và xác nhận các hóa đơn chưa thanh toán vẫn được giữ nguyên.</span>
                  </label>
                )}
              </div>
            )}

            {actionError && <p role="alert" className="mt-4 rounded-lg bg-rose-50 p-3 text-sm text-rose-700">{actionError}</p>}
            <div className="mt-6 flex justify-end gap-3">
              <button type="button" onClick={() => setAction(null)} className="rounded-lg border border-slate-200 px-4 py-2 text-sm font-semibold text-slate-600">
                Hủy
              </button>
              <button
                type="button"
                disabled={!canSubmit || savingAction}
                onClick={() => void submitAction()}
                className="rounded-lg bg-[#087775] px-4 py-2 text-sm font-bold text-white disabled:cursor-not-allowed disabled:opacity-50"
              >
                {savingAction ? "Đang lưu..." : action.kind === "extend" ? "Xác nhận gia hạn" : "Xác nhận kết thúc / Trả phòng"}
              </button>
            </div>
          </section>
        </div>
      )}
      {toast && <Toast message={toast} onClose={() => setToast("")} />}
    </ManagementPage>
  )
}

function OutstandingInvoice({ invoice }: { invoice: InvoiceDto }) {
  const prepaidRentApplied = invoice.prepaidRentApplied ?? 0
  const grossTotal = invoice.total + prepaidRentApplied
  const netPayable = invoice.netPayable ?? invoice.total
  const amountPaid = invoice.amountPaid ?? 0
  const remainingPayable = remainingAmount(invoice)
  const displayStatus = invoice.displayStatus ?? invoice.status

  return (
    <li className="rounded-xl border border-slate-100 p-4 text-sm">
      <div className="flex flex-wrap justify-between gap-2">
        <strong>Hóa đơn #{invoice.invoiceId} · Tháng {formatMonth(invoice.invoiceMonth)}</strong>
        <span className={displayStatus === "Overdue" ? "font-semibold text-rose-700" : "text-amber-700"}>
          {displayStatus === "Overdue" ? "Quá hạn" : "Chưa thanh toán"}
        </span>
      </div>
      <p className="mt-1 text-xs text-slate-500">Hạn thanh toán: {formatContractDate(invoice.dueDate)}</p>
      <div className="mt-3 grid gap-1 text-xs text-slate-600 sm:grid-cols-2">
        <span>Tổng khoản phí: {formatVnd(grossTotal)}</span>
        <span>Trả trước đã áp dụng: {formatVnd(prepaidRentApplied)}</span>
        <span>Cần thanh toán: {formatVnd(netPayable)}</span>
        <span>Đã thanh toán: {formatVnd(amountPaid)}</span>
        <strong className="sm:col-span-2">Còn phải trả: {formatVnd(remainingPayable)}</strong>
      </div>
    </li>
  )
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex justify-between gap-4">
      <dt className="text-slate-500">{label}</dt>
      <dd className="text-right font-semibold text-slate-800">{value}</dd>
    </div>
  )
}
