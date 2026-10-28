import { useEffect, useState } from "react"
import { useAuth } from "@/auth/AuthContext"
import { getContracts, type ContractDto } from "@/api/contractsApi"
import { createInvoice, previewInvoice, type InvoiceDto } from "@/api/invoicesApi"
import { createUtilityReading, getUtilityReadings, type UtilityReadingDto } from "@/api/utilityReadingsApi"
import { bookingErrorMessage } from "@/utils/bookingDisplay"
import { defaultDueDate, formatMonth, invoiceLineLabel } from "@/utils/invoiceDisplay"
import { localToday } from "@/utils/managementContracts"
import { formatVnd } from "@/utils/publicFormatting"

type CreateInvoiceModalProps = {
  onClose: () => void
  onCreated: (invoice: InvoiceDto) => void
}

function meterValue(value: string) {
  const parsed = Number(value)
  return value.trim() !== "" && Number.isFinite(parsed) && parsed >= 0 ? parsed : null
}

// Luồng: chọn hợp đồng + tháng -> ghi chỉ số tháng đó nếu chưa có -> xem trước -> tạo hoá đơn.
// BE tính tiền theo biểu giá thật của khu trọ; FE chỉ hiển thị kết quả xem trước.
export default function CreateInvoiceModal({ onClose, onCreated }: CreateInvoiceModalProps) {
  const { accessToken } = useAuth()
  const today = localToday()
  const [contracts, setContracts] = useState<ContractDto[] | null>(null)
  const [contractId, setContractId] = useState<number | null>(null)
  const [month, setMonth] = useState(today.slice(0, 7))
  const [dueDate, setDueDate] = useState(defaultDueDate(`${today.slice(0, 7)}-01`, today))
  const [readings, setReadings] = useState<UtilityReadingDto[]>([])
  const [electricStart, setElectricStart] = useState("")
  const [waterStart, setWaterStart] = useState("")
  const [electricEnd, setElectricEnd] = useState("")
  const [waterEnd, setWaterEnd] = useState("")
  const [preview, setPreview] = useState<InvoiceDto | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

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
  }, [accessToken])

  useEffect(() => {
    if (!accessToken || contractId === null) return
    const controller = new AbortController()
    setReadings([])
    getUtilityReadings(accessToken, contractId, controller.signal)
      .then(setReadings)
      .catch((loadError: unknown) => {
        if (!controller.signal.aborted) {
          setError(loadError instanceof Error ? loadError.message : "Không thể tải chỉ số điện nước.")
        }
      })
    return () => controller.abort()
  }, [accessToken, contractId])

  const monthDate = `${month}-01`
  const reading = readings.find((item) => item.readingMonth === monthDate)
  const previous = readings
    .filter((item) => item.readingMonth < monthDate)
    .sort((a, b) => b.readingMonth.localeCompare(a.readingMonth))[0]

  // Đổi bất kỳ thông tin nào thì bản xem trước cũ không còn đúng nữa.
  const change = (apply: () => void) => {
    apply()
    setPreview(null)
    setError(null)
  }

  const saveReading = async () => {
    if (!accessToken || contractId === null || busy) return
    const startE = previous ? previous.electricEnd : meterValue(electricStart)
    const startW = previous ? previous.waterEnd : meterValue(waterStart)
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
        electricStart: previous ? null : startE,
        waterStart: previous ? null : startW,
      })
      setReadings((items) => [saved, ...items])
    } catch (saveError) {
      setError(bookingErrorMessage(saveError, "Tháng này đã có chỉ số, hoặc đã có chỉ số của tháng sau."))
    } finally {
      setBusy(false)
    }
  }

  const runPreview = async () => {
    if (!accessToken || contractId === null || busy) return
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
              onChange={(event) => change(() => setContractId(event.target.value ? Number(event.target.value) : null))}
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
            <input type="month" value={month} max={today.slice(0, 7)}
              onChange={(event) => change(() => {
                setMonth(event.target.value)
                setDueDate(defaultDueDate(`${event.target.value}-01`, today))
              })}
              className="mt-2 h-11 w-full rounded-xl border border-slate-200 px-3 text-sm" />
          </label>
          <label className="block text-sm font-semibold text-slate-700">
            Hạn thanh toán
            <input type="date" value={dueDate} min={today}
              onChange={(event) => change(() => setDueDate(event.target.value))}
              className="mt-2 h-11 w-full rounded-xl border border-slate-200 px-3 text-sm" />
          </label>
        </div>

        {contractId !== null && (
          <section className="mt-6 rounded-xl bg-slate-50 p-4 text-sm" aria-label="Chỉ số điện nước">
            <h3 className="font-bold text-slate-800">Chỉ số điện nước tháng {formatMonth(monthDate)}</h3>
            {reading ? (
              <p className="mt-2 text-slate-600">
                Đã ghi: điện {reading.electricStart} → {reading.electricEnd} ({reading.electricUsage} kWh),
                nước {reading.waterStart} → {reading.waterEnd} ({reading.waterUsage} m³).
              </p>
            ) : (
              <>
                <p className="mt-2 text-slate-500">
                  {previous
                    ? `Chỉ số đầu kỳ lấy từ tháng ${formatMonth(previous.readingMonth)}: điện ${previous.electricEnd}, nước ${previous.waterEnd}.`
                    : "Đây là lần ghi đầu tiên của hợp đồng, cần nhập cả chỉ số đầu kỳ."}
                </p>
                <div className="mt-3 grid gap-3 sm:grid-cols-2">
                  {!previous && (
                    <>
                      <MeterInput label="Chỉ số điện đầu kỳ" value={electricStart} onChange={(value) => change(() => setElectricStart(value))} />
                      <MeterInput label="Chỉ số nước đầu kỳ" value={waterStart} onChange={(value) => change(() => setWaterStart(value))} />
                    </>
                  )}
                  <MeterInput label="Chỉ số điện cuối kỳ" value={electricEnd} onChange={(value) => change(() => setElectricEnd(value))} />
                  <MeterInput label="Chỉ số nước cuối kỳ" value={waterEnd} onChange={(value) => change(() => setWaterEnd(value))} />
                </div>
                <button type="button" disabled={busy} onClick={saveReading}
                  className="mt-3 rounded-lg bg-slate-800 px-4 py-2.5 text-[13px] font-bold text-white disabled:opacity-50">
                  Lưu chỉ số
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

        {error && <p role="alert" className="mt-4 rounded-lg bg-rose-50 p-3 text-sm text-rose-600">{error}</p>}
        <div className="mt-6 flex flex-wrap justify-end gap-3">
          <button type="button" onClick={onClose} className="rounded-lg bg-slate-100 px-5 py-3 text-sm font-semibold text-slate-600">Hủy</button>
          {preview ? (
            <button type="button" disabled={busy} onClick={submit}
              className="rounded-lg bg-[#087775] px-5 py-3 text-sm font-bold text-white disabled:opacity-50">
              {busy ? "Đang tạo…" : "Tạo hóa đơn"}
            </button>
          ) : (
            <button type="button" disabled={busy || contractId === null} onClick={runPreview}
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
      <input type="number" min={0} step="any" inputMode="decimal" value={value}
        onChange={(event) => onChange(event.target.value)}
        className="mt-1.5 h-10 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm" />
    </label>
  )
}
