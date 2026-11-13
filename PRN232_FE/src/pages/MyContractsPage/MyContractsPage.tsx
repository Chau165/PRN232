import ContractMeterHandover from "@/components/customer/ContractMeterHandover"
import { useCallback, useEffect, useState, type ReactNode } from "react"
import { Link } from "react-router"
import CustomerPageShell from "@/components/customer/CustomerPageShell"
import StatusBadge from "@/components/customer/StatusBadge"
import { useAuth } from "@/auth/AuthContext"
import { getContractBillingTimeline, getContracts } from "@/services/contractService"
import type { ContractBillingTimelineDto, ContractDto } from "@/types/contract"
import type { InvoiceDto } from "@/types/invoice"
import { formatContractDate, localToday } from "@/utils/managementContracts"
import { formatMonth } from "@/utils/invoiceDisplay"
import { formatVnd } from "@/utils/publicFormatting"
import { routes } from "@/constants/routes"

type TimelineState = {
  loading: boolean
  error: string | null
  timeline: ContractBillingTimelineDto | null
}

export default function MyContractsPage() {
  const { accessToken } = useAuth()
  const [contracts, setContracts] = useState<ContractDto[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [retry, setRetry] = useState(0)
  const [expandedContractId, setExpandedContractId] = useState<number | null>(null)
  const [timelines, setTimelines] = useState<Record<number, TimelineState>>({})

  const loadContracts = useCallback(async (signal: AbortSignal) => {
    if (!accessToken) {
      setContracts([])
      setLoading(false)
      return
    }

    setLoading(true)
    setError(null)
    try {
      const contractRows = await getContracts(accessToken, signal)
      if (!signal.aborted) {
        setContracts(contractRows)
        setTimelines({})
      }
    } catch (cause) {
      if (!signal.aborted) {
        setError(cause instanceof Error ? cause.message : "Không thể tải hợp đồng.")
      }
    } finally {
      if (!signal.aborted) setLoading(false)
    }
  }, [accessToken])

  useEffect(() => {
    const controller = new AbortController()
    void loadContracts(controller.signal)
    return () => controller.abort()
  }, [loadContracts, retry])

  async function loadTimeline(contractId: number, force = false) {
    if (!accessToken) return
    const existingTimeline = timelines[contractId]
    if (!force && (existingTimeline?.loading || existingTimeline?.timeline)) return

    setTimelines((current) => ({
      ...current,
      [contractId]: { loading: true, error: null, timeline: null },
    }))

    try {
      const timeline = await getContractBillingTimeline(accessToken, contractId)
      setTimelines((current) => ({
        ...current,
        [contractId]: { loading: false, error: null, timeline },
      }))
    } catch (cause) {
      setTimelines((current) => ({
        ...current,
        [contractId]: {
          loading: false,
          error: cause instanceof Error ? cause.message : "Không thể tải lịch thanh toán.",
          timeline: null,
        },
      }))
    }
  }

  function toggleContract(contractId: number) {
    if (expandedContractId === contractId) {
      setExpandedContractId(null)
      return
    }

    setExpandedContractId(contractId)
    void loadTimeline(contractId)
  }

  function retryTimeline(contractId: number) {
    void loadTimeline(contractId, true)
  }

  return (
    <CustomerPageShell
      title="Hợp đồng của tôi"
      description="Xem hợp đồng sắp có hiệu lực, đang thuê, đã kết thúc và lịch hóa đơn thực tế."
    >
      {loading ? (
        <p role="status" className="rounded-xl bg-white p-6 text-slate-500">
          Đang tải hợp đồng...
        </p>
      ) : error ? (
        <div role="alert" className="rounded-xl bg-white p-6 text-rose-600">
          <p>{error}</p>
          <button
            type="button"
            onClick={() => setRetry((value) => value + 1)}
            className="mt-3 font-semibold"
          >
            Thử lại
          </button>
        </div>
      ) : contracts.length === 0 ? (
        <p role="status" className="rounded-xl border border-dashed border-slate-200 bg-white p-8 text-center text-slate-500">
          Bạn chưa có hợp đồng.
        </p>
      ) : (
        <div className="space-y-4">
          {contracts.map((contract) => (
            <ContractSummaryCard
              key={contract.contractId}
              contract={contract}
              expanded={expandedContractId === contract.contractId}
              timelineState={timelines[contract.contractId]}
              onToggle={() => toggleContract(contract.contractId)}
              onRetry={() => retryTimeline(contract.contractId)}
            />
          ))}
        </div>
      )}
    </CustomerPageShell>
  )
}

function ContractSummaryCard({
  contract,
  expanded,
  timelineState,
  onToggle,
  onRetry,
}: {
  contract: ContractDto
  expanded: boolean
  timelineState?: TimelineState
  onToggle: () => void
  onRetry: () => void
}) {
  const status = contract.displayStatus
  const tone = status === "Active"
    ? "green"
    : status === "Upcoming" || status === "ExpiredPendingCheckout"
      ? "amber"
      : "slate"
  const label = status === "Active"
    ? "Đang thuê"
    : status === "Upcoming"
      ? "Sắp có hiệu lực"
      : status === "ExpiredPendingCheckout"
        ? "Hết hạn — Chờ trả phòng"
        : status === "Terminated"
          ? "Đã kết thúc / Đã trả phòng"
          : "Đã kết thúc"

  return (
    <article className="rounded-2xl bg-white p-6 shadow-[0_3px_18px_rgba(16,65,67,.07)]">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p className="text-[12px] font-bold uppercase tracking-[.12em] text-[#087775]">
            Hợp đồng #{contract.contractId}
          </p>
          <h2 className="mt-2 font-display text-[22px] font-bold text-slate-900">
            {contract.propertyName}
          </h2>
          <p className="mt-1 text-[14px] text-slate-500">
            Phòng {contract.roomCode} · {contract.propertyAddress ?? "Địa chỉ chưa có"}
          </p>
        </div>
        <StatusBadge tone={tone}>{label}</StatusBadge>
      </div>

      <div className="mt-6 grid gap-4 border-t border-slate-100 pt-5 text-[14px] sm:grid-cols-2 lg:grid-cols-4">
        <SummaryValue label="Bắt đầu" value={formatContractDate(contract.startDate)} />
        <SummaryValue label="Kết thúc" value={formatContractDate(contract.endDate)} />
        <SummaryValue label="Giá thuê / tháng" value={formatVnd(contract.monthlyRent)} emphasized />
        <SummaryValue label="Đã trả trước" value={formatVnd(contract.prepaidRentAmount)} emphasized />
      </div>

      <button
        type="button"
        aria-expanded={expanded}
        onClick={onToggle}
        className="mt-5 rounded-lg bg-[#087775] px-4 py-2.5 text-sm font-bold text-white"
      >
        {expanded ? "Thu gọn chi tiết" : "Xem chi tiết và lịch thanh toán"}
      </button>

      {expanded && (
        <ContractBillingTimeline
          contract={contract}
          state={timelineState}
          onRetry={onRetry}
        />
      )}
    </article>
  )
}

function ContractBillingTimeline({
  contract,
  state,
  onRetry,
}: {
  contract: ContractDto
  state?: TimelineState
  onRetry: () => void
}) {
  if (!state || state.loading) {
    return <p role="status" className="mt-6 border-t border-slate-100 pt-5 text-sm text-slate-500">Đang tải chi tiết và lịch hóa đơn...</p>
  }

  if (state.error || !state.timeline) {
    return (
      <div role="alert" className="mt-6 border-t border-slate-100 pt-5 text-sm text-rose-600">
        <p>{state.error ?? "Không tải được lịch hóa đơn."}</p>
        <button type="button" onClick={onRetry} className="mt-2 font-semibold underline">
          Thử lại
        </button>
      </div>
    )
  }

  const timeline = state.timeline
  const todayMonth = localToday().slice(0, 7)
  const pastInvoices = timeline.issuedInvoices.filter(
    (invoice) => invoice.invoiceMonth.slice(0, 7) < todayMonth,
  )
  const currentInvoice = timeline.issuedInvoices.find(
    (invoice) => invoice.invoiceMonth.slice(0, 7) === todayMonth,
  )
  const futureInvoices = timeline.issuedInvoices.filter(
    (invoice) => invoice.invoiceMonth.slice(0, 7) > todayMonth,
  )
  const plannedBillingPeriods = contract.status === "Terminated" ? [] : timeline.plannedBillingPeriods
  const currentPlanned = plannedBillingPeriods.find(
    (period) => period.invoiceMonth.slice(0, 7) === todayMonth,
  )
  const futurePlanned = plannedBillingPeriods.filter(
    (period) => period.invoiceMonth.slice(0, 7) > todayMonth,
  )

  return (
    <section className="mt-6 space-y-7 border-t border-slate-100 pt-6" aria-label={`Chi tiết hợp đồng ${contract.contractId}`}>
      <section aria-labelledby={`contract-detail-${contract.contractId}`}>
        <h3 id={`contract-detail-${contract.contractId}`} className="font-display text-lg font-bold text-slate-900">
          Thông tin hợp đồng
        </h3>
        <dl className="mt-4 grid gap-x-6 gap-y-3 text-sm sm:grid-cols-2">
          <DetailValue label="Khách thuê" value={contract.customerName} />
          <DetailValue label="Bên cho thuê / chủ sở hữu" value={contract.lessorName ?? "Chưa có thông tin lưu trên hợp đồng"} />
          <DetailValue label="Địa chỉ bên cho thuê" value={contract.lessorAddress ?? "Chưa có thông tin lưu trên hợp đồng"} />
          <DetailValue label="Người lập hồ sơ" value={contract.processedByStaffName ?? "Không có dữ liệu"} />
          <DetailValue label="Trạng thái lưu trong hệ thống" value={contract.status} />
          <DetailValue label="Số tiền thuê trả trước" value={formatVnd(contract.prepaidRentAmount)} />
          <DetailValue label="Đã phân bổ vào hóa đơn" value={formatVnd(contract.prepaidRentApplied)} />
          <DetailValue label="Số dư trả trước" value={formatVnd(Math.max(0, contract.remainingPrepaidCredit))} />
        </dl>
        <ContractMeterHandover contract={contract} />
        <p className="mt-4 rounded-lg bg-amber-50 px-4 py-3 text-xs text-amber-900">
          Chữ ký điện tử/chữ ký xác nhận chưa được lưu trong dữ liệu hợp đồng; trạng thái trên không xác nhận hợp đồng đã ký.
        </p>
      </section>

      <section aria-labelledby={`billing-title-${contract.contractId}`}>
        <h3 id={`billing-title-${contract.contractId}`} className="font-display text-lg font-bold text-slate-900">
          Lịch hóa đơn và thanh toán
        </h3>
        <div className="mt-4 space-y-5">
          <BillingGroup title="Hóa đơn các tháng trước" emptyText="Chưa có hóa đơn đã phát hành cho các tháng trước.">
            {pastInvoices.map((invoice) => (
              <IssuedInvoiceItem key={invoice.invoiceId} invoice={invoice} />
            ))}
          </BillingGroup>

          <BillingGroup title="Hóa đơn tháng hiện tại" emptyText="Chưa có hóa đơn tháng này; Manager chưa phát hành nên chưa thể thanh toán.">
            {currentInvoice ? (
              <IssuedInvoiceItem invoice={currentInvoice} />
            ) : currentPlanned ? (
              <PlannedPeriodItem period={currentPlanned} />
            ) : (
              <p className="rounded-lg bg-slate-50 px-4 py-3 text-sm text-slate-500">
                Hợp đồng không có kỳ thanh toán trong tháng hiện tại.
              </p>
            )}
          </BillingGroup>

          {futureInvoices.length > 0 && (
            <BillingGroup title="Hóa đơn đã phát hành cho kỳ tương lai" emptyText="">
              {futureInvoices.map((invoice) => (
                <IssuedInvoiceItem key={invoice.invoiceId} invoice={invoice} />
              ))}
            </BillingGroup>
          )}

          <BillingGroup title="Các kỳ thanh toán dự kiến" emptyText="Không còn kỳ thanh toán chưa phát hành trong thời hạn hợp đồng.">
            {futurePlanned.map((period) => (
              <PlannedPeriodItem key={period.invoiceMonth} period={period} />
            ))}
          </BillingGroup>
          {timeline.hasMorePlannedPeriods && (
            <p className="text-xs text-slate-500">
              Danh sách kỳ dự kiến đã được giới hạn; hãy liên hệ hỗ trợ để xem phần thời hạn còn lại.
            </p>
          )}
        </div>
      </section>
    </section>
  )
}

function BillingGroup({
  title,
  emptyText,
  children,
}: {
  title: string
  emptyText: string
  children: ReactNode
}) {
  const hasItems = Array.isArray(children) ? children.length > 0 : children !== null
  return (
    <section>
      <h4 className="mb-2 text-sm font-bold text-slate-700">{title}</h4>
      {hasItems ? (
        <div className="space-y-2">{children}</div>
      ) : (
        <p className="rounded-lg bg-slate-50 px-4 py-3 text-sm text-slate-500">{emptyText}</p>
      )}
    </section>
  )
}

function IssuedInvoiceItem({ invoice }: { invoice: InvoiceDto }) {
  const displayStatus = invoice.displayStatus ?? invoice.status
  const tone = displayStatus === "Paid" ? "green" : displayStatus === "Overdue" ? "rose" : "amber"
  const netPayable = invoice.netPayable ?? invoice.total
  const remainingPayable = invoice.remainingPayable ?? netPayable

  return (
    <article className="rounded-xl border border-slate-100 p-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p className="font-semibold text-slate-800">Tháng {formatMonth(invoice.invoiceMonth)} · Hóa đơn #{invoice.invoiceId}</p>
          <p className="mt-1 text-xs text-slate-500">Hạn thanh toán: {formatContractDate(invoice.dueDate)}</p>
        </div>
        <StatusBadge tone={tone}>{displayStatus === "Paid" ? "Đã thanh toán" : displayStatus === "Overdue" ? "Quá hạn" : "Chưa thanh toán"}</StatusBadge>
      </div>
      <div className="mt-4 grid gap-2 text-sm sm:grid-cols-2">
        <MoneyValue label="Tổng hóa đơn" amount={invoice.total} />
        <MoneyValue label="Tiền thuê trả trước đã trừ" amount={invoice.prepaidRentApplied ?? 0} />
        <MoneyValue label="Số tiền phải trả sau khấu trừ" amount={netPayable} />
        <MoneyValue label="Còn phải trả" amount={remainingPayable} emphasized />
      </div>
      <p className="mt-3 text-xs text-slate-500">
        {invoice.paymentMethod ? `Phương thức thanh toán: ${invoice.paymentMethod}` : "Phương thức thanh toán: chưa có dữ liệu"}
        {invoice.paymentDateUtc ? ` · ${new Date(invoice.paymentDateUtc).toLocaleString("vi-VN")}` : ""}
      </p>
      {invoice.lines.length > 0 && (
        <ul className="mt-3 space-y-1 border-t border-slate-100 pt-3 text-xs text-slate-600">
          {invoice.lines.map((line, index) => (
            <li key={`${invoice.invoiceId}-${index}`} className="flex justify-between gap-3">
              <span>{line.description}</span>
              <span className="shrink-0">{formatVnd(line.amount)}</span>
            </li>
          ))}
        </ul>
      )}
      <div className="mt-4 flex flex-wrap gap-2">
        <Link to={routes.myBills} className="rounded-lg bg-slate-100 px-3 py-2 text-xs font-bold text-slate-700">
          Xem hóa đơn
        </Link>
        {displayStatus !== "Paid" && remainingPayable > 0 && (
          <Link to={routes.myBills} className="rounded-lg bg-[#087775] px-3 py-2 text-xs font-bold text-white">
            Thanh toán
          </Link>
        )}
        <Link to={routes.myBills} className="rounded-lg border border-[#087775] px-3 py-2 text-xs font-bold text-[#087775]">
          Khiếu nại
        </Link>
      </div>
    </article>
  )
}

function PlannedPeriodItem({ period }: { period: ContractBillingTimelineDto["plannedBillingPeriods"][number] }) {
  return (
    <article className="rounded-xl border border-dashed border-slate-200 bg-slate-50 p-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <p className="font-semibold text-slate-700">Kỳ dự kiến tháng {formatMonth(period.invoiceMonth)}</p>
          <p className="mt-1 text-xs text-slate-500">Tiền phòng cơ bản: {formatVnd(period.monthlyRent)}</p>
        </div>
        <StatusBadge tone="slate">Chưa phát hành</StatusBadge>
      </div>
      <p className="mt-3 text-xs text-slate-500">Tổng hóa đơn: chưa xác định · Điện nước/dịch vụ: {period.utilitiesStatus}.</p>
      {period.note && <p className="mt-2 text-xs text-amber-800">{period.note}</p>}
      <p className="mt-2 text-xs text-slate-500">Kỳ dự kiến chưa có mã hóa đơn và chưa thể thanh toán.</p>
    </article>
  )
}

function SummaryValue({ label, value, emphasized = false }: { label: string; value: string; emphasized?: boolean }) {
  return (
    <div>
      <p className="text-slate-400">{label}</p>
      <p className={`mt-1 font-semibold ${emphasized ? "text-[#087775]" : "text-slate-700"}`}>{value}</p>
    </div>
  )
}

function DetailValue({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-slate-400">{label}</dt>
      <dd className="mt-1 font-semibold text-slate-700">{value}</dd>
    </div>
  )
}

function MoneyValue({ label, amount, emphasized = false }: { label: string; amount: number; emphasized?: boolean }) {
  return (
    <p className="flex justify-between gap-3 text-slate-500">
      <span>{label}</span>
      <strong className={emphasized ? "text-[#087775]" : "text-slate-700"}>{formatVnd(amount)}</strong>
    </p>
  )
}
