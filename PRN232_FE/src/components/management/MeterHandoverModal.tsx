import { useEffect, useRef, useState, type FormEvent } from "react"
import { ApiError } from "@/api/apiClient"
import { useAuth } from "@/auth/AuthContext"
import { getContract, saveContractMeterHandover } from "@/services/contractService"
import type { ContractDto } from "@/types/contract"
import { formatContractDate, localToday } from "@/utils/managementContracts"

export default function MeterHandoverModal({ contract, onClose, onSaved }: {
  contract: ContractDto
  onClose: () => void
  onSaved: (contract: ContractDto) => void
}) {
  const { accessToken } = useAuth()
  const [date, setDate] = useState(contract.meterHandoverDate ?? localToday())
  const [electric, setElectric] = useState(contract.initialElectricReading?.toString() ?? "")
  const [water, setWater] = useState(contract.initialWaterReading?.toString() ?? "")
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const dateInput = useRef<HTMLInputElement>(null)
  const editing = contract.meterHandoverDate != null
  const today = localToday()
  const maxDate = contract.endDate < today ? contract.endDate : today

  useEffect(() => {
    const previousFocus = document.activeElement
    dateInput.current?.focus()
    return () => {
      if (previousFocus instanceof HTMLElement) previousFocus.focus()
    }
  }, [])

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (!accessToken || busy) return
    const initialElectricReading = Number(electric)
    const initialWaterReading = Number(water)
    if (!date || date < contract.startDate || date > maxDate || electric.trim() === "" || water.trim() === "" ||
        !Number.isFinite(initialElectricReading) || !Number.isFinite(initialWaterReading) ||
        initialElectricReading < 0 || initialWaterReading < 0 ||
        Number(initialElectricReading.toFixed(2)) !== initialElectricReading || Number(initialWaterReading.toFixed(2)) !== initialWaterReading) {
      setError("Vui lòng nhập ngày trong thời hạn hợp đồng, không ở tương lai và chỉ số không âm, tối đa 2 chữ số thập phân.")
      return
    }
    if (editing && !window.confirm("Xác nhận sửa chỉ số bàn giao trước khi chốt kỳ đầu tiên?")) return
    setBusy(true)
    setError(null)
    try {
      await saveContractMeterHandover(accessToken, contract.contractId, {
        meterHandoverDate: date, initialElectricReading, initialWaterReading,
        expectedMeterHandover: editing && contract.initialElectricReading != null && contract.initialWaterReading != null
          ? { meterHandoverDate: contract.meterHandoverDate!, initialElectricReading: contract.initialElectricReading,
              initialWaterReading: contract.initialWaterReading }
          : null,
      })
      onSaved(await getContract(accessToken, contract.contractId))
    } catch (cause) {
      if (cause instanceof ApiError && cause.status === 403) {
        setError("Bạn không còn được phân công quản lý hợp đồng này.")
      } else if (cause instanceof ApiError && cause.status === 409) {
        setError("Dữ liệu đã chốt hoặc đã thay đổi. Vui lòng đóng và tải lại hợp đồng.")
      } else {
        setError(cause instanceof Error ? cause.message : "Không thể lưu chỉ số bàn giao. Vui lòng thử lại.")
      }
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="fixed inset-0 z-[60] grid place-items-center overflow-y-auto bg-slate-950/40 p-4">
      <form onSubmit={submit} role="dialog" aria-modal="true" aria-labelledby="meter-handover-title"
        onKeyDown={(event) => {
          if (event.key === "Escape" && !busy) onClose()
          if (event.key === "Tab") {
            const controls = Array.from(event.currentTarget.querySelectorAll<HTMLElement>("input:not(:disabled), button:not(:disabled)"))
            const first = controls[0]
            const last = controls[controls.length - 1]
            if (event.shiftKey && document.activeElement === first) {
              event.preventDefault()
              last?.focus()
            }
            if (!event.shiftKey && document.activeElement === last) {
              event.preventDefault()
              first?.focus()
            }
          }
        }}
        className="max-h-[92vh] w-full max-w-lg overflow-y-auto rounded-2xl bg-white p-6 shadow-2xl">
        <h2 id="meter-handover-title" className="font-display text-xl font-bold text-slate-900">Ghi nhận chỉ số bàn giao</h2>
        <p className="mt-3 text-sm text-slate-600">{contract.propertyName} · Phòng {contract.roomCode} · {contract.customerName}</p>
        <p className="mt-1 text-sm text-slate-500">Bắt đầu hợp đồng: {formatContractDate(contract.startDate)}</p>
        <fieldset disabled={busy} className="mt-5 grid gap-4">
          <label className="text-sm font-semibold text-slate-700">Ngày bàn giao
            <input ref={dateInput} required type="date" min={contract.startDate} max={maxDate} value={date}
              onChange={(event) => setDate(event.target.value)} className="mt-2 h-11 w-full rounded-xl border border-slate-200 px-3" />
          </label>
          <label className="text-sm font-semibold text-slate-700">Chỉ số điện ban đầu (kWh)
            <input required type="number" min="0" step="0.01" value={electric} onChange={(event) => setElectric(event.target.value)}
              className="mt-2 h-11 w-full rounded-xl border border-slate-200 px-3" />
          </label>
          <label className="text-sm font-semibold text-slate-700">Chỉ số nước ban đầu (m³)
            <input required type="number" min="0" step="0.01" value={water} onChange={(event) => setWater(event.target.value)}
              className="mt-2 h-11 w-full rounded-xl border border-slate-200 px-3" />
          </label>
        </fieldset>
        {error && <p role="alert" className="mt-4 rounded-lg bg-rose-50 p-3 text-sm text-rose-700">{error}</p>}
        <div className="mt-6 flex flex-wrap justify-end gap-3">
          <button type="button" disabled={busy} onClick={onClose} className="rounded-lg bg-slate-100 px-4 py-3 text-sm font-semibold">Đóng</button>
          <button type="submit" disabled={busy} className="rounded-lg bg-[#087775] px-4 py-3 text-sm font-bold text-white disabled:opacity-50">
            {busy ? "Đang lưu..." : "Lưu chỉ số bàn giao"}
          </button>
        </div>
      </form>
    </div>
  )
}
