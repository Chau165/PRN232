import { useCallback, useEffect, useRef, useState } from "react"
import { useLocation, useSearchParams } from "react-router"
import CustomerPageShell from "@/components/customer/CustomerPageShell"
import CustomerRoomDrawer from "@/components/customer/CustomerRoomDrawer"
import MaintenanceDetailDrawer from "@/components/customer/MaintenanceDetailDrawer"
import ReportDamageModal from "@/components/customer/ReportDamageModal"
import StatusBadge from "@/components/customer/StatusBadge"
import Toast from "@/components/customer/Toast"
import Icon from "@/components/common/Icon"
import { useAuth } from "@/auth/AuthContext"
import { useCustomerNotificationsState } from "@/auth/CustomerNotificationsContext"
import { createMaintenanceRequest, getMaintenanceLogs, getMaintenanceRequest, getMaintenanceRequests } from "@/services/maintenanceService"
import { getCurrentRentals, getRentalHistory } from "@/services/rentalService"
import type { MaintenanceLogDto, MaintenanceRequestDto } from "@/types/maintenance"
import type { Rental } from "@/types/rental"

type Tab = "current" | "history" | "maintenance"

export default function MyRoomPage() {
  const { accessToken } = useAuth()
  const location = useLocation()
  const notificationState = useCustomerNotificationsState()
  const [, setSearchParams] = useSearchParams()
  const [activeTab, setActiveTab] = useState<Tab>(new URLSearchParams(location.search).get("tab") === "maintenance" ? "maintenance" : "current")
  const [currentRentals, setCurrentRentals] = useState<Rental[]>([])
  const [history, setHistory] = useState<Rental[]>([])
  const [requests, setRequests] = useState<MaintenanceRequestDto[]>([])
  const [selectedRental, setSelectedRental] = useState<Rental | null>(null)
  const [reportRental, setReportRental] = useState<Rental | null>(null)
  const [selectedRequest, setSelectedRequest] = useState<MaintenanceRequestDto | null>(null)
  const [logs, setLogs] = useState<MaintenanceLogDto[]>([])
  const [rentalLoading, setRentalLoading] = useState(true)
  const [historyLoading, setHistoryLoading] = useState(false)
  const [requestsLoading, setRequestsLoading] = useState(false)
  const [detailLoading, setDetailLoading] = useState(false)
  const [submitPending, setSubmitPending] = useState(false)
  const [rentalError, setRentalError] = useState<string | null>(null)
  const [historyError, setHistoryError] = useState<string | null>(null)
  const [requestsError, setRequestsError] = useState<string | null>(null)
  const [detailError, setDetailError] = useState<string | null>(null)
  const [submitError, setSubmitError] = useState<string | null>(null)
  const [toast, setToast] = useState("")
  const [dataOwnerToken, setDataOwnerToken] = useState<string | null>(null)
  const [detailReload, setDetailReload] = useState(0)
  const [historyRetry, setHistoryRetry] = useState(0)
  const [requestsRetry, setRequestsRetry] = useState(0)
  const [historyLoaded, setHistoryLoaded] = useState(false)
  const [requestsLoaded, setRequestsLoaded] = useState(false)
  const [requestsPending, setRequestsPending] = useState(false)
  const requestVersion = useRef(0)
  const detailVersion = useRef(0)
  const submitLock = useRef(false)
  const ownerToken = useRef(accessToken)
  ownerToken.current = accessToken
  const initializedToken = useRef<string | null>(null)

  const refreshRentals = useCallback(async (signal?: AbortSignal) => {
    if (!accessToken) return
    setRentalLoading(true)
    setRentalError(null)
    try {
      const items = await getCurrentRentals(accessToken, signal)
      if (!signal?.aborted && ownerToken.current === accessToken) {
        setCurrentRentals(items)
      }
    } catch (error) {
      if (!signal?.aborted && ownerToken.current === accessToken) setRentalError(error instanceof Error ? error.message : "Không thể tải phòng đang thuê.")
    } finally {
      if (!signal?.aborted && ownerToken.current === accessToken) {
        setDataOwnerToken(accessToken)
        setRentalLoading(false)
      }
    }
  }, [accessToken])

  const refreshHistory = useCallback(async (signal?: AbortSignal) => {
    if (!accessToken) return
    setHistoryLoading(true)
    setHistoryError(null)
    try {
      const items = await getRentalHistory(accessToken, signal)
      if (!signal?.aborted && ownerToken.current === accessToken) {
        setHistory(items)
        setHistoryLoaded(true)
      }
    } catch (error) {
      if (!signal?.aborted && ownerToken.current === accessToken) {
        setHistoryLoaded(true)
        setHistoryError(error instanceof Error ? error.message : "Không thể tải lịch sử thuê.")
      }
    } finally {
      if (!signal?.aborted && ownerToken.current === accessToken) setHistoryLoading(false)
    }
  }, [accessToken])

  const refreshRequests = useCallback(async (signal?: AbortSignal) => {
    if (!accessToken) return
    const version = ++requestVersion.current
    setRequestsPending(true)
    setRequestsLoading(true)
    setRequestsError(null)
    try {
      const items = await getMaintenanceRequests(accessToken, signal)
      if (!signal?.aborted && version === requestVersion.current && ownerToken.current === accessToken) {
        setRequests([...items].sort((a, b) => b.requestId - a.requestId))
        setRequestsLoaded(true)
      }
    } catch (error) {
      if (!signal?.aborted && version === requestVersion.current && ownerToken.current === accessToken) {
        setRequestsLoaded(true)
        setRequestsError(error instanceof Error ? error.message : "Không thể tải yêu cầu sửa chữa.")
      }
    } finally {
      if (!signal?.aborted && version === requestVersion.current && ownerToken.current === accessToken) {
        setRequestsPending(false)
        setRequestsLoading(false)
      }
    }
  }, [accessToken])

  useEffect(() => {
    if (new URLSearchParams(location.search).get("tab") === "maintenance") setActiveTab("maintenance")
  }, [location.search])

  useEffect(() => {
    if (initializedToken.current !== accessToken) {
      initializedToken.current = accessToken
      ownerToken.current = accessToken
      setCurrentRentals([])
      setHistory([])
      setRequests([])
      setHistoryLoaded(false)
      setRequestsLoaded(false)
      setRequestsPending(false)
      setDataOwnerToken(null)
      setSelectedRental(null)
      setReportRental(null)
      setSelectedRequest(null)
      setLogs([])
      setSubmitPending(false)
      submitLock.current = false
      setSubmitError(null)
      setRentalError(null)
      setHistoryError(null)
      setRequestsError(null)
      setDetailError(null)
      requestVersion.current++
      detailVersion.current++
    }
    if (!accessToken) return
    const controller = new AbortController()
    void refreshRentals(controller.signal)
    return () => controller.abort()
  }, [accessToken, refreshRentals])

  useEffect(() => {
    if (!accessToken || activeTab !== "history" || dataOwnerToken !== accessToken) return
    const controller = new AbortController()
    void refreshHistory(controller.signal)
    const refreshOnFocus = () => { if (document.visibilityState === "visible") void refreshHistory() }
    const refreshOnVisibility = () => { if (document.visibilityState === "visible") void refreshHistory() }
    const timer = window.setInterval(() => { if (document.visibilityState === "visible") void refreshHistory() }, 30_000)
    window.addEventListener("focus", refreshOnFocus)
    document.addEventListener("visibilitychange", refreshOnVisibility)
    return () => {
      controller.abort()
      window.clearInterval(timer)
      window.removeEventListener("focus", refreshOnFocus)
      document.removeEventListener("visibilitychange", refreshOnVisibility)
    }
  }, [accessToken, activeTab, refreshHistory, dataOwnerToken, historyRetry])

  useEffect(() => {
    if (!accessToken || activeTab !== "maintenance" || dataOwnerToken !== accessToken) return
    const controller = new AbortController()
    void refreshRequests(controller.signal)
    const refreshOnFocus = () => {
      if (document.visibilityState === "visible") {
        void refreshRequests()
      }
    }
    const refreshOnVisibility = () => {
      if (document.visibilityState === "visible") {
        void refreshRequests()
      }
    }
    const timer = window.setInterval(() => {
      if (document.visibilityState === "visible") {
        void refreshRequests()
      }
    }, 30_000)
    window.addEventListener("focus", refreshOnFocus)
    document.addEventListener("visibilitychange", refreshOnVisibility)
    return () => {
      controller.abort()
      requestVersion.current++
      window.clearInterval(timer)
      window.removeEventListener("focus", refreshOnFocus)
      document.removeEventListener("visibilitychange", refreshOnVisibility)
    }
  }, [accessToken, activeTab, refreshRequests, dataOwnerToken, requestsRetry])

  useEffect(() => {
    const requestId = selectedRequest?.requestId
    if (!accessToken || activeTab !== "maintenance" || dataOwnerToken !== accessToken || requestId === undefined) return
    const controller = new AbortController()
    const loadDetail = async () => {
      const version = ++detailVersion.current
      setDetailLoading(true)
      setDetailError(null)
      try {
        const [detail, historyLogs] = await Promise.all([
          getMaintenanceRequest(accessToken, requestId, controller.signal),
          getMaintenanceLogs(accessToken, requestId, controller.signal),
        ])
        if (!controller.signal.aborted && version === detailVersion.current && ownerToken.current === accessToken) {
          setSelectedRequest(detail)
          setLogs(historyLogs)
        }
      } catch (error) {
        if (!controller.signal.aborted && version === detailVersion.current && ownerToken.current === accessToken) {
          setDetailError(error instanceof Error ? error.message : "Không thể tải chi tiết yêu cầu.")
        }
      } finally {
        if (!controller.signal.aborted && version === detailVersion.current && ownerToken.current === accessToken) setDetailLoading(false)
      }
    }
    void loadDetail()
    const refreshOnFocus = () => { if (document.visibilityState === "visible") void loadDetail() }
    const refreshOnVisibility = () => { if (document.visibilityState === "visible") void loadDetail() }
    const timer = window.setInterval(() => { if (document.visibilityState === "visible") void loadDetail() }, 30_000)
    window.addEventListener("focus", refreshOnFocus)
    document.addEventListener("visibilitychange", refreshOnVisibility)
    return () => {
      controller.abort()
      detailVersion.current++
      window.clearInterval(timer)
      window.removeEventListener("focus", refreshOnFocus)
      document.removeEventListener("visibilitychange", refreshOnVisibility)
    }
  }, [accessToken, activeTab, dataOwnerToken, selectedRequest?.requestId, detailReload])

  function selectTab(tab: Tab) {
    setActiveTab(tab)
    setSearchParams(tab === "maintenance" ? { tab: "maintenance" } : {})
  }

  function openRequest(item: MaintenanceRequestDto) {
    if (!accessToken) return
    setSelectedRequest(item)
    setLogs([])
    setDetailError(null)
    setDetailLoading(true)
    void refreshRequests()
  }

  async function submitReport(form: { equipmentId: number | null; description: string }) {
    if (!accessToken || !reportRental || submitLock.current) return
    submitLock.current = true
    setSubmitPending(true)
    setSubmitError(null)
    try {
      const created = await createMaintenanceRequest(accessToken, {
        contractId: reportRental.contractId,
        equipmentId: form.equipmentId,
        description: form.description.trim(),
      })
      if (ownerToken.current !== accessToken) return
      setReportRental(null)
      selectTab("maintenance")
      setRequests((current) => [created, ...current.filter((item) => item.requestId !== created.requestId)])
      void notificationState.refresh()
      setToast("Đã gửi yêu cầu sửa chữa thành công")
    } catch (error) {
      if (ownerToken.current === accessToken) {
        setSubmitError(error instanceof Error ? error.message : "Không thể gửi yêu cầu. Vui lòng thử lại.")
      }
    } finally {
      submitLock.current = false
      if (ownerToken.current === accessToken) setSubmitPending(false)
    }
  }

  return (
    <CustomerPageShell title="Phòng của tôi" description="Quản lý phòng đang thuê, lịch sử thuê và các yêu cầu hỗ trợ của bạn.">
      <div className="flex flex-wrap gap-2 border-b border-slate-100">
        {([ ["current", "Đang thuê"], ["history", "Lịch sử thuê"], ["maintenance", "Yêu cầu sửa chữa"] ] as const).map(([value, label]) => (
          <button key={value} onClick={() => selectTab(value)} className={`border-b-2 px-3 pb-3 text-[14px] font-semibold ${activeTab === value ? "border-[#087775] text-[#087775]" : "border-transparent text-slate-400 hover:text-slate-700"}`}>{label}</button>
        ))}
      </div>

      {activeTab === "current" && <section className="mt-7 space-y-4">
        {rentalLoading || dataOwnerToken !== accessToken ? <p role="status" className="rounded-xl bg-white p-6 text-slate-500">Đang tải phòng thuê...</p> : rentalError ? <ErrorState message={rentalError} retry={() => void refreshRentals()} /> : currentRentals.length === 0 ? <EmptyState message="Bạn chưa có phòng đang thuê." /> : currentRentals.map((rental) => (
          <article key={rental.contractId} className="rounded-2xl bg-white p-6 shadow-[0_5px_24px_rgba(16,65,67,.08)]">
            <div className="flex flex-col gap-6 lg:flex-row lg:items-start">
              {rental.primaryImageUrl && <img src={rental.primaryImageUrl} alt={`Phòng ${rental.roomCode}`} className="h-52 w-full rounded-xl object-cover lg:w-64" />}
              <div className="min-w-0 flex-1">
                <div className="flex flex-wrap items-start justify-between gap-3"><div><p className="text-[12px] font-bold uppercase tracking-[.12em] text-[#087775]">{rental.propertyName}</p><h2 className="mt-2 font-display text-[24px] font-bold text-slate-900">Phòng {rental.roomCode}</h2><p className="mt-1 flex items-center gap-1.5 text-[14px] text-slate-500"><Icon name="pin" size={15} />{rental.propertyAddress}</p></div><StatusBadge tone="green">ĐANG THUÊ</StatusBadge></div>
                <div className="mt-5 grid gap-4 text-[14px] sm:grid-cols-2"><div><p className="text-slate-400">Giá thuê</p><p className="mt-1 font-bold text-[#087775]">{formatCurrency(rental.monthlyRent)} / tháng</p></div><div><p className="text-slate-400">Hợp đồng</p><p className="mt-1 font-semibold text-slate-700">{formatDate(rental.startDate)} - {formatDate(rental.endDate)}</p></div></div>
                <div className="mt-5 flex flex-wrap gap-2">{rental.equipments.map((item) => <span key={item.equipmentId} className="room-amenity">{item.name}</span>)}</div>
                <div className="mt-6 flex flex-wrap gap-3"><button onClick={() => setSelectedRental(rental)} className="rounded-lg bg-[#087775] px-4 py-3 text-[13px] font-bold text-white shadow-sm hover:bg-[#066a68]">Xem chi tiết</button><button onClick={() => { setReportRental(rental); setSubmitError(null) }} className="rounded-lg bg-teal-50 px-4 py-3 text-[13px] font-bold text-[#087775] hover:bg-teal-100">Báo hư hỏng</button></div>
              </div>
            </div>
          </article>
        ))}
        {rentalError && <button onClick={() => void refreshRentals()} className="font-semibold text-[#087775]">Thử tải lại</button>}
      </section>}

      {activeTab === "history" && <section className="mt-7 space-y-4">
        {historyLoading || dataOwnerToken !== accessToken || !historyLoaded && !historyError ? <p role="status" className="rounded-xl bg-white p-6 text-slate-500">Đang tải lịch sử thuê...</p> : historyError ? <ErrorState message={historyError} retry={() => setHistoryRetry((value) => value + 1)} /> : history.length === 0 ? <EmptyState message="Chưa có hợp đồng thuê đã hết hạn." /> : history.map((rental) => <article key={rental.contractId} className="rounded-2xl bg-white p-6 shadow-[0_3px_18px_rgba(16,65,67,.07)]"><div className="flex flex-wrap items-start justify-between gap-3"><div><p className="text-[12px] font-bold uppercase tracking-[.12em] text-[#087775]">{rental.propertyName}</p><h2 className="mt-2 font-display text-[22px] font-bold text-slate-900">Phòng {rental.roomCode}</h2><p className="mt-1 text-[14px] text-slate-500">{rental.propertyAddress}</p></div><StatusBadge tone="slate">ĐÃ KẾT THÚC</StatusBadge></div><div className="mt-5 grid gap-4 border-t border-slate-100 pt-4 text-[14px] sm:grid-cols-3"><p><span className="text-slate-400">Thời gian:</span><br /><strong>{formatDate(rental.startDate)} - {formatDate(rental.endDate)}</strong></p><p><span className="text-slate-400">Giá thuê:</span><br /><strong>{formatCurrency(rental.monthlyRent)}</strong></p><p><span className="text-slate-400">Hợp đồng:</span><br /><strong>#{rental.contractId}</strong></p></div></article>)}
      </section>}

      {activeTab === "maintenance" && <section className="mt-7 space-y-4">
        {dataOwnerToken !== accessToken || !requestsLoaded && !requestsError ? <p role="status" className="rounded-xl bg-white p-6 text-slate-500">Đang tải yêu cầu sửa chữa...</p> : requestsError && requests.length === 0 ? <ErrorState message={requestsError} retry={() => setRequestsRetry((value) => value + 1)} /> : requests.length === 0 ? <EmptyState message="Bạn chưa gửi yêu cầu sửa chữa nào." /> : requests.map((request) => <button key={request.requestId} onClick={() => openRequest(request)} className="w-full rounded-2xl bg-white p-5 text-left shadow-[0_3px_18px_rgba(16,65,67,.07)] transition hover:shadow-[0_8px_28px_rgba(15,54,60,.1)]"><div className="flex flex-wrap items-start justify-between gap-3"><div><p className="text-[12px] font-bold uppercase tracking-[.12em] text-[#087775]">#{request.requestId} · Phòng {request.roomCode ?? "—"}</p><h3 className="mt-2 text-[16px] font-bold text-slate-800">{request.equipmentName ?? "Báo hỏng chung"}</h3><p className="mt-1 line-clamp-2 text-[13px] text-slate-500">{request.description}</p>{request.assignedStaffName && <p className="mt-1 text-[12px] text-slate-500">Phụ trách: {request.assignedStaffName}</p>}</div><StatusBadge tone={request.status === "Completed" ? "green" : request.status === "Approved" ? "teal" : "amber"}>{statusLabel(request.status)}</StatusBadge></div></button>)}
        {requestsError && requests.length > 0 && <ErrorState message={requestsError} retry={() => setRequestsRetry((value) => value + 1)} />}
        <button onClick={() => void refreshRequests()} disabled={requestsPending} className="text-[13px] font-semibold text-[#087775] disabled:opacity-50">{requestsPending ? "Đang cập nhật..." : "Cập nhật danh sách"}</button>
      </section>}

      {dataOwnerToken === accessToken && selectedRental && <CustomerRoomDrawer room={selectedRental} onClose={() => setSelectedRental(null)} />}
      {dataOwnerToken === accessToken && reportRental && <ReportDamageModal roomCode={reportRental.roomCode} equipment={reportRental.equipments} pending={submitPending} error={submitError} onClose={() => { setReportRental(null); setSubmitError(null) }} onSubmit={submitReport} />}
      {submitError && !reportRental && <div role="alert" className="fixed bottom-6 right-6 z-[90] max-w-sm rounded-xl bg-rose-50 px-4 py-3 text-[13px] text-rose-700 shadow-xl">{submitError}</div>}
      {dataOwnerToken === accessToken && selectedRequest && <MaintenanceDetailDrawer request={selectedRequest} logs={logs} loading={detailLoading} error={detailError} onRetry={() => setDetailReload((value) => value + 1)} onClose={() => { detailVersion.current++; setSelectedRequest(null) }} />}
      {toast && <Toast message={toast} onClose={() => setToast("")} />}
    </CustomerPageShell>
  )
}

function EmptyState({ message }: { message: string }) {
  return <div role="status" className="rounded-2xl border border-dashed border-slate-200 bg-white p-10 text-center text-[14px] text-slate-500">{message}</div>
}

function ErrorState({ message, retry }: { message: string; retry: () => void }) {
  return <div role="alert" className="rounded-2xl border border-rose-100 bg-white p-6 text-[14px] text-rose-700"><p>{message}</p><button onClick={retry} className="mt-3 font-semibold">Thử lại</button></div>
}

function formatCurrency(amount: number) {
  return new Intl.NumberFormat("vi-VN", { style: "currency", currency: "VND" }).format(amount)
}

function formatDate(value: string) {
  return new Date(`${value}T00:00:00`).toLocaleDateString("vi-VN")
}

function statusLabel(status: MaintenanceRequestDto["status"]) {
  return status === "Pending" ? "CHỜ DUYỆT" : status === "Approved" ? "ĐÃ DUYỆT · ĐÃ PHÂN CÔNG" : "HOÀN THÀNH"
}
