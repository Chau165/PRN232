import {
  useEffect,
  useRef,
  type KeyboardEvent as ReactKeyboardEvent,
} from "react"
import { Link } from "react-router"
import { publicApi } from "@/services/publicBrowsingService"
import type { PublicPropertyServiceRate } from "@/types/publicBrowsing"
import { usePublicApiResource } from "@/hooks/usePublicApiResource"
import { routes, viewingRequestPath } from "@/constants/routes"
import RoomDetailView from "@/components/room/RoomDetailView"
import { publicApiErrorMessage } from "@/utils/publicApiDisplay"
import { formatAddress, serviceRateUnitLabel } from "@/utils/publicFormatting"

type RoomDetailModalProps = {
  roomId: number
  propertyServiceRates?: PublicPropertyServiceRate[]
  onClose: () => void
  onRefresh: () => void
}

export default function RoomDetailModal({
  roomId,
  propertyServiceRates = [],
  onClose,
  onRefresh,
}: RoomDetailModalProps) {
  const closeButtonRef = useRef<HTMLButtonElement>(null)
  const dialogRef = useRef<HTMLDivElement>(null)
  const onCloseRef = useRef(onClose)
  onCloseRef.current = onClose
  const resource = usePublicApiResource(String(roomId), (signal) =>
    publicApi.getRoom(roomId, signal),
  )

  useEffect(() => {
    const previousFocus =
      document.activeElement instanceof HTMLElement
        ? document.activeElement
        : null
    closeButtonRef.current?.focus({ preventScroll: true })
    const handleEscape = (event: globalThis.KeyboardEvent) => {
      if (event.key === "Escape") onCloseRef.current()
    }
    document.addEventListener("keydown", handleEscape)
    return () => {
      document.removeEventListener("keydown", handleEscape)
      previousFocus?.focus({ preventScroll: true })
    }
  }, [])

  function keepFocusInside(event: ReactKeyboardEvent<HTMLDivElement>) {
    if (event.key !== "Tab") return
    const focusable = dialogRef.current?.querySelectorAll<HTMLElement>(
      'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])',
    )
    if (!focusable?.length) {
      event.preventDefault()
      dialogRef.current?.focus()
      return
    }

    const first = focusable[0]
    const last = focusable[focusable.length - 1]
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault()
      last.focus()
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault()
      first.focus()
    }
  }

  const room = resource.status === "success" ? resource.data : null
  const address = room
    ? formatAddress(room.property.addressText, room.property.ward, room.property.city)
    : ""

  return (
    <div
      className="fixed inset-0 z-[60] flex justify-end bg-slate-950/40"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) onClose()
      }}
    >
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="public-room-detail-title"
        tabIndex={-1}
        onKeyDown={keepFocusInside}
        className="h-full w-full max-w-[720px] overflow-y-auto bg-white shadow-2xl"
      >
        <div className="sticky top-0 z-10 flex items-center justify-between border-b border-slate-100 bg-white px-7 py-5">
          <div>
            <p className="text-[11px] font-bold uppercase tracking-[.14em] text-[#087775]">
              {room?.property.propertyName ?? "Chi tiết phòng"}
            </p>
            <h2
              id="public-room-detail-title"
              className="mt-1 font-display text-[25px] font-bold tracking-[-.04em] text-slate-900"
            >
              {room?.roomType.roomTypeName ?? "Chi tiết phòng"}
            </h2>
          </div>
          <button
            ref={closeButtonRef}
            type="button"
            aria-label="Đóng chi tiết phòng"
            onClick={onClose}
            className="grid h-9 w-9 place-items-center rounded-full bg-slate-50 text-xl text-slate-500 hover:bg-slate-100"
          >
            ×
          </button>
        </div>

        <div className="p-7">
          {resource.status === "loading" && (
            <p
              role="status"
              className="py-12 text-center text-[14px] text-slate-500"
            >
              Đang tải chi tiết phòng...
            </p>
          )}
          {resource.status === "error" && (
            <div
              role="alert"
              className="rounded-xl bg-slate-50 px-5 py-8 text-center"
            >
              <p className="text-[15px] font-semibold text-slate-800">
                {resource.error.kind === "not-found" ||
                resource.error.kind === "validation"
                  ? "Phòng hiện không khả dụng"
                  : publicApiErrorMessage(resource.error)}
              </p>
              {resource.error.kind === "not-found" ? (
                <button
                  type="button"
                  onClick={() => {
                    onRefresh()
                    onClose()
                  }}
                  className="mt-4 rounded-lg bg-[#087775] px-4 py-2.5 text-[13px] font-bold text-white"
                >
                  Tải lại danh sách phòng
                </button>
              ) : (
                <button
                  type="button"
                  onClick={resource.retry}
                  className="mt-4 rounded-lg bg-[#087775] px-4 py-2.5 text-[13px] font-bold text-white"
                >
                  Thử lại
                </button>
              )}
            </div>
          )}

          {room && (
            <RoomDetailView
              viewerRole="guest"
              room={{
                roomId: room.roomId,
                roomCode: room.roomCode,
                propertyId: room.property.propertyId,
                propertyName: room.property.propertyName,
                address,
                roomTypeName: room.roomType.roomTypeName,
                price: room.baseRent,
                areaSqm: room.areaSqm,
                maxOccupants: room.maxOccupants,
                floor: room.floor,
                description: room.description,
                images: room.images,
                imageAltSubject: room.roomType.roomTypeName,
                amenities: room.amenities,
                services: room.services.map((service) => {
                  const rate = propertyServiceRates.find(
                    (item) => item.serviceId === service.serviceId,
                  )
                  const unitLabel = rate
                    ? serviceRateUnitLabel(rate.unitCode, rate.billingBasis)
                    : null

                  return {
                    serviceId: service.serviceId,
                    name: service.name,
                    price: rate && unitLabel ? rate.price : null,
                    unitCode: rate?.unitCode ?? null,
                    billingBasis: rate?.billingBasis ?? null,
                    note: rate
                      ? unitLabel
                        ? null
                        : "Đơn vị tính đang được cập nhật."
                      : "Biểu phí theo khu trọ đang được cập nhật.",
                  }
                }),
                equipments: room.equipments.map((equipment) => ({
                  equipmentId: equipment.equipmentId,
                  name: equipment.name,
                  quantity: equipment.quantity,
                })),
              }}
              actions={
                <div className="grid gap-3 sm:grid-cols-2">
                  <Link
                    to={viewingRequestPath(room.property.propertyId, room.roomType.roomTypeId)}
                    className="rounded-lg bg-teal-50 py-3.5 text-center text-[13px] font-bold text-[#087775] hover:bg-teal-100"
                  >
                    Đặt lịch xem phòng
                  </Link>
                  <Link
                    to={`${routes.deposit}?roomId=${room.roomId}`}
                    className="rounded-lg bg-[#087775] py-3.5 text-center text-[13px] font-bold text-white hover:bg-[#066a68]"
                  >
                    Đặt cọc giữ phòng
                  </Link>
                </div>
              }
            />
          )}
        </div>
      </div>
    </div>
  )
}
