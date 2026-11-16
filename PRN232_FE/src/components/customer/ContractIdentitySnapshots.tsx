import { useEffect, useRef, useState } from "react"
import { useAuth } from "@/auth/AuthContext"
import { apiRequest } from "@/api/apiClient"

type Snapshot = { snapshotId: number; position: number; fullName: string; maskedIdentityNumber: string }
export default function ContractIdentitySnapshots({ contractId }: { contractId: number }) {
  const { accessToken } = useAuth()
  const generation = useRef(0)
  const [rows, setRows] = useState<Snapshot[] | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState("")
  const [preview, setPreview] = useState<string | null>(null)
  useEffect(() => {
    generation.current++; setRows(null); setPreview(null); setError(""); setBusy(false)
    return () => { generation.current++ }
  }, [accessToken, contractId])
  useEffect(() => {
    if (!preview) return
    const timer = setTimeout(() => setPreview(null), 110000)
    return () => clearTimeout(timer)
  }, [preview])
  const root = `/contracts/${contractId}/identity-snapshots`
  async function request(snapshotId?: number, side?: string) {
    if (!accessToken || busy) return
    const current = generation.current
    setBusy(true); setError(""); setPreview(null)
    try {
      if (snapshotId) {
        const result = await apiRequest<{ url: string }>(`${root}/${snapshotId}/images/${side}/read-url`, { accessToken, method: "POST", requireJson: true })
        if (current === generation.current) setPreview(result.url)
      } else {
        const result = await apiRequest<Snapshot[]>(root, { accessToken, cache: "no-store", requireJson: true })
        if (current === generation.current) setRows(result)
      }
    } catch { if (current === generation.current) setError("Không thể tải hồ sơ CCCD. Hãy kiểm tra quyền và thử lại.") }
    finally { if (current === generation.current) setBusy(false) }
  }
  return <section aria-label="Hồ sơ CCCD theo hợp đồng" className="my-4 space-y-3">
    <button type="button" disabled={busy} onClick={() => void request()} className="text-teal-700 underline">{busy ? "Đang tải hồ sơ..." : "Xem hồ sơ CCCD đã chốt"}</button>
    {error && <p role="alert">{error}</p>}
    {rows?.length === 0 && <p>Hợp đồng này chưa có bản sao hồ sơ đặt cọc.</p>}
    {rows?.map(row => <div key={row.snapshotId}><p>{row.position === 0 ? "Người đứng tên" : "Người ở cùng"}: {row.fullName} · {row.maskedIdentityNumber}</p>
      {(["Front", "Back"] as const).map(side => <button className="mr-3 text-teal-700 underline" type="button" disabled={busy} key={side} onClick={() => void request(row.snapshotId, side)}>{side === "Front" ? "Mặt trước" : "Mặt sau"}</button>)}
    </div>)}
    {preview && <div><button type="button" onClick={() => setPreview(null)}>Đóng ảnh CCCD</button><img className="max-h-80 max-w-full" src={preview} alt="Ảnh CCCD trong hồ sơ hợp đồng" referrerPolicy="no-referrer" /></div>}
  </section>
}
