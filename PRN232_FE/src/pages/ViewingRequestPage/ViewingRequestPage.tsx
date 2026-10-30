import { useState } from "react"
import { Link, useSearchParams } from "react-router"
import CustomerPageShell from "@/components/customer/CustomerPageShell"
import { useAuth } from "@/auth/AuthContext"
import { createViewingSchedule } from "@/services/viewingScheduleService"
import type { ViewingScheduleDto } from "@/types/viewingSchedule"
import { publicApi } from "@/services/publicBrowsingService"
import { usePublicApiResource } from "@/hooks/usePublicApiResource"
import { parsePositiveRouteId, routes } from "@/constants/routes"
import { bookingErrorMessage } from "@/utils/bookingDisplay"
import { publicApiErrorMessage } from "@/utils/publicApiDisplay"
import { formatVnd } from "@/utils/publicFormatting"
import { localInputToIso, toLocalInput, viewingTimeLabel } from "@/utils/viewingDisplay"

export default function ViewingRequestPage() {
  const [params] = useSearchParams()
  const roomId = parsePositiveRouteId(params.get("roomId") ?? undefined)

  return (
    <CustomerPageShell title="Đặt lịch xem phòng" description="Chọn giờ bạn muốn đến xem, hoặc để chủ trọ sắp xếp.">
      <div className="max-w-2xl rounded-2xl bg-white p-7 shadow-[0_5px_24px_rgba(16,65,67,.08)]">
        {roomId ? <ViewingRequestForm roomId={roomId} /> : <Notice message="Không tìm thấy phòng cần xem." />}
      </div>
    </CustomerPageShell>
  )
}

function ViewingRequestForm({ roomId }: { roomId: number }) {
  const { accessToken } = useAuth()
  const resource = usePublicApiResource(String(roomId), (signal) => publicApi.getRoom(roomId, signal))
  const [ownerDecides, setOwnerDecides] = useState(false)
  const [localTime, setLocalTime] = useState("")
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)
  const [created, setCreated] = useState<ViewingScheduleDto | null>(null)

  if (resource.status === "loading") return <p role="status" className="text-sm text-slate-500">Đang tải thông tin phòng...</p>
  if (resource.status === "error") {
    return (
      <Notice
        message={resource.error.kind === "not-found" ? "Phòng hiện không còn trống để xem." : publicApiErrorMessage(resource.error)}
      />
    )
  }
  if (created) {
    return <Notice message={`Đã gửi yêu cầu xem phòng ${created.roomCode} (${viewingTimeLabel(created)}). Chủ trọ sẽ xác nhận lịch hẹn.`} />
  }

  const room = resource.data
  const submit = async (event: React.FormEvent) => {
    event.preventDefault()
    if (!accessToken || pending) return
    if (!ownerDecides && (!localTime || Date.parse(localTime) <= Date.now())) {
      setError("Vui lòng chọn thời điểm xem trong tương lai.")
      return
    }
    setPending(true)
    setError(null)
    try {
      setCreated(await createViewingSchedule(accessToken, {
        roomId: room.roomId,
        scheduledAt: ownerDecides ? null : localInputToIso(localTime),
      }))
    } catch (createError) {
      setError(bookingErrorMessage(createError, "Phòng không còn trống hoặc bạn đã có lịch xem đang chờ cho phòng này."))
      setPending(false)
    }
  }

  return (
    <form onSubmit={submit} className="space-y-6">
      <dl className="grid gap-3 text-sm sm:grid-cols-2">
        <div>
          <dt className="text-slate-400">Khu trọ</dt>
          <dd className="mt-1 font-semibold text-slate-700">{room.property.propertyName}</dd>
        </div>
        <div>
          <dt className="text-slate-400">Phòng</dt>
          <dd className="mt-1 font-semibold text-slate-700">{room.roomCode} · {formatVnd(room.baseRent)}/tháng</dd>
        </div>
      </dl>
      <fieldset className="space-y-3">
        <legend className="font-display text-lg font-bold text-slate-900">Thời gian xem phòng</legend>
        <label className="flex items-center gap-2 text-sm text-slate-600">
          <input type="radio" name="timeChoice" checked={!ownerDecides} onChange={() => setOwnerDecides(false)} />
          Tự chọn ngày giờ
        </label>
        {!ownerDecides && (
          <input
            type="datetime-local"
            aria-label="Ngày giờ xem phòng"
            min={toLocalInput()}
            value={localTime}
            onChange={(event) => setLocalTime(event.target.value)}
            className="h-11 rounded-xl border border-slate-200 px-3 text-sm"
          />
        )}
        <label className="flex items-center gap-2 text-sm text-slate-600">
          <input type="radio" name="timeChoice" checked={ownerDecides} onChange={() => setOwnerDecides(true)} />
          Để chủ trọ sắp xếp
        </label>
      </fieldset>
      {error && <p role="alert" className="text-sm text-rose-600">{error}</p>}
      <button
        type="submit"
        disabled={pending}
        className="w-full rounded-lg bg-[#087775] py-3.5 text-sm font-bold text-white hover:bg-[#066a68] disabled:opacity-60"
      >
        {pending ? "Đang gửi..." : "Gửi yêu cầu xem phòng"}
      </button>
    </form>
  )
}

function Notice({ message }: { message: string }) {
  return (
    <div className="text-center">
      <p role="alert" className="text-[15px] font-semibold text-slate-700">{message}</p>
      <Link to={routes.myBookings} className="mt-4 inline-block text-sm font-semibold text-[#087775] hover:underline">
        Xem lịch hẹn và booking của tôi
      </Link>
    </div>
  )
}
