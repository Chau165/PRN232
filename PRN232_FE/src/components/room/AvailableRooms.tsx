import type { PublicPropertyRoomCard } from "@/api/publicBrowsingApi"

import { formatVnd } from "@/utils/publicFormatting"

type AvailableRoomsProps = {
  rooms: PublicPropertyRoomCard[]

  totalCount: number

  onOpenRoom: (roomId: number) => void
}

export default function AvailableRooms({
  rooms,
  totalCount,
  onOpenRoom,
}: AvailableRoomsProps) {
  return (
    <section className="mt-16 border-t border-slate-100 pt-12">
      <div className="mb-6">
        <h2 className="font-display text-[27px] font-bold tracking-[-.04em] text-slate-900">
          Phòng phù hợp
        </h2>
        <p className="mt-1 text-[13px] text-slate-500">
          {totalCount} phòng phù hợp với điều kiện lọc hiện tại
        </p>
      </div>
      <div className="space-y-4">
        {rooms.map((room) => (
          <article
            key={room.roomId}
            className="grid overflow-hidden rounded-2xl bg-white shadow-[0_3px_18px_rgba(16,65,67,.07)] md:grid-cols-[198px_1fr_202px]"
          >
            <div className="grid h-[175px] place-items-center overflow-hidden bg-slate-100">
              {room.primaryImageUrl ? (
                <img
                  src={room.primaryImageUrl}
                  alt={`Ảnh ${room.roomTypeName}`}
                  className="h-full w-full object-cover"
                />
              ) : (
                <span className="text-[13px] text-slate-400">
                  Ảnh phòng chưa có
                </span>
              )}
            </div>
            <div className="px-6 py-5">
              <h3 className="text-[17px] font-bold tracking-[-.02em] text-slate-800">
                {room.roomTypeName}
              </h3>
              <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-[12px] text-slate-500">
                <span>{room.areaSqm} m²</span>
              </div>
              {room.amenitiesSummary.length > 0 && (
                <div
                  className="mt-4 flex flex-wrap gap-2"
                  aria-label="Tiện ích phòng"
                >
                  {room.amenitiesSummary.map((amenity) => (
                    <span key={amenity.amenityId} className="room-amenity">
                      {amenity.name}
                    </span>
                  ))}
                </div>
              )}
            </div>
            <div className="flex flex-col border-t border-slate-100 px-5 py-5 md:border-l md:border-t-0">
              <p className="text-[11px] text-slate-400">Giá thuê</p>
              <p className="mt-1 text-[17px] font-bold tracking-[-.03em] text-[#087775]">
                {formatVnd(room.baseRent)}
              </p>
              <button
                type="button"
                onClick={() => onOpenRoom(room.roomId)}
                className="mt-auto rounded-lg bg-[#087775] py-2.5 text-[12px] font-bold text-white shadow-sm hover:bg-[#066a68]"
              >
                Xem chi tiết
              </button>
            </div>
          </article>
        ))}
      </div>
    </section>
  )
}
