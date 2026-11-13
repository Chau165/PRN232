import { useEffect, useState } from "react"
import { getDepositReport, type DepositReport } from "@/services/reportService"
import { formatVnd } from "@/utils/publicFormatting"

export default function DepositReportPanel({ token, month, version }: { token: string | null; month: string; version: number }) {
  const key = `${token}:${month}:${version}`
  const [state, setState] = useState<{ key: string; data?: DepositReport; error?: string } | null>(null)
  useEffect(() => {
    if (!token || !/^\d{4}-\d{2}$/.test(month)) return
    const controller = new AbortController()
    const timer = setTimeout(() => {
      controller.abort()
      setState({ key, error: "Đã hết thời gian tải báo cáo tiền cọc. Hãy tải lại báo cáo." })
    }, 15000)
    void getDepositReport(token, month, controller.signal).then(data => {
      if (!controller.signal.aborted && data) setState({ key, data })
    }).catch(() => {
      if (!controller.signal.aborted) setState({ key, error: "Không thể tải báo cáo tiền cọc. Hãy tải lại báo cáo." })
    }).finally(() => clearTimeout(timer))
    return () => { clearTimeout(timer); controller.abort() }
  }, [token, month, version, key])
  const data = state?.key === key ? state.data : undefined
  return <section className="mt-6 overflow-x-auto rounded-2xl bg-white p-5 shadow-sm">
    <h3 className="mb-2 text-lg font-bold text-slate-900">Tiền cọc thu / hoàn · {month}</h3>
    <p className="mb-4 text-sm text-slate-500">Dòng tiền trong tháng, tách riêng doanh thu hóa đơn. Thu trừ hoàn không phải số dư cọc còn phải trả; không bao gồm phí payout.</p>
    {state?.key === key && state.error && <p role="alert">{state.error}</p>}
    {!data && !(state?.key === key && state.error) && <p role="status">Đang tải tiền cọc...</p>}
    {data && <>
      <p className="mb-3 font-semibold">Đã thu: {formatVnd(data.totals.collectedAmount)} ({data.totals.collectionCount} giao dịch) · Đã hoàn: {formatVnd(data.totals.refundedAmount)} ({data.totals.refundCount} giao dịch) · Thu trừ hoàn: {formatVnd(data.totals.netCashFlow)}</p>
      <p className="mb-4 text-sm">Chưa rõ thời điểm, chưa phân bổ vào tháng: thu {formatVnd(data.unknownDate.collectedAmount)} ({data.unknownDate.collectionCount} giao dịch), hoàn {formatVnd(data.unknownDate.refundedAmount)} ({data.unknownDate.refundCount} giao dịch).</p>
      <table className="w-full text-left text-sm"><thead><tr><th className="p-3">Khu trọ</th><th className="p-3">Thu cọc</th><th className="p-3">Hoàn cọc</th><th className="p-3">Thu trừ hoàn</th><th className="p-3">Chưa rõ thời điểm</th></tr></thead>
        <tbody>{data.byProperty.map(p => <tr key={p.propertyId} className="border-t border-slate-100"><td className="p-3">{p.propertyName}</td><td className="p-3">{formatVnd(p.totals.collectedAmount)} · {p.totals.collectionCount}</td><td className="p-3">{formatVnd(p.totals.refundedAmount)} · {p.totals.refundCount}</td><td className="p-3">{formatVnd(p.totals.netCashFlow)}</td><td className="p-3">Thu {formatVnd(p.unknownDate.collectedAmount)} · {p.unknownDate.collectionCount}; hoàn {formatVnd(p.unknownDate.refundedAmount)} · {p.unknownDate.refundCount}</td></tr>)}</tbody>
      </table>
      {!data.byProperty.length && <p>Không có giao dịch tiền cọc trong phạm vi được xem.</p>}
    </>}
  </section>
}
