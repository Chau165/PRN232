import { useCallback, useEffect, useRef, useState } from "react"
import CustomerPageShell from "@/components/customer/CustomerPageShell"
import StatusBadge from "@/components/customer/StatusBadge"
import { useAuth } from "@/auth/AuthContext"
import { getCurrentRentals, getRentalHistory, type Rental } from "@/api/rentalsApi"

export default function MyContractsPage() {
  const { accessToken } = useAuth()
  const [contracts, setContracts] = useState<Array<Rental & { isCurrent: boolean }>>([])
  const [dataOwnerToken, setDataOwnerToken] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [retry, setRetry] = useState(0)
  const ownerToken = useRef(accessToken)
  ownerToken.current = accessToken
  const load = useCallback(async (signal: AbortSignal) => {
    if (!accessToken) return
    setLoading(true)
    setError(null)
    try {
      const [current, history] = await Promise.all([
        getCurrentRentals(accessToken, signal),
        getRentalHistory(accessToken, signal),
      ])
      if (!signal.aborted && ownerToken.current === accessToken) {
        const currentIds = new Set(current.map((item) => item.contractId))
        setContracts([...current, ...history].map((item) => ({ ...item, isCurrent: currentIds.has(item.contractId) })))
        setDataOwnerToken(accessToken)
      }
    } catch (cause) {
      if (!signal.aborted && ownerToken.current === accessToken) setError(cause instanceof Error ? cause.message : "Không thể tải hợp đồng.")
    } finally {
      if (!signal.aborted && ownerToken.current === accessToken) {
        setDataOwnerToken(accessToken)
        setLoading(false)
      }
    }
  }, [accessToken])

  useEffect(() => {
    ownerToken.current = accessToken
    setContracts([])
    setDataOwnerToken(null)
    const controller = new AbortController()
    void load(controller.signal)
    return () => controller.abort()
  }, [load, retry])

  return (
    <CustomerPageShell title="Hợp đồng của tôi" description="Theo dõi các hợp đồng đang hiệu lực và lịch sử thuê phòng của bạn.">
      {loading || dataOwnerToken !== accessToken ? <p role="status" className="rounded-xl bg-white p-6 text-slate-500">Đang tải hợp đồng...</p> : error ? <div role="alert" className="rounded-xl bg-white p-6 text-rose-600"><p>{error}</p><button onClick={() => setRetry((value) => value + 1)} className="mt-3 font-semibold">Thử lại</button></div> : contracts.length === 0 ? <p role="status" className="rounded-xl border border-dashed border-slate-200 bg-white p-8 text-center text-slate-500">Chưa có hợp đồng đang thuê hoặc đã hết hạn.</p> : <div className="space-y-4">
        {contracts.map((contract) => {
          const current = contract.isCurrent
          return <article key={contract.contractId} className="rounded-2xl bg-white p-6 shadow-[0_3px_18px_rgba(16,65,67,.07)]"><div className="flex flex-wrap items-start justify-between gap-3"><div><p className="text-[12px] font-bold uppercase tracking-[.12em] text-[#087775]">Hợp đồng #{contract.contractId}</p><h2 className="mt-2 font-display text-[22px] font-bold text-slate-900">{contract.propertyName}</h2><p className="mt-1 text-[14px] text-slate-500">Phòng {contract.roomCode} · {contract.propertyAddress}</p></div><StatusBadge tone={current ? "green" : "slate"}>{current ? "ĐANG HIỆU LỰC" : "ĐÃ KẾT THÚC"}</StatusBadge></div><div className="mt-6 grid gap-4 border-t border-slate-100 pt-5 text-[14px] sm:grid-cols-3"><div><p className="text-slate-400">Bắt đầu</p><p className="mt-1 font-semibold text-slate-700">{formatDate(contract.startDate)}</p></div><div><p className="text-slate-400">Kết thúc</p><p className="mt-1 font-semibold text-slate-700">{formatDate(contract.endDate)}</p></div><div><p className="text-slate-400">Giá thuê</p><p className="mt-1 font-bold text-[#087775]">{formatCurrency(contract.monthlyRent)}</p></div></div></article>
        })}
      </div>}
    </CustomerPageShell>
  )
}

function formatDate(value: string) {
  return new Date(`${value}T00:00:00`).toLocaleDateString("vi-VN")
}

function formatCurrency(amount: number) {
  return new Intl.NumberFormat("vi-VN", { style: "currency", currency: "VND" }).format(amount)
}
