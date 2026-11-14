import { Link } from "react-router"
import { viewingRequestPath } from "@/constants/routes"
import { usePublicApiResource } from "@/hooks/usePublicApiResource"
import { publicApi } from "@/services/publicBrowsingService"
import { publicApiErrorMessage } from "@/utils/publicApiDisplay"
import { formatVnd } from "@/utils/publicFormatting"

export default function PublicRoomTypeCards({ propertyId }: { propertyId: number }) {
  const resource = usePublicApiResource(
    `public-room-types-${propertyId}`,
    (signal) => publicApi.listRoomTypes(propertyId, {}, signal),
  )

  if (resource.status === "loading") {
    return <p role="status" className="mt-5 text-sm text-slate-500">Đang tải loại phòng...</p>
  }

  if (resource.status === "error") {
    return (
      <div role="alert" className="mt-5 rounded-xl bg-rose-50 p-4 text-sm text-rose-700">
        <p>{publicApiErrorMessage(resource.error)}</p>
        <button type="button" onClick={resource.retry} className="mt-2 font-semibold underline">
          Thử tải lại
        </button>
      </div>
    )
  }

  if (resource.data.length === 0) {
    return <p role="status" className="mt-5 text-sm text-slate-500">Khu trọ chưa công bố loại phòng nào.</p>
  }

  return (
    <div className="mt-5 grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
      {resource.data.map((roomType) => (
        <article key={roomType.roomTypeId} className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
          {roomType.referenceImageUrl ? (
            <img
              src={roomType.referenceImageUrl}
              alt={`Ảnh tham khảo loại phòng ${roomType.roomTypeName}`}
              className="h-40 w-full object-cover"
            />
          ) : (
            <div className="grid h-40 place-items-center bg-slate-100 text-sm text-slate-500">
              Chưa có ảnh tham khảo
            </div>
          )}
          <div className="p-5">
            <h3 className="font-display text-lg font-bold text-slate-900">{roomType.roomTypeName}</h3>
            <p className="mt-2 text-xl font-bold text-[#087775]">{formatVnd(roomType.monthlyRent)}<span className="ml-1 text-xs font-medium text-slate-500">/tháng</span></p>
            <p className="mt-2 text-sm text-slate-600">
              {roomType.availableRoomCount}/{roomType.totalRoomCount} phòng hiện có thể thuê
            </p>
            {(roomType.minimumAreaSqm !== null || roomType.maximumAreaSqm !== null) && (
              <p className="mt-1 text-sm text-slate-500">
                Diện tích {roomType.minimumAreaSqm ?? roomType.maximumAreaSqm}–{roomType.maximumAreaSqm ?? roomType.minimumAreaSqm} m²
              </p>
            )}
            <Link
              to={viewingRequestPath(propertyId, roomType.roomTypeId)}
              className="mt-4 inline-flex w-full items-center justify-center rounded-lg bg-[#087775] px-4 py-3 text-sm font-bold text-white hover:bg-[#066a68]"
            >
              Đặt lịch xem
            </Link>
          </div>
        </article>
      ))}
    </div>
  )
}
