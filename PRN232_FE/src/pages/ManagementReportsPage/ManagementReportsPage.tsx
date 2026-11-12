import { useEffect, useState } from "react"
import { useAuth } from "@/auth/AuthContext"
import { ApiError } from "@/api/apiClient"
import ManagementPage from "@/components/management/ManagementPage"
import ManagementStatCard from "@/components/management/ManagementStatCard"
import { getReceivablesReport, getRevenueReport, type ReceivablesReport, type RevenueReport } from "@/services/reportService"
import { formatVnd } from "@/utils/publicFormatting"

export default function ManagementReportsPage() {
  const { accessToken } = useAuth()
  const [month, setMonth] = useState(() => new Intl.DateTimeFormat("en-CA", { timeZone: "Asia/Ho_Chi_Minh", year: "numeric", month: "2-digit" }).format(new Date()).slice(0, 7))
  const [data, setData] = useState<{ revenue: RevenueReport; debt: ReceivablesReport } | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [version, setVersion] = useState(0)
  useEffect(() => {
  if (!accessToken || !/^\d{4}-\d{2}$/.test(month)) return

  const controller = new AbortController()

  setLoading(true)
  setError(null)
  setData(null)

  async function loadReports() {
    try {
      const [revenue, debt] = await Promise.all([
        getRevenueReport(accessToken!, month, controller.signal),
        getReceivablesReport(accessToken!, controller.signal),
      ])

      if (controller.signal.aborted) return

      setData({ revenue, debt })
    } catch (failure: unknown) {
      if (controller.signal.aborted) return

      setError(
        failure instanceof ApiError && failure.status === 403
          ? "Bạn không có quyền xem báo cáo."
          : "Không thể tải báo cáo từ máy chủ. Vui lòng thử lại.",
      )
    } finally {
      if (!controller.signal.aborted) {
        setLoading(false)
      }
    }
  }

  void loadReports()

  return () => controller.abort()
}, [accessToken, month, version])
  return <ManagementPage title="Báo cáo" description="Doanh thu theo ngày nhận tiền và công nợ từ hóa đơn thực tế. Dữ liệu theo quyền quản lý hiện tại.">
    <div className="mb-6 flex flex-wrap items-end gap-3">
      <label className="text-sm font-semibold text-slate-700">Tháng doanh thu<input aria-label="Tháng doanh thu" type="month" value={month} onChange={event => { if (event.target.value) setMonth(event.target.value) }} className="ml-3 rounded-lg border border-slate-200 p-2" /></label>
      <button onClick={() => setVersion(value => value + 1)} disabled={loading} className="rounded-lg bg-[#087775] px-4 py-2 text-sm font-bold text-white disabled:opacity-50">Tải lại báo cáo</button>
    </div>
    {loading && <p role="status">Đang tải báo cáo...</p>}
    {error && <p role="alert" className="rounded-xl bg-rose-50 p-4 text-rose-700">{error}</p>}
    {data && <>
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <ManagementStatCard label="Doanh thu tháng" value={formatVnd(data.revenue.totalCollected)} hint={`${data.revenue.paymentCount} giao dịch đã ghi nhận`} />
        <ManagementStatCard label="Tổng công nợ" value={formatVnd(data.debt.outstandingAmount)} hint={`${data.debt.outstandingInvoiceCount} hóa đơn · đến ${data.debt.asOfDate}`} tone="amber" />
        <ManagementStatCard label="Nợ quá hạn" value={formatVnd(data.debt.overdueAmount)} hint={`${data.debt.overdueInvoiceCount} hóa đơn quá hạn`} tone="rose" />
        <ManagementStatCard label="Chưa xác định thời gian thu" value={formatVnd(data.revenue.unknownDateAmount)} hint={`${data.revenue.unknownDatePaymentCount} giao dịch cần kiểm tra, chưa phân bổ vào tháng`} tone="slate" />
      </div>
      <section className="mt-6 overflow-x-auto rounded-2xl bg-white p-5 shadow-sm">
        <h3 className="mb-4 text-lg font-bold text-slate-900">Doanh thu theo khu trọ · {month}</h3>
        <table className="w-full text-left text-sm"><thead><tr className="border-b border-slate-100 text-slate-500"><th className="p-3">Khu trọ</th><th className="p-3">Doanh thu</th><th className="p-3">Giao dịch</th><th className="p-3">Chưa xác định thời gian</th></tr></thead>
          <tbody>{data.revenue.byProperty.map(property => <tr key={property.propertyId} className="border-b border-slate-100"><td className="p-3">{property.propertyName}</td><td className="p-3 font-bold text-[#087775]">{formatVnd(property.collectedAmount)}</td><td className="p-3">{property.paymentCount}</td><td className="p-3">{formatVnd(property.unknownDateAmount)} · {property.unknownDatePaymentCount} giao dịch</td></tr>)}</tbody></table>
        {!data.revenue.byProperty.length && <p className="mt-4 text-sm text-slate-500">Chưa có doanh thu thuộc phạm vi được xem.</p>}
      </section>
    </>}
  </ManagementPage>

}
