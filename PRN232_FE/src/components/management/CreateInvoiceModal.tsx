import MeterHandoverModal from "@/components/management/MeterHandoverModal"
import { useEffect, useState } from "react"
import { useAuth } from "@/auth/AuthContext"
import { getContracts } from "@/services/contractService"
import { createInvoice, previewInvoice } from "@/services/invoiceService"
import { createUtilityReading, getUtilityReadings } from "@/services/utilityReadingService"
import type { ContractDto } from "@/types/contract"
import type { InvoiceDto } from "@/types/invoice"
import type { UtilityReadingDto } from "@/types/utilityReading"
import { bookingErrorMessage } from "@/utils/bookingDisplay"
import { defaultDueDate, formatMonth, invoiceLineLabel } from "@/utils/invoiceDisplay"
import { localToday } from "@/utils/managementContracts"
import { formatVnd } from "@/utils/publicFormatting"

type CreateInvoiceModalProps = {
  onClose: () => void
  onCreated: (invoice: InvoiceDto) => void
}

function nextMonth(month: string) {
  const date = new Date(`${month}T00:00:00Z`)
  date.setUTCMonth(date.getUTCMonth() + 1)
  return date.toISOString().slice(0, 10)
}

function meterValue(value: string) {
  const parsed = Number(value)
  return value.trim() !== "" && Number.isFinite(parsed) && parsed >= 0 && Number(parsed.toFixed(2)) === parsed ? parsed : null
}

// Luồng: chọn hợp đồng + tháng -> ghi chỉ số tháng đó nếu chưa có -> xem trước -> tạo hoá đơn.
// BE tính tiền theo biểu giá thật của khu trọ; FE chỉ hiển thị kết quả xem trước.
export default function CreateInvoiceModal({ onClose, onCreated }: CreateInvoiceModalProps) {
  const { accessToken, user } = useAuth()
  const today = localToday()
  const [contracts, setContracts] = useState<ContractDto[] | null>(null)
  const [contractId, setContractId] = useState<number | null>(null)
  const [month, setMonth] = useState(today.slice(0, 7))
  const [dueDate, setDueDate] = useState(defaultDueDate(`${today.slice(0, 7)}-01`, today))
  const [readings, setReadings] = useState<UtilityReadingDto[]>([])
  const [handoverOpen, setHandoverOpen] = useState(false)
  const [readingsLoading, setReadingsLoading] = useState(false)
  const [readingsError, setReadingsError] = useState<string | null>(null)
  const [retry, setRetry] = useState(0)
  const [readingsContractId, setReadingsContractId] = useState<number | null>(null)
  const [electricEnd, setElectricEnd] = useState("")
  const [waterEnd, setWaterEnd] = useState("")
  const [preview, setPreview] = useState<InvoiceDto | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [handoverNotice, setHandoverNotice] = useState<string | null>(null)

  useEffect(() => {
    if (!accessToken) return
    const controller = new AbortController()
    getContracts(accessToken, controller.signal)
      .then((items) => setContracts(items.filter((item) => item.status === "Active")))
      .catch((loadError: unknown) => {
        if (!controller.signal.aborted) {
          setContracts([])
          setError(loadError instanceof Error ? loadError.message : "Không thể tải danh sách hợp đồng.")
        }
      })
    return () => controller.abort()
  }, [accessToken, retry])

  useEffect(() => {
    if (!accessToken || contractId === null) return
    const controller = new AbortController()
    setReadings([])
    setReadingsContractId(null)
    setReadingsLoading(true)
    setReadingsError(null)
    setElectricEnd("")
    setWaterEnd("")
    getUtilityReadings(accessToken, contractId, controller.signal)
      .then((items) => {
        if (!controller.signal.aborted) {
          setReadings(items)
          setReadingsContractId(contractId)
        }
      })
      .catch((loadError: unknown) => {
        if (!controller.signal.aborted) {
          setReadingsError(loadError instanceof Error ? loadError.message : "Không thể tải chỉ số điện nước.")
        }
      })
      .finally(() => { if (!controller.signal.aborted) setReadingsLoading(false) })
    return () => controller.abort()
  }, [accessToken, contractId, retry])

  const monthDate = `${month}-01`
  const selectedContract = contracts?.find((item) => item.contractId === contractId)
  const reading = readings.find((item) => item.readingMonth === monthDate)
  const previous = [...readings].sort((left, right) => right.readingMonth.localeCompare(left.readingMonth))[0]
  const startE = previous?.electricEnd ?? selectedContract?.initialElectricReading ?? null
  const startW = previous?.waterEnd ?? selectedContract?.initialWaterReading ?? null
  const ready = contractId !== null && readingsContractId === contractId && !readingsLoading && !readingsError
  const missingBaseline = !previous && (startE === null || startW === null || !selectedContract?.meterHandoverDate)
  const expectedMonth = previous ? nextMonth(previous.readingMonth) : `${selectedContract?.startDate.slice(0, 7)}-01`
  const sequenceError = !reading && !missingBaseline && monthDate !== expectedMonth
    ? `Vui lòng chốt kỳ ${formatMonth(expectedMonth)} trước khi chuyển sang kỳ khác.` : null


  // Đổi bất kỳ thông tin nào thì bản xem trước cũ không còn đúng nữa.
  const change = (apply: () => void) => {
    apply()
    setPreview(null)
    setError(null)
  }

  const saveReading = async () => {
    if (!accessToken || contractId === null || busy || !ready || missingBaseline || sequenceError || reading) return
    const endE = meterValue(electricEnd)
    const endW = meterValue(waterEnd)
    if (startE === null || startW === null || endE === null || endW === null) {
      setError("Vui lòng nhập đầy đủ chỉ số điện nước.")
      return
    }
    if (endE < startE || endW < startW) {
      setError("Chỉ số tháng này không được nhỏ hơn chỉ số đầu kỳ.")
      return
    }
    setBusy(true)
    setError(null)
    try {
      const saved = await createUtilityReading(accessToken, {
        contractId,
        readingMonth: monthDate,
        electricEnd: endE,
        waterEnd: endW,
      })
      setReadings((items) => [saved, ...items])
    } catch (saveError) {
      setError(bookingErrorMessage(saveError, "Tháng này đã có chỉ số, hoặc đã có chỉ số của tháng sau."))
    } finally {
      setBusy(false)
    }
  }

  const runPreview = async () => {
    if (!accessToken || contractId === null || busy || !ready) return
    if (dueDate < today) {
      setError("Hạn thanh toán không được ở quá khứ.")
      return
    }
    setBusy(true)
    setError(null)
    try {
      setPreview(await previewInvoice(accessToken, { contractId, invoiceMonth: monthDate, dueDate }))
    } catch (previewError) {
      setError(bookingErrorMessage(previewError,
        "Không thể lập hoá đơn: tháng này đã có hoá đơn, chưa ghi chỉ số, hoặc khu trọ chưa đặt giá cho một dịch vụ."))
    } finally {
      setBusy(false)
    }
  }

  const submit = async () => {
    if (!accessToken || contractId === null || !preview || busy) return
    setBusy(true)
    setError(null)
    try {
      onCreated(await createInvoice(accessToken, { contractId, invoiceMonth: monthDate, dueDate }))
    } catch (createError) {
      setError(bookingErrorMessage(createError, "Tháng này vừa được lập hoá đơn. Vui lòng tải lại trang."))
      setBusy(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 grid place-items-center overflow-y-auto bg-slate-950/40 p-6">
      <div role="dialog" aria-modal="true" aria-labelledby="create-invoice-title" className="w-full max-w-2xl rounded-2xl bg-white p-7 shadow-2xl">
        <div className="flex items-start justify-between gap-4">
          <h2 id="create-invoice-title" className="font-display text-2xl font-bold text-slate-900">Tạo hóa đơn tháng</h2>
          <button type="button" aria-label="Đóng" onClick={onClose} className="grid h-9 w-9 place-items-center rounded-full bg-slate-50 text-xl text-slate-500">×</button>
        </div>

        <div className="mt-6 grid gap-4 sm:grid-cols-2">
          <label className="block text-sm font-semibold text-slate-700 sm:col-span-2">
            Hợp đồng / phòng
            <select
              value={contractId ?? ""}
              disabled={busy}
              onChange={(event) => change(() => {
                setContractId(event.target.value ? Number(event.target.value) : null)
                setElectricEnd("")
                setWaterEnd("")
                setHandoverNotice(null)
              })}
              className="mt-2 h-11 w-full rounded-xl border border-slate-200 px-3 text-sm"
            >
              <option value="">{contracts === null ? "Đang tải..." : "Chọn hợp đồng đang hiệu lực"}</option>
              {contracts?.map((contract) => (
                <option key={contract.contractId} value={contract.contractId}>
                  {contract.propertyName} · Phòng {contract.roomCode} · {contract.customerName}
                </option>
              ))}
            </select>
          </label>
          <label className="block text-sm font-semibold text-slate-700">
            Kỳ hóa đơn
            <input type="month" disabled={busy} value={month} max={today.slice(0, 7)}
              onChange={(event) => change(() => {
                setMonth(event.target.value)
                setElectricEnd("")
                setWaterEnd("")
                setDueDate(defaultDueDate(`${event.target.value}-01`, today))
              })}
              className="mt-2 h-11 w-full rounded-xl border border-slate-200 px-3 text-sm" />
          </label>
          <label className="block text-sm font-semibold text-slate-700">
            Hạn thanh toán
            <input type="date" disabled={busy} value={dueDate} min={today}
              onChange={(event) => change(() => setDueDate(event.target.value))}
              className="mt-2 h-11 w-full rounded-xl border border-slate-200 px-3 text-sm" />
          </label>
        </div>

        {contractId !== null && (
          <section className="mt-6 rounded-xl bg-slate-50 p-4 text-sm" aria-label="Chỉ số điện nước">
            <h3 className="font-bold text-slate-800">Chỉ số điện nước tháng {formatMonth(monthDate)}</h3>
            {!ready ? (
              readingsError ? <p role="alert" className="mt-2">{readingsError} <button type="button" onClick={() => setRetry((value) => value + 1)} className="underline">Thử lại</button></p>
                : <p role="status" className="mt-2">Đang tải chỉ số và dữ liệu bàn giao...</p>
            ) : reading ? (
              <p className="mt-2 text-slate-600">
                Đã ghi: điện {reading.electricStart} → {reading.electricEnd} ({reading.electricUsage} kWh),
                nước {reading.waterStart} → {reading.waterEnd} ({reading.waterUsage} m³).
              </p>
            ) : missingBaseline ? (
              <div className="mt-2 text-slate-600">
                <p>Chưa có chỉ số điện nước bàn giao. Vui lòng ghi nhận chỉ số ban đầu trước khi chốt kỳ đầu tiên.</p>
                {user?.role === "MANAGER" && selectedContract?.hasInvoices === false && selectedContract.hasUtilityReadings === false &&
                  <button type="button" disabled={busy} onClick={() => setHandoverOpen(true)} className="mt-3 rounded-lg bg-[#087775] px-4 py-2.5 font-bold text-white">Ghi nhận chỉ số bàn giao</button>}
              </div>
            ) : sequenceError ? <p role="alert" className="mt-2 text-amber-800">{sequenceError}</p> : (
              <>
                <p className="mt-2 text-slate-500">
                  {previous ? `Chỉ số đầu kỳ lấy từ tháng ${formatMonth(previous.readingMonth)}: điện ${previous.electricEnd}, nước ${previous.waterEnd}.`
                    : "Chỉ số đầu kỳ được lấy từ dữ liệu bàn giao phòng."}
                </p>
                <div className="mt-3 grid gap-3 sm:grid-cols-2">
                  <label className="text-sm font-semibold text-slate-700">Chỉ số điện đầu kỳ<input readOnly value={startE ?? ""} className="mt-2 h-11 w-full rounded-xl border border-slate-200 bg-slate-100 px-3" /></label>
                  <label className="text-sm font-semibold text-slate-700">Chỉ số nước đầu kỳ<input readOnly value={startW ?? ""} className="mt-2 h-11 w-full rounded-xl border border-slate-200 bg-slate-100 px-3" /></label>
                  <MeterInput label="Chỉ số điện cuối kỳ" value={electricEnd} onChange={(value) => change(() => setElectricEnd(value))} />
                  <MeterInput label="Chỉ số nước cuối kỳ" value={waterEnd} onChange={(value) => change(() => setWaterEnd(value))} />
                </div>
                <button type="button" disabled={busy} onClick={saveReading}
                  className="mt-3 rounded-lg bg-slate-800 px-4 py-2.5 text-[13px] font-bold text-white disabled:opacity-50">
                  Lưu chỉ số cuối kỳ
                </button>
              </>
            )}
          </section>
        )}

        {preview && (
          <section className="mt-6 divide-y divide-slate-100 rounded-xl border border-slate-100 px-4 text-sm" aria-label="Xem trước hóa đơn">
            {preview.lines.map((line, index) => (
              <div key={`${line.itemType}-${index}`} className="flex justify-between gap-4 py-3">
                <span className="text-slate-500">{invoiceLineLabel(line)}</span>
                <strong className="shrink-0 text-slate-700">{formatVnd(line.amount)}</strong>
              </div>
            ))}
            <div className="flex justify-between py-3.5">
              <strong>Tổng cộng</strong>
              <strong className="text-[#087775]">{formatVnd(preview.total)}</strong>
            </div>
          </section>
        )}

        {handoverOpen && selectedContract && <MeterHandoverModal contract={selectedContract} onClose={() => setHandoverOpen(false)} onSaved={(updated) => {
          setContracts((items) => items?.map((item) => item.contractId === updated.contractId ? updated : item) ?? null)
          setHandoverOpen(false)
          setPreview(null)
          setHandoverNotice("Đã lưu chỉ số bàn giao thành công.")
        }} />}
        {handoverNotice && <p role="status" className="mt-4 text-sm text-[#087775]">{handoverNotice}</p>}
        {error && <p role="alert" className="mt-4 rounded-lg bg-rose-50 p-3 text-sm text-rose-600">{error}</p>}
        {error && contracts?.length === 0 && <button type="button" onClick={() => setRetry((value) => value + 1)} className="mt-2 text-sm font-semibold underline">Thử lại</button>}
        <div className="mt-6 flex flex-wrap justify-end gap-3">
          <button type="button" onClick={onClose} className="rounded-lg bg-slate-100 px-5 py-3 text-sm font-semibold text-slate-600">Hủy</button>
          {preview ? (
            <button type="button" disabled={busy} onClick={submit}
              className="rounded-lg bg-[#087775] px-5 py-3 text-sm font-bold text-white disabled:opacity-50">
              {busy ? "Đang tạo…" : "Tạo hóa đơn"}
            </button>
          ) : (
            <button type="button" disabled={busy || contractId === null || !ready} onClick={runPreview}
              className="rounded-lg bg-[#087775] px-5 py-3 text-sm font-bold text-white disabled:opacity-50">
              Xem trước hóa đơn
            </button>
          )}
        </div>
      </div>
    </div>
  )
}

function MeterInput({ label, value, onChange }: { label: string; value: string; onChange: (value: string) => void }) {
  return (
    <label className="block text-[13px] font-semibold text-slate-600">
      {label}
      <input type="number" min={0} step="0.01" inputMode="decimal" value={value}
        onChange={(event) => onChange(event.target.value)}
        className="mt-1.5 h-10 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm" />
    </label>
  )
}
