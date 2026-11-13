import { useEffect, useState } from "react"
import { useAuth } from "@/auth/AuthContext"
import { getUtilityReadings } from "@/services/utilityReadingService"
import type { ContractDto } from "@/types/contract"
import type { UtilityReadingDto } from "@/types/utilityReading"
import { formatContractDate } from "@/utils/managementContracts"
import { formatMonth } from "@/utils/invoiceDisplay"

export default function ContractMeterHandover({ contract }: { contract: ContractDto }) {
  const { accessToken } = useAuth()
  const [readings, setReadings] = useState<UtilityReadingDto[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [retry, setRetry] = useState(0)
  const complete = contract.meterHandoverDate != null && contract.initialElectricReading != null && contract.initialWaterReading != null
  const legacy = !complete && contract.hasUtilityReadings === true
  useEffect(() => {
    if (!accessToken || !legacy) return
    const controller = new AbortController()
    setReadings(null)
    setError(null)
    getUtilityReadings(accessToken, contract.contractId, controller.signal).then((items) => {
      if (!controller.signal.aborted) setReadings(items)
    }).catch(() => {
      if (!controller.signal.aborted) setError("Không thể tải lịch sử chỉ số.")
    })
    return () => controller.abort()
  }, [accessToken, contract.contractId, legacy, retry])
  return (
    <section className="mt-5 rounded-xl bg-slate-50 p-5 text-sm" aria-label="Chỉ số điện nước khi bàn giao">
      <h3 className="font-bold text-slate-800">Chỉ số điện nước khi bàn giao</h3>
      {complete ? (
        <dl className="mt-3 space-y-2 text-slate-600">
          <div><dt className="inline">Ngày bàn giao: </dt><dd className="inline font-semibold">{formatContractDate(contract.meterHandoverDate!)}</dd></div>
          <div><dt className="inline">Điện ban đầu: </dt><dd className="inline font-semibold">{contract.initialElectricReading!.toLocaleString("vi-VN")} kWh</dd></div>
          <div><dt className="inline">Nước ban đầu: </dt><dd className="inline font-semibold">{contract.initialWaterReading!.toLocaleString("vi-VN")} m³</dd></div>
          <div><dt className="inline">Trạng thái: </dt><dd className="inline">Đã ghi nhận{contract.hasUtilityReadings || contract.hasInvoices ? " · Đã khóa sau khi chốt kỳ/hóa đơn" : ""}</dd></div>
        </dl>
      ) : <p className="mt-3 text-slate-600">{legacy || contract.hasInvoices
        ? "Hợp đồng lịch sử chưa có thông tin bàn giao; dữ liệu đã chốt được giữ nguyên."
        : "Chưa ghi nhận chỉ số bàn giao."}</p>}
      {legacy && !error && readings === null && <p role="status" className="mt-2">Đang tải lịch sử chỉ số...</p>}
      {error && <p role="alert" className="mt-2">{error} <button type="button" onClick={() => setRetry((value) => value + 1)} className="underline">Thử lại</button></p>}
      {legacy && readings?.map((reading) => <p key={reading.readingId} className="mt-2 text-slate-600">
        Tháng {formatMonth(reading.readingMonth)}: điện {reading.electricStart} → {reading.electricEnd} kWh; nước {reading.waterStart} → {reading.waterEnd} m³.
      </p>)}
    </section>
  )
}
