<<<<<<< HEAD
import { ApiError } from "@/api/apiClient"
import { bookingRoomLabel } from "@/utils/bookingDisplay"
=======
import { getContractApplicationSource, type BookingApplication } from "@/services/bookingApplicationService"
>>>>>>> origin/main
import { useEffect, useState, type FormEvent } from "react"
import { Link, useNavigate, useSearchParams } from "react-router"
import ManagementPage from "@/components/management/ManagementPage"
import { ManagementLoadingState } from "@/components/management/ManagementState"
import { useAuth } from "@/auth/AuthContext"
import { getBooking, getEligibleContractRooms } from "@/services/bookingService"
import { createContract } from "@/services/contractService"
import { getRoomTypeServices } from "@/services/roomTypeService"
import { getPropertyServiceRates } from "@/services/serviceRateService"
import type { BookingDto, EligibleContractRoomDto } from "@/types/booking"
import type { PropertyServiceRateDto } from "@/types/serviceRate"
import { parsePositiveRouteId, routes } from "@/constants/routes"
import { bookingErrorMessage, formatBookingDepositTermText } from "@/utils/bookingDisplay"
import { refundBlocksContract } from "@/utils/bookingRefund"
import { addContractMonths, isContractDate, localToday } from "@/utils/managementContracts"
import { formatVnd } from "@/utils/publicFormatting"

const quickTerms = [
  { label: "6 tháng", months: 6 },
  { label: "1 năm", months: 12 },
  { label: "2 năm", months: 24 },
]

type InitialOccupant = { fullName: string; identityNumber: string; phoneNumber: string }

export default function CreateContractPage() {
  const [params] = useSearchParams()
  const bookingId = parsePositiveRouteId(params.get("bookingId") ?? undefined)

  return (
    <ManagementPage
      title="Tạo hợp đồng"
      description="Kiểm tra thông tin booking đã đặt cọc và chọn thời hạn hợp đồng."
      breadcrumbs={[
        { label: "Đặt phòng & Đặt cọc", to: routes.managementBookings },
        { label: "Tạo hợp đồng" },
      ]}
    >
      <div className="mx-auto w-full max-w-3xl">
        <Link to={routes.managementBookings} className="mb-5 inline-block text-sm font-semibold text-[#087775] hover:underline">
          ← Về Đặt phòng & Đặt cọc
        </Link>
        {bookingId ? <CreateContractForm bookingId={bookingId} /> : <Warning message="Không tìm thấy booking cần lập hợp đồng." />}
      </div>
    </ManagementPage>
  )
}

function CreateContractForm({ bookingId }: { bookingId: number }) {
  const { accessToken } = useAuth()
  const navigate = useNavigate()
  const today = localToday()
  const [source, setSource] = useState<BookingApplication | null>(null)
  const [sourceLoaded, setSourceLoaded] = useState(false)
  const [booking, setBooking] = useState<BookingDto | null>(null)
  const [eligibleRooms, setEligibleRooms] = useState<EligibleContractRoomDto[]>([])
  const [selectedRoomId, setSelectedRoomId] = useState("")
  const [roomSelectionAcknowledged, setRoomSelectionAcknowledged] = useState(false)
  const [roomsError, setRoomsError] = useState<string | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [startDate, setStartDate] = useState(today)
  const [endDate, setEndDate] = useState("")
  const [occupantCount, setOccupantCount] = useState(1)
  const [occupants, setOccupants] = useState<InitialOccupant[]>([])
  const [holderIdentityNumber, setHolderIdentityNumber] = useState("")
  const [registeredVehicleCount, setRegisteredVehicleCount] = useState("0")
  const [manualSecurityDepositAmount, setManualSecurityDepositAmount] = useState("0")
  const [securityDepositHandoverAcknowledged, setSecurityDepositHandoverAcknowledged] = useState(false)
  const [occupantCountChangeConfirmed, setOccupantCountChangeConfirmed] = useState(false)
  const [serviceRates, setServiceRates] = useState<PropertyServiceRateDto[]>([])
  const [assignedServices, setAssignedServices] = useState<{ serviceId: number; name: string }[]>([])
  const [servicePreviewError, setServicePreviewError] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    if (!accessToken) return
    const controller = new AbortController()
    Promise.all([getBooking(accessToken, bookingId, controller.signal),
      getContractApplicationSource(accessToken, bookingId, controller.signal).catch((error: unknown) => {
        if (typeof error === "object" && error !== null && "status" in error && error.status === 404) return null
        throw error
      })]).then(([bookingData, application]) => {if (!controller.signal.aborted) {setBooking(bookingData);setSource(application);setSourceLoaded(true)}})
      .catch((fetchError: unknown) => {
        if (!controller.signal.aborted) {
          setLoadError(fetchError instanceof Error ? fetchError.message : "Không thể tải booking.")
        }
      })
    return () => controller.abort()
  }, [accessToken, bookingId])

  useEffect(() => {
<<<<<<< HEAD
    if (!accessToken || !booking || booking.roomId !== null || booking.status !== "Deposited" || booking.contractId != null) return
    const controller = new AbortController()
    setSelectedRoomId("")
    setRoomSelectionAcknowledged(false)
    getEligibleContractRooms(accessToken, bookingId, controller.signal)
      .then((rooms) => { if (!controller.signal.aborted) { setEligibleRooms(rooms); setRoomsError(null) } })
      .catch((cause: unknown) => { if (!controller.signal.aborted) setRoomsError(bookingErrorMessage(cause, "Không tải được phòng đủ điều kiện.")) })
    return () => controller.abort()
  }, [accessToken, bookingId, booking?.roomId, booking?.status, booking?.contractId])

  useEffect(() => {
    if (!booking) return
=======
    if (!booking || !sourceLoaded) return
>>>>>>> origin/main
    const max = Math.max(1, booking.maxOccupants ?? 1)
    const initialCount = Math.min(Math.max(1, booking.occupantCount ?? 1), max)
    setOccupantCount(initialCount)
    setOccupants(Array.from({ length: initialCount - 1 }, () => ({ fullName: "", identityNumber: "", phoneNumber: "" })))
    setRegisteredVehicleCount("0")
  }, [booking?.bookingId, booking?.occupantCount, booking?.maxOccupants])

  useEffect(() => {
    if (!accessToken || !booking?.roomTypeId) return
    const controller = new AbortController()
    Promise.all([
      getRoomTypeServices(accessToken, booking.roomTypeId, controller.signal),
      getPropertyServiceRates(accessToken, booking.propertyId, controller.signal),
    ])
      .then(([roomServices, rates]) => {
        if (controller.signal.aborted) return
        setAssignedServices(roomServices.map(({ serviceId, name }) => ({ serviceId, name })))
        setServiceRates(rates)
        setServicePreviewError(null)
      })
      .catch((serviceError: unknown) => {
        if (!controller.signal.aborted) setServicePreviewError(serviceError instanceof Error ? serviceError.message : "Không tải được giá dịch vụ.")
      })
    return () => controller.abort()
  }, [accessToken, booking?.propertyId, booking?.roomTypeId])

  if (loadError) return <Warning message={loadError} />
  if (!booking) return <ManagementLoadingState variant="inline" message="Đang tải booking..." />
  if (booking.contractId !== null) return <Warning message="Booking này đã được lập hợp đồng." />
  if (booking.status !== "Deposited") return <Warning message="Chỉ booking đã đặt cọc mới lập được hợp đồng." />
  if (refundBlocksContract(booking.refundStatus)) return <Warning message="Booking đang có yêu cầu hoàn cọc hoặc đã hoàn cọc, chưa thể lập hợp đồng." />

  if (!(booking.agreedMonthlyRent && booking.agreedMonthlyRent > 0)) return <Warning message="Booking chưa có giá thuê thỏa thuận đã xác minh. Cần đối soát trước khi lập hợp đồng; không suy ra giá thuê từ tiền cọc." />

  const needsRoomSelection = booking.roomId === null
  const availableOptions = eligibleRooms.filter((room) => room.propertyId === booking.propertyId &&
    room.roomTypeId === booking.roomTypeId && room.maxOccupants >= Math.max(occupantCount, booking.occupantCount ?? 1))
  const bookingOccupants = booking.occupantCount ?? 1
  const selectedServiceMonth = `${startDate.slice(0, 7)}-01`
  const applicableServices = assignedServices.map((service) => ({
    service,
    rate: serviceRates.find((item) => item.serviceId === service.serviceId &&
      item.effectiveFrom <= selectedServiceMonth &&
      (item.effectiveTo === null || item.effectiveTo > selectedServiceMonth)),
  }))

  const updateOccupantCount = (nextCount: number) => {
    const safeCount = Math.min(Math.max(1, nextCount), booking.maxOccupants ?? 1)
    setOccupantCount(safeCount)
    setOccupants((current) => Array.from({ length: safeCount - 1 }, (_, index) =>
      current[index] ?? { fullName: "", identityNumber: "", phoneNumber: "" }))
    setRegisteredVehicleCount((current) => String(Math.min(Number(current) || 0, safeCount)))
    setError(null)
  }

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!accessToken || saving) return
    if (source && !source.readyForContract) {setError("Cần duyệt đầy đủ hồ sơ CCCD trước khi lập hợp đồng.");return}
    if (!isContractDate(startDate) || startDate < today) {
      setError("Ngày bắt đầu phải từ hôm nay trở đi.")
      return
    }
    if (!isContractDate(endDate) || endDate <= startDate) {
      setError("Ngày kết thúc phải sau ngày bắt đầu.")
      return
    }
    if (!source && !/^\d{12}$/.test(holderIdentityNumber)) {
      setError("CCCD người đứng tên phải gồm đúng 12 chữ số.")
      return
    }
    if (!source && occupants.some((occupant) => !occupant.fullName.trim() || !/^\d{12}$/.test(occupant.identityNumber))) {
      setError("Vui lòng nhập họ tên và CCCD 12 số cho từng người ở cùng.")
      return
    }
    const identityNumbers = [holderIdentityNumber, ...occupants.map((occupant) => occupant.identityNumber)]
    if (!source && new Set(identityNumbers).size !== identityNumbers.length) {
      setError("Mỗi người phải đăng ký một CCCD riêng.")
      return
    }
    const vehicleCount = Number(registeredVehicleCount)
    if (!Number.isInteger(vehicleCount) || vehicleCount < 0 || vehicleCount > occupantCount) {
      setError("Số xe phải từ 0 đến bằng số người đăng ký trong hợp đồng.")
      return
    }
    const manualDeposit = Number(manualSecurityDepositAmount)
    if (!Number.isFinite(manualDeposit) || manualDeposit < 0 || Math.round(manualDeposit * 100) !== manualDeposit * 100) {
      setError("Tiền bảo đảm bàn giao phải từ 0 trở lên và tối đa 2 chữ số thập phân.")
      return
    }
    if (!securityDepositHandoverAcknowledged) {
      setError("Vui lòng xác nhận tiền bảo đảm đã được bàn giao; nhập 0 nếu không thu khoản này.")
      return
    }
    if (occupantCount !== bookingOccupants && !occupantCountChangeConfirmed) {
      setError("Số người đăng ký khác booking. Vui lòng xác nhận thay đổi số người.")
      return
    }
    if (needsRoomSelection && (!availableOptions.some((room) => room.roomId === Number(selectedRoomId)) || !roomSelectionAcknowledged)) {
      setError("Chọn phòng cụ thể và xác nhận khách đồng ý nhận phòng được chọn.")
      return
    }
    setSaving(true)
    setError(null)
    try {
      const contract = await createContract(accessToken, {
        bookingId: booking.bookingId,
        ...(needsRoomSelection ? { roomId: Number(selectedRoomId), customerRoomSelectionAcknowledged: roomSelectionAcknowledged } : {}),
        startDate,
        endDate,
        bookingApplicationVersion: source?.version,
        holderIdentityNumber: source ? undefined : holderIdentityNumber,
        occupants: source ? undefined : occupants.map(({ fullName, identityNumber, phoneNumber }) => ({
          fullName: fullName.trim(),
          identityNumber,
          phoneNumber: phoneNumber.trim() || null,
        })),
        registeredVehicleCount: vehicleCount,
        manualSecurityDepositAmount: manualDeposit,
        securityDepositHandoverAcknowledged,
        occupantCountChangeConfirmed,
      })
      navigate(routes.managementContracts, { replace: true, state: { createdContractId: contract.contractId } })
    } catch (createError) {
      if (createError instanceof ApiError && createError.status === 409 && needsRoomSelection) {
        setSelectedRoomId("")
        setRoomSelectionAcknowledged(false)
        try { setEligibleRooms(await getEligibleContractRooms(accessToken, bookingId)); setRoomsError(null) }
        catch { setEligibleRooms([]); setRoomsError("Không tải lại được danh sách phòng. Vui lòng tải lại trang.") }
      }
      setError(bookingErrorMessage(createError, needsRoomSelection ? "Phòng hoặc booking vừa thay đổi. Danh sách đã được kiểm tra lại; vui lòng chọn phòng và xác nhận lại." : "Booking vừa thay đổi hoặc phòng đã có hợp đồng khác. Vui lòng tải lại trang."))
      setSaving(false)
    }
  }

  return (
    <form onSubmit={submit} className="max-w-3xl space-y-7 rounded-2xl border border-slate-100 bg-white p-5 shadow-sm sm:p-8">
      <div className="rounded-xl bg-teal-50 p-4">
        <p className="text-xs font-bold uppercase tracking-wider text-[#087775]">Booking #{booking.bookingId} · Đã đặt cọc</p>
        <h3 className="mt-2 text-lg font-bold text-slate-800">{booking.propertyName} · {bookingRoomLabel(booking)}</h3>
        <p className="mt-2 text-sm text-slate-500">
          Tiền booking đã nhận: {formatVnd(booking.depositAmount)}. Giá thuê thỏa thuận: {formatVnd(booking.agreedMonthlyRent)} / tháng.
        </p>
        <p className="mt-1 text-sm text-slate-600">
          {formatBookingDepositTermText(booking)}
        </p>
      </div>

<<<<<<< HEAD
      {needsRoomSelection && (
        <section className="space-y-3 rounded-xl border border-teal-100 p-4">
          <label className="block text-sm font-semibold text-slate-700">Chọn phòng cụ thể
            <select aria-label="Chọn phòng cụ thể" required value={selectedRoomId} disabled={saving || !!roomsError}
              onChange={(event) => { setSelectedRoomId(event.target.value); setRoomSelectionAcknowledged(false) }}
              className="mt-2 h-11 w-full rounded-lg border border-slate-200 bg-white px-3 font-normal">
              <option value="">— Chọn phòng sau khi trao đổi với khách —</option>
              {availableOptions.map((room) => <option key={room.roomId} value={room.roomId}>{room.roomCode} · {room.areaSqm} m² · tối đa {room.maxOccupants} người</option>)}
            </select>
          </label>
          {roomsError && <p role="alert" className="text-sm text-rose-700">{roomsError}</p>}
          {!roomsError && availableOptions.length === 0 && <p role="status" className="text-sm text-amber-700">Hiện chưa có phòng đủ điều kiện. Trao đổi với khách để xử lý hoặc hoàn cọc theo chính sách.</p>}
          <label className="flex items-start gap-2 text-sm text-slate-700">
            <input type="checkbox" required checked={roomSelectionAcknowledged} disabled={saving || !selectedRoomId}
              onChange={(event) => setRoomSelectionAcknowledged(event.target.checked)} />
            <span>Tôi đã trao đổi với khách và khách đồng ý nhận phòng được chọn. Đây là ghi nhận của Manager, không thay thế chữ ký của khách.</span>
          </label>
        </section>
      )}

      <section aria-labelledby="tenant-heading">
=======
      {source ? <section aria-label="Hồ sơ đã duyệt"><h3 className="font-bold">Hồ sơ người thuê</h3>
        {source.people.map(person => <p key={person.personId}>{person.position === 0 ? "Người đứng tên" : "Người ở cùng"}: {person.fullName} · {person.maskedIdentityNumber}</p>)}
        <p>{source.readyForContract ? "Hồ sơ đã duyệt. Hợp đồng sẽ lưu bản sao hồ sơ này." : "Hồ sơ chưa sẵn sàng. Hãy duyệt đầy đủ CCCD và danh sách người ở."}</p>
        <Link to="/management/booking-identities" className="text-teal-700 underline">Xem hồ sơ CCCD</Link>
      </section> : <><section aria-labelledby="tenant-heading">
>>>>>>> origin/main
        <h3 id="tenant-heading" className="mb-4 text-lg font-bold text-slate-800">Thông tin người thuê</h3>
        <div className="grid gap-4 sm:grid-cols-2">
          <ReadOnlyField label="Họ và tên người thuê" value={booking.customerName} />
          <ReadOnlyField label="Số điện thoại người thuê" value={booking.customerPhone || "Chưa có thông tin"} />
          <label className="block text-sm font-semibold text-slate-700">
            CCCD người đứng tên <span className="text-rose-600">*</span>
            <input required inputMode="numeric" maxLength={12} value={holderIdentityNumber}
              onChange={(event) => { setHolderIdentityNumber(event.target.value.replace(/\D/g, "").slice(0, 12)); setError(null) }}
              className="mt-2 h-12 w-full rounded-xl border border-slate-200 px-3.5 font-normal outline-none focus:border-[#087775]" />
            <span className="mt-1 block text-xs font-normal text-slate-500">Nhập CCCD sau khi đối chiếu bản gốc. Hồ sơ mới sẽ ở trạng thái chờ duyệt.</span>
          </label>
        </div>
      </section>

      <section aria-labelledby="occupants-heading" className="border-t border-slate-100 pt-6">
        <h3 id="occupants-heading" className="mb-2 text-lg font-bold text-slate-800">Người ở đăng ký ban đầu</h3>
        <p className="mb-4 text-sm text-slate-500">Tổng số người gồm người đứng tên và người ở cùng; tối đa {booking.maxOccupants ?? 1} người theo sức chứa phòng.</p>
        <label className="block max-w-xs text-sm font-semibold text-slate-700">
          Tổng số người đăng ký
          <input aria-label="Tổng số người đăng ký" type="number" min={1} max={booking.maxOccupants ?? 1} step={1}
            value={occupantCount} disabled={saving} onChange={(event) => updateOccupantCount(Number(event.target.value))}
            className="mt-2 h-11 w-full rounded-xl border border-slate-200 px-3.5 font-normal" />
        </label>
        {occupantCount !== bookingOccupants && (
          <label className="mt-3 flex items-start gap-2 text-sm text-amber-800">
            <input type="checkbox" checked={occupantCountChangeConfirmed} disabled={saving}
              onChange={(event) => setOccupantCountChangeConfirmed(event.target.checked)} className="mt-1" />
            <span>Xác nhận số người trên hợp đồng thay đổi từ {bookingOccupants} thành {occupantCount} so với booking.</span>
          </label>
        )}
        {occupants.map((occupant, index) => (
          <div key={index} className="mt-4 rounded-xl border border-slate-100 p-4">
            <p className="mb-3 text-sm font-bold text-slate-700">Người ở cùng {index + 1}</p>
            <div className="grid gap-3 sm:grid-cols-3">
              <label className="text-sm font-semibold text-slate-700">Họ và tên <span className="text-rose-600">*</span>
                <input required value={occupant.fullName} disabled={saving}
                  aria-label={`Họ tên người ở cùng ${index + 1}`}
                  onChange={(event) => setOccupants((current) => current.map((item, i) => i === index ? { ...item, fullName: event.target.value } : item))}
                  className="mt-2 h-11 w-full rounded-lg border border-slate-200 px-3 font-normal" />
              </label>
              <label className="text-sm font-semibold text-slate-700">CCCD <span className="text-rose-600">*</span>
                <input required inputMode="numeric" maxLength={12} value={occupant.identityNumber} disabled={saving}
                  aria-label={`CCCD người ở cùng ${index + 1}`}
                  onChange={(event) => setOccupants((current) => current.map((item, i) => i === index ? { ...item, identityNumber: event.target.value.replace(/\D/g, "").slice(0, 12) } : item))}
                  className="mt-2 h-11 w-full rounded-lg border border-slate-200 px-3 font-normal" />
              </label>
              <label className="text-sm font-semibold text-slate-700">Số điện thoại
                <input value={occupant.phoneNumber} disabled={saving}
                  onChange={(event) => setOccupants((current) => current.map((item, i) => i === index ? { ...item, phoneNumber: event.target.value } : item))}
                  className="mt-2 h-11 w-full rounded-lg border border-slate-200 px-3 font-normal" />
              </label>
            </div>
          </div>
        ))}
      </section>

      </>}

      <section aria-labelledby="handover-heading" className="border-t border-slate-100 pt-6">
        <h3 id="handover-heading" className="mb-4 text-lg font-bold text-slate-800">Xe và tiền bảo đảm</h3>
        <div className="grid gap-4 sm:grid-cols-2">
          <label className="text-sm font-semibold text-slate-700">Số xe đăng ký
            <input aria-label="Số xe đăng ký" type="number" min={0} max={occupantCount} step={1} required
              value={registeredVehicleCount} disabled={saving} onChange={(event) => setRegisteredVehicleCount(event.target.value)}
              className="mt-2 h-11 w-full rounded-xl border border-slate-200 px-3.5 font-normal" />
            <span className="mt-1 block text-xs font-normal text-slate-500">Tối đa {occupantCount} xe theo số người trong hợp đồng.</span>
          </label>
          <label className="text-sm font-semibold text-slate-700">Tiền bảo đảm nhận trực tiếp (VND)
            <input aria-label="Tiền bảo đảm nhận trực tiếp" type="number" min={0} step="0.01" required
              value={manualSecurityDepositAmount} disabled={saving} onChange={(event) => setManualSecurityDepositAmount(event.target.value)}
              className="mt-2 h-11 w-full rounded-xl border border-slate-200 px-3.5 font-normal" />
            <span className="mt-1 block text-xs font-normal text-slate-500">Khoản này tách biệt với tiền booking. Nhập 0 nếu không nhận thêm.</span>
          </label>
        </div>
        <label className="mt-4 flex items-start gap-2 text-sm text-slate-700">
          <input type="checkbox" required checked={securityDepositHandoverAcknowledged} disabled={saving}
            onChange={(event) => setSecurityDepositHandoverAcknowledged(event.target.checked)} className="mt-1" />
          <span>Tôi xác nhận đã đối chiếu và ghi nhận tiền bảo đảm bàn giao là {formatVnd(Number(manualSecurityDepositAmount) || 0)}.</span>
        </label>
      </section>

      <section aria-labelledby="service-preview-heading" className="border-t border-slate-100 pt-6">
        <h3 id="service-preview-heading" className="mb-2 text-lg font-bold text-slate-800">Dịch vụ theo loại phòng</h3>
        <p className="mb-3 text-sm text-slate-500">Giá dưới đây áp dụng trong tháng bắt đầu; tháng ở không trọn được tính theo số ngày thực tế.</p>
        {servicePreviewError && <p role="alert" className="text-sm text-amber-700">Không tải được phần xem trước giá dịch vụ: {servicePreviewError}</p>}
        {!servicePreviewError && applicableServices.length === 0 && <p className="text-sm text-slate-500">Chưa gắn dịch vụ cho loại phòng này.</p>}
        <ul className="divide-y divide-slate-100 rounded-xl border border-slate-100">
          {applicableServices.map(({ service, rate }) => (
            <li key={service.serviceId} className="flex flex-wrap justify-between gap-2 px-4 py-3 text-sm">
              <span className="font-semibold text-slate-700">{service.name}</span>
              <span className="text-slate-600">{rate ? `${formatVnd(rate.price)} / ${serviceRateUnit(rate)}` : "Chưa cấu hình giá hiệu lực"}</span>
            </li>
          ))}
        </ul>
      </section>

      <section aria-labelledby="term-heading" className="border-t border-slate-100 pt-6">
        <h3 id="term-heading" className="mb-4 text-lg font-bold text-slate-800">Thời hạn hợp đồng</h3>
        <div className="grid gap-4 sm:grid-cols-2">
          <DateField label="Ngày bắt đầu" value={startDate} min={today} disabled={saving}
            onChange={(value) => { setStartDate(value); setError(null) }} />
          <DateField label="Ngày kết thúc" value={endDate} min={startDate} disabled={saving}
            onChange={(value) => { setEndDate(value); setError(null) }} />
        </div>
        <p className="mt-4 text-xs text-slate-500">Chọn nhanh thời hạn tính từ ngày bắt đầu:</p>
        <div className="mt-2 flex flex-wrap gap-2">
          {quickTerms.map(({ label, months }) => (
            <button
              key={months}
              type="button"
              disabled={saving}
              aria-pressed={!!endDate && endDate === addContractMonths(startDate, months)}
              onClick={() => { setEndDate(addContractMonths(startDate, months)); setError(null) }}
              className="rounded-lg border border-teal-100 px-4 py-2 text-sm font-semibold text-[#087775] hover:bg-teal-50 aria-pressed:border-[#087775] aria-pressed:bg-teal-50 disabled:opacity-40"
            >
              {label}
            </button>
          ))}
        </div>
      </section>

      {error && <p role="alert" className="rounded-lg bg-rose-50 p-3 text-sm text-rose-600">{error}</p>}
      <div className="flex flex-wrap items-center justify-end gap-3 border-t border-slate-100 pt-6">
        <Link to={routes.managementBookings} className="rounded-lg bg-slate-100 px-5 py-3 text-sm font-semibold text-slate-600">Quay lại</Link>
        <button type="submit" disabled={saving || !!source && !source.readyForContract} className="rounded-lg bg-[#087775] px-5 py-3 text-sm font-bold text-white hover:bg-[#066a68] disabled:cursor-not-allowed disabled:opacity-40">
          {saving ? "Đang lưu…" : "Xác nhận hợp đồng"}
        </button>
      </div>
    </form>
  )
}

function DateField({ label, value, min, disabled, onChange }: {
  label: string
  value: string
  min: string
  disabled: boolean
  onChange: (value: string) => void
}) {
  return (
    <label className="block text-sm font-semibold text-slate-700">
      {label} <span className="text-rose-600">*</span>
      <input
        type="date"
        required
        min={min}
        value={value}
        disabled={disabled}
        onChange={(event) => onChange(event.target.value)}
        className="mt-2 h-12 w-full rounded-xl border border-slate-200 px-3.5 outline-none focus:border-[#087775] focus:ring-2 focus:ring-teal-50"
      />
    </label>
  )
}

function ReadOnlyField({ label, value }: { label: string; value: string }) {
  return (
    <label className="block text-sm font-semibold text-slate-700">
      {label}
      <input readOnly value={value} className="mt-2 h-12 w-full rounded-xl border border-transparent bg-slate-50 px-3.5 font-normal text-slate-600" />
    </label>
  )
}

function serviceRateUnit(rate: PropertyServiceRateDto) {
  if (rate.billingBasis === "vehicle") return "xe/tháng"
  if (rate.billingBasis === "occupant") return "người/tháng"
  if (rate.billingBasis === "additional_occupant") return "người ở cùng/tháng"
  if (rate.billingBasis === "fixed_room") return "phòng/tháng"
  return rate.unitCode
}

function Warning({ message }: { message: string }) {
  return <div role="alert" className="rounded-2xl border border-amber-200 bg-amber-50 p-6 text-sm text-amber-800">{message}</div>
}
