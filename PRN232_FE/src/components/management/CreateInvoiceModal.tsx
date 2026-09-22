import { useMemo, useState, type FormEvent } from "react"
import type {
  InvoiceAdditionalFee,
  ManagedRoom,
} from "@/types/management"
import {
  formatVnd,
  getUtilityMeterReadings,
  type CreateManagedInvoiceInput,
} from "@/utils/managedInvoices"

const electricityRate = 2047
const waterRate = 6700
const parkingRate = 70000
const internetRate = 100000
const trashFee = 35000

type InvoiceRoomOption = ManagedRoom & { propertyName: string }

type OtherFeeInput = InvoiceAdditionalFee & { id: number }

type CreateInvoiceModalProps = {
  rooms: InvoiceRoomOption[]
  onClose: () => void
  onSubmit: (input: CreateManagedInvoiceInput) => void
}

function numericValue(value: string) {
  const parsed = Number(value)
  return Number.isFinite(parsed) && parsed > 0 ? parsed : 0
}

function priceValue(value: string) {
  return Number(value.replace(/[^\d]/g, "")) || 0
}

function monthToPeriod(month: string) {
  const [year, monthNumber] = month.split("-")
  return `${monthNumber}/${year}`
}

export default function CreateInvoiceModal({
  rooms,
  onClose,
  onSubmit,
}: CreateInvoiceModalProps) {
  const [roomId, setRoomId] = useState("")
  const [period, setPeriod] = useState("2026-09")
  const [electricityReading, setElectricityReading] = useState("")
  const [waterReading, setWaterReading] = useState("")
  const [vehicleCount, setVehicleCount] = useState("0")
  const [internetDeviceCount, setInternetDeviceCount] = useState("1")
  const [otherFees, setOtherFees] = useState<OtherFeeInput[]>([])
  const [error, setError] = useState("")

  const selectedRoom = rooms.find((room) => room.id === roomId)
  const meterReadings = useMemo(() => getUtilityMeterReadings(), [])
  const previousReadings = selectedRoom
    ? meterReadings[selectedRoom.id] ?? { electricity: 0, water: 0 }
    : { electricity: 0, water: 0 }
  const currentElectricity = numericValue(electricityReading)
  const currentWater = numericValue(waterReading)
  const electricityUsage = Math.max(
    0,
    currentElectricity - previousReadings.electricity,
  )
  const waterUsage = Math.max(0, currentWater - previousReadings.water)
  const roomRent = selectedRoom ? priceValue(selectedRoom.price) : 0
  const electricityFee = electricityUsage * electricityRate
  const waterFee = waterUsage * waterRate
  const parkingFee = numericValue(vehicleCount) * parkingRate
  const internetFee = numericValue(internetDeviceCount) * internetRate
  const otherFeeTotal = otherFees.reduce(
    (sum, fee) => sum + numericValue(fee.amount),
    0,
  )
  const total =
    roomRent +
    electricityFee +
    waterFee +
    parkingFee +
    internetFee +
    trashFee +
    otherFeeTotal

  function selectRoom(nextRoomId: string) {
    setRoomId(nextRoomId)
    setElectricityReading("")
    setWaterReading("")
    setError("")
  }

  function addOtherFee() {
    setOtherFees((fees) => [
      ...fees,
      { id: Date.now() + fees.length, name: "", amount: "" },
    ])
  }

  function updateOtherFee(
    id: number,
    field: "name" | "amount",
    value: string,
  ) {
    setOtherFees((fees) =>
      fees.map((fee) => (fee.id === id ? { ...fee, [field]: value } : fee)),
    )
  }

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!selectedRoom) {
      setError("Vui lòng chọn phòng cần tạo hóa đơn.")
      return
    }
    if (
      currentElectricity < previousReadings.electricity ||
      currentWater < previousReadings.water
    ) {
      setError("Chỉ số tháng này không được nhỏ hơn chỉ số tháng trước.")
      return
    }
    if (
      otherFees.some(
        (fee) => !fee.name.trim() || numericValue(fee.amount) <= 0,
      )
    ) {
      setError("Vui lòng nhập đầy đủ tên và số tiền cho mỗi phí khác.")
      return
    }

    onSubmit({
      propertyId: selectedRoom.propertyId,
      propertyName: selectedRoom.propertyName,
      roomCode: selectedRoom.code,
      tenantName: selectedRoom.tenantName ?? "",
      period: monthToPeriod(period),
      roomRent,
      electricityUsage,
      electricityRate,
      electricityReading: currentElectricity,
      waterUsage,
      waterRate,
      waterReading: currentWater,
      parkingFee,
      internetFee,
      trashFee,
      additionalFees: otherFees,
    })
  }

  return (
    <div
      className="fixed inset-0 z-[70] grid place-items-center bg-slate-950/45 p-4 sm:p-6"
      role="presentation"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) onClose()
      }}
    >
      <form
        onSubmit={submit}
        className="max-h-[92vh] w-full max-w-[820px] overflow-y-auto rounded-2xl bg-white shadow-2xl"
        aria-labelledby="create-invoice-title"
      >
        <header className="sticky top-0 z-10 flex items-start justify-between border-b border-slate-100 bg-white px-6 py-5 sm:px-7">
          <div>
            <p className="text-[11px] font-bold uppercase tracking-[.16em] text-[#087775]">
              Hóa đơn mới
            </p>
            <h2
              id="create-invoice-title"
              className="mt-1 font-display text-[25px] font-bold tracking-[-.04em] text-slate-900"
            >
              Tạo hóa đơn
            </h2>
            <p className="mt-1 text-[13px] text-slate-500">
              Nhập chỉ số và các phí dịch vụ để tính tổng tự động.
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="Đóng"
            className="grid h-9 w-9 shrink-0 place-items-center rounded-full bg-slate-50 text-xl text-slate-500 hover:bg-slate-100"
          >
            ×
          </button>
        </header>

        <div className="space-y-7 px-6 py-6 sm:px-7">
          <section>
            <div className="mb-3 flex items-center justify-between gap-4">
              <h3 className="text-[14px] font-bold text-slate-800">
                Thông tin phòng
              </h3>
              <label className="text-[12px] font-semibold text-slate-500">
                Kỳ hóa đơn
                <input
                  type="month"
                  value={period}
                  onChange={(event) => setPeriod(event.target.value)}
                  className="ml-2 rounded-lg border border-slate-200 bg-white px-2 py-1.5 text-[12px] outline-none focus:border-[#087775]"
                />
              </label>
            </div>
            <label className="block text-[13px] font-semibold text-slate-700">
              Số phòng
              <select
                required
                value={roomId}
                onChange={(event) => selectRoom(event.target.value)}
                className="mt-2 h-11 w-full rounded-xl border border-slate-200 bg-white px-3.5 text-[14px] outline-none focus:border-[#087775] focus:ring-2 focus:ring-[#087775]/15"
              >
                <option value="">Chọn phòng đang có người ở</option>
                {rooms.map((room) => (
                  <option key={room.id} value={room.id}>
                    {room.propertyName} · Phòng {room.code}
                  </option>
                ))}
              </select>
            </label>
            {selectedRoom && (
              <div className="mt-3 grid gap-3 rounded-xl bg-[#f1f8f7] p-4 text-[13px] sm:grid-cols-3">
                <p>
                  <span className="block text-[11px] text-slate-400">Khách thuê</span>
                  <strong className="text-slate-700">{selectedRoom.tenantName}</strong>
                </p>
                <p>
                  <span className="block text-[11px] text-slate-400">Số người ở</span>
                  <strong className="text-slate-700">{selectedRoom.occupancy}</strong>
                </p>
                <p>
                  <span className="block text-[11px] text-slate-400">Tiền phòng / tháng</span>
                  <strong className="text-[#087775]">{selectedRoom.price}</strong>
                </p>
              </div>
            )}
          </section>

          <section>
            <h3 className="text-[14px] font-bold text-slate-800">
              Điện và nước
            </h3>
            <div className="mt-3 grid gap-4 md:grid-cols-2">
              <fieldset className="rounded-xl border border-slate-200 p-4">
                <legend className="px-1 text-[13px] font-bold text-slate-700">
                  Điện
                </legend>
                <p className="text-[12px] text-[#087775]">
                  Đơn giá cố định: {formatVnd(electricityRate)}/kWh
                </p>
                <div className="mt-3 grid grid-cols-2 gap-3">
                  <label className="text-[12px] font-semibold text-slate-500">
                    Chỉ số tháng trước
                    <input
                      readOnly
                      value={selectedRoom ? previousReadings.electricity : ""}
                      placeholder="Chọn phòng"
                      className="mt-1.5 h-10 w-full rounded-lg border border-slate-200 bg-slate-50 px-3 text-[13px] text-slate-500"
                    />
                  </label>
                  <label className="text-[12px] font-semibold text-slate-500">
                    Chỉ số tháng này (kWh)
                    <input
                      required
                      disabled={!selectedRoom}
                      min={previousReadings.electricity}
                      type="number"
                      inputMode="numeric"
                      value={electricityReading}
                      onChange={(event) => setElectricityReading(event.target.value)}
                      placeholder="Ví dụ: 1325"
                      className="mt-1.5 h-10 w-full rounded-lg border border-slate-200 bg-white px-3 text-[13px] text-slate-700 outline-none placeholder:text-slate-400 focus:border-[#087775] disabled:bg-slate-50"
                    />
                  </label>
                </div>
                <p className="mt-3 text-[12px] text-slate-500">
                  Tiêu thụ: <strong className="text-slate-700">{electricityUsage} kWh</strong>
                  <span className="mx-1.5 text-slate-300">•</span>
                  Tiền điện: <strong className="text-[#087775]">{formatVnd(electricityFee)}</strong>
                </p>
              </fieldset>

              <fieldset className="rounded-xl border border-slate-200 p-4">
                <legend className="px-1 text-[13px] font-bold text-slate-700">
                  Nước
                </legend>
                <p className="text-[12px] text-[#087775]">
                  Đơn giá cố định: {formatVnd(waterRate)}/m³
                </p>
                <div className="mt-3 grid grid-cols-2 gap-3">
                  <label className="text-[12px] font-semibold text-slate-500">
                    Chỉ số tháng trước
                    <input
                      readOnly
                      value={selectedRoom ? previousReadings.water : ""}
                      placeholder="Chọn phòng"
                      className="mt-1.5 h-10 w-full rounded-lg border border-slate-200 bg-slate-50 px-3 text-[13px] text-slate-500"
                    />
                  </label>
                  <label className="text-[12px] font-semibold text-slate-500">
                    Chỉ số tháng này (m³)
                    <input
                      required
                      disabled={!selectedRoom}
                      min={previousReadings.water}
                      type="number"
                      inputMode="numeric"
                      value={waterReading}
                      onChange={(event) => setWaterReading(event.target.value)}
                      placeholder="Ví dụ: 84"
                      className="mt-1.5 h-10 w-full rounded-lg border border-slate-200 bg-white px-3 text-[13px] text-slate-700 outline-none placeholder:text-slate-400 focus:border-[#087775] disabled:bg-slate-50"
                    />
                  </label>
                </div>
                <p className="mt-3 text-[12px] text-slate-500">
                  Tiêu thụ: <strong className="text-slate-700">{waterUsage} m³</strong>
                  <span className="mx-1.5 text-slate-300">•</span>
                  Tiền nước: <strong className="text-[#087775]">{formatVnd(waterFee)}</strong>
                </p>
              </fieldset>
            </div>
          </section>

          <section>
            <h3 className="text-[14px] font-bold text-slate-800">Phí dịch vụ</h3>
            <div className="mt-3 grid gap-4 md:grid-cols-2">
              <label className="block rounded-xl border border-slate-200 p-4 text-[12px] font-semibold text-slate-500">
                Tiền gửi xe
                <span className="ml-1 font-normal text-[#087775]">
                  {formatVnd(parkingRate)}/xe
                </span>
                <input
                  min="0"
                  type="number"
                  inputMode="numeric"
                  value={vehicleCount}
                  onChange={(event) => setVehicleCount(event.target.value)}
                  className="mt-2 h-10 w-full rounded-lg border border-slate-200 bg-white px-3 text-[13px] text-slate-700 outline-none focus:border-[#087775]"
                  aria-label="Số xe gửi"
                />
                <span className="mt-2 block text-[12px] font-bold text-[#087775]">
                  {formatVnd(parkingFee)}
                </span>
              </label>
              <label className="block rounded-xl border border-slate-200 p-4 text-[12px] font-semibold text-slate-500">
                Tiền Internet
                <span className="ml-1 font-normal text-[#087775]">
                  {formatVnd(internetRate)}/thiết bị
                </span>
                <input
                  min="0"
                  type="number"
                  inputMode="numeric"
                  value={internetDeviceCount}
                  onChange={(event) => setInternetDeviceCount(event.target.value)}
                  className="mt-2 h-10 w-full rounded-lg border border-slate-200 bg-white px-3 text-[13px] text-slate-700 outline-none focus:border-[#087775]"
                  aria-label="Số thiết bị dùng Internet"
                />
                <span className="mt-2 block text-[12px] font-bold text-[#087775]">
                  {formatVnd(internetFee)}
                </span>
              </label>
            </div>
            <div className="mt-4 flex items-center justify-between rounded-xl bg-slate-50 px-4 py-3 text-[13px]">
              <span className="font-semibold text-slate-600">Tiền đổ rác</span>
              <strong className="text-[#087775]">{formatVnd(trashFee)}</strong>
            </div>
          </section>

          <section>
            <div className="flex items-center justify-between gap-3">
              <div>
                <h3 className="text-[14px] font-bold text-slate-800">Phí khác</h3>
                <p className="mt-1 text-[12px] text-slate-500">
                  Thêm từng loại phí để tạo danh sách chi tiết trên hóa đơn.
                </p>
              </div>
              <button
                type="button"
                onClick={addOtherFee}
                className="shrink-0 rounded-lg bg-teal-50 px-3 py-2 text-[12px] font-bold text-[#087775] hover:bg-teal-100"
              >
                + Thêm phí
              </button>
            </div>
            {otherFees.length > 0 && (
              <div className="mt-3 space-y-2">
                {otherFees.map((fee, index) => (
                  <div key={fee.id} className="grid grid-cols-[1fr_.8fr_auto] gap-2">
                    <input
                      value={fee.name}
                      onChange={(event) =>
                        updateOtherFee(fee.id, "name", event.target.value)
                      }
                      placeholder={`Tên phí khác ${index + 1}`}
                      className="h-10 min-w-0 rounded-lg border border-slate-200 px-3 text-[13px] outline-none placeholder:text-slate-400 focus:border-[#087775]"
                    />
                    <input
                      min="0"
                      type="number"
                      inputMode="numeric"
                      value={fee.amount}
                      onChange={(event) =>
                        updateOtherFee(fee.id, "amount", event.target.value)
                      }
                      placeholder="Số tiền"
                      className="h-10 min-w-0 rounded-lg border border-slate-200 px-3 text-[13px] outline-none placeholder:text-slate-400 focus:border-[#087775]"
                    />
                    <button
                      type="button"
                      onClick={() =>
                        setOtherFees((fees) =>
                          fees.filter((item) => item.id !== fee.id),
                        )
                      }
                      aria-label={`Xóa phí khác ${index + 1}`}
                      className="grid h-10 w-10 place-items-center rounded-lg bg-slate-100 text-lg text-slate-500 hover:bg-rose-50 hover:text-rose-600"
                    >
                      ×
                    </button>
                  </div>
                ))}
              </div>
            )}
          </section>
        </div>

        <footer className="sticky bottom-0 border-t border-slate-100 bg-white px-6 py-4 sm:px-7">
          {error && (
            <p className="mb-3 rounded-lg bg-rose-50 px-3 py-2 text-[12px] font-semibold text-rose-700">
              {error}
            </p>
          )}
          <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <p className="text-[12px] text-slate-400">Tổng cộng</p>
              <p className="text-[22px] font-bold text-[#087775]">{formatVnd(total)}</p>
            </div>
            <div className="grid grid-cols-2 gap-3 sm:flex">
              <button
                type="button"
                onClick={onClose}
                className="rounded-lg bg-slate-100 px-5 py-3 text-[13px] font-bold text-slate-600 hover:bg-slate-200"
              >
                Hủy
              </button>
              <button
                type="submit"
                className="rounded-lg bg-[#087775] px-5 py-3 text-[13px] font-bold text-white shadow-sm hover:bg-[#066a68]"
              >
                Tạo hóa đơn
              </button>
            </div>
          </div>
        </footer>
      </form>
    </div>
  )
}
