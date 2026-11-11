import { useEffect, useState } from "react"
import { useAuth } from "@/auth/AuthContext"
import { ApiError } from "@/api/apiClient"
import ManagementPage from "@/components/management/ManagementPage"
import ManagementStatCard from "@/components/management/ManagementStatCard"
import ManagementRevenueChart from "@/components/management/ManagementRevenueChart"
import { getManagementReports, type ManagementReportsData } from "@/services/managementReportsService"
import { formatContractDate } from "@/utils/managementContracts"
import { formatVnd } from "@/utils/publicFormatting"

type ReportsState =
  | { status: "loading" }
  | { status: "error"; message: string }
  | { status: "success"; data: ManagementReportsData }

function formatRevenue(amount: number) {
  return amount >= 1_000_000
    ? `${(amount / 1_000_000).toLocaleString("vi-VN", { maximumFractionDigits: 2 })} triệu`
    : formatVnd(amount)
}

export default function ManagementReportsPage() {
  const { accessToken } = useAuth()
  const [state, setState] = useState<ReportsState>({ status: "loading" })
  const [reload, setReload] = useState(0)
  useEffect(() => {
    if (!accessToken) return
    const controller = new AbortController()
    setState({ status: "loading" })
    getManagementReports(accessToken, controller.signal).then(
      (data) => { if (!controller.signal.aborted) setState({ status: "success", data }) },
      (error: unknown) => {
        if (!controller.signal.aborted) setState({
          status: "error",
          message: error instanceof ApiError && error.status === 403
            ? "Bạn không có quyền xem báo cáo."
            : "Không thể tải báo cáo. Vui lòng kiểm tra kết nối và thử lại.",
        })
      },
    )
    return () => controller.abort()
  }, [accessToken, reload])
  const data = state.status === "success" ? state.data : null
  return (
    <ManagementPage title="Báo cáo" description="Báo cáo toàn hệ thống từ dữ liệu API backend.">
      {state.status === "loading" && (
        <div role="status" aria-live="polite" aria-busy="true">
          <p className="mb-4 text-sm text-slate-500">Đang tải báo cáo...</p>
          <div aria-hidden="true" className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            {Array.from({ length: 4 }, (_, index) => <div key={index} className="h-40 animate-pulse rounded-2xl bg-slate-100 motion-reduce:animate-none" />)}
          </div>
        </div>
      )}
      {state.status === "error" && (
        <div role="alert" className="rounded-xl bg-rose-50 p-5 text-sm text-rose-700">
          <p>{state.message}</p>
          <button type="button" onClick={() => setReload((value) => value + 1)} className="mt-3 rounded-lg border border-rose-200 px-4 py-2 font-semibold focus-visible:outline-2 focus-visible:outline-offset-2">
            Tải lại báo cáo
          </button>
        </div>
      )}
      {data && <>
        <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
          <p className="text-sm text-slate-500">Kỳ hiện tại: {data.period} · Ngày theo giờ Việt Nam</p>
          <button type="button" onClick={() => setReload((value) => value + 1)} className="rounded-lg border border-slate-200 bg-white px-4 py-2 text-sm font-semibold text-slate-600 hover:bg-slate-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-teal-700">
            Làm mới báo cáo
          </button>
        </div>
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          <ManagementStatCard label="Doanh thu tháng" value={formatRevenue(data.monthlyRevenue)} hint={`Tiền đã thu cho kỳ ${data.period}`} />
          <ManagementStatCard label="Tỷ lệ lấp đầy" value={`${data.occupancyRate.toLocaleString("vi-VN", { maximumFractionDigits: 1 })}%`} hint={`${data.occupiedRooms}/${data.totalRooms} phòng đang thuê`} tone="green" />
          <ManagementStatCard label="Hóa đơn còn nợ" value={data.unpaidInvoices.toLocaleString("vi-VN")} hint="Chưa thanh toán hoặc quá hạn" tone="amber" />
          <ManagementStatCard label="Bảo trì hoàn thành" value={data.completedMaintenance.toLocaleString("vi-VN")} hint="Tổng yêu cầu đã hoàn thành, không giới hạn tháng" tone="slate" />
        </div>
        <div className="mt-6 grid gap-5 lg:grid-cols-2">
          <ManagementRevenueChart months={data.months} />
          <section className="min-w-0 rounded-2xl bg-white p-6 shadow-[0_3px_18px_rgba(16,65,67,.06)]">
            <h3 id="expiring-contracts" className="font-display text-[21px] font-bold text-slate-900">Hợp đồng sắp hết hạn</h3>
            <p className="mt-2 text-xs text-slate-500">Hợp đồng đang hiệu lực, hết hạn trong 30 ngày tới.</p>
            {data.expiringContracts.length === 0 ? <p className="mt-5 text-sm text-slate-500">Không có hợp đồng sắp hết hạn trong 30 ngày tới.</p> : (
              <div role="region" aria-labelledby="expiring-contracts" tabIndex={0} className="mt-5 max-h-96 space-y-3 overflow-y-auto focus-visible:outline-2 focus-visible:outline-teal-700">
                {data.expiringContracts.map((contract) => (
                  <div key={contract.contractId} className="flex flex-wrap items-center justify-between gap-3 rounded-xl bg-slate-50 p-4">
                    <div className="min-w-0">
                      <p className="break-words text-sm font-semibold text-slate-700">{contract.propertyName} · {contract.roomCode}</p>
                      <p className="mt-1 text-xs text-slate-500">{contract.customerName} · hết hạn {formatContractDate(contract.endDate)}</p>
                    </div>
                    <span className="text-xs font-bold text-amber-700">{contract.daysRemaining === 0 ? "Hết hạn hôm nay" : `${contract.daysRemaining} ngày`}</span>
                  </div>
                ))}
              </div>
            )}
          </section>
        </div>
      </>}
    </ManagementPage>
  )
}
