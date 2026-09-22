import type { Room } from "@/types/room"

type AvailableRoomsProps = {
  onOpenRoom: (room: Room) => void
  onLoginRequired: () => void
  onSchedule: (room: Room) => void
}

export default function AvailableRooms({
  onOpenRoom,
  onLoginRequired,
  onSchedule,
  rooms,
}: AvailableRoomsProps & { rooms: Room[] }) {
  return (
    <section className="mt-16 border-t border-slate-100 pt-12">
      <div className="mb-6">
        <h2 className="font-display text-[27px] font-bold tracking-[-.04em] text-slate-900">
          Phòng đang trống
        </h2>
        <p className="mt-1 text-[13px] text-slate-500">
          5 phòng có thể thuê tại Sunrise Residence
        </p>
      </div>
      <div className="space-y-4">
        {rooms.map((room) => (
          <article
            key={room.code}
            className="grid grid-cols-[198px_1fr_202px] overflow-hidden rounded-2xl bg-white shadow-[0_3px_18px_rgba(16,65,67,.07)]"
          >
            <img
              src={room.image}
              alt={`Phòng ${room.code}`}
              className="h-[175px] w-full object-cover"
            />
            <div className="px-6 py-5">
              <h3 className="text-[17px] font-bold tracking-[-.02em] text-slate-800">
                Phòng {room.code}
              </h3>
              <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-[12px] text-slate-500">
                <span>{room.size}</span>
                <span>{room.occupants}</span>
                <span>{room.floor}</span>
                <span className="font-medium text-[#087775]">
                  {room.available}
                </span>
              </div>
              <div className="mt-4 flex items-center gap-2">
                {room.cardAmenities.map((amenity) => (
                  <span key={amenity} className="room-amenity">
                    {amenity}
                  </span>
                ))}
              </div>
            </div>
            <div className="flex flex-col border-l border-slate-100 px-5 py-5">
              <p className="text-[17px] font-bold tracking-[-.03em] text-[#087775]">
                {room.price}
              </p>
              <p className="text-[11px] text-slate-400">/ tháng</p>
              <span className="mt-3 w-fit rounded-full bg-emerald-50 px-2.5 py-1 text-[10px] font-bold tracking-[.08em] text-emerald-700">
                TRỐNG
              </span>
              <button
                onClick={() => onOpenRoom(room)}
                className="mt-auto rounded-lg bg-[#087775] py-2.5 text-[12px] font-bold text-white shadow-sm hover:bg-[#066a68]"
              >
                Xem phòng
              </button>
              <button
                onClick={() => onSchedule(room)}
                className="mt-2 text-[12px] font-semibold text-[#087775] hover:underline"
              >
                Đặt lịch xem
              </button>
            </div>
          </article>
        ))}
      </div>
    </section>
  )
}
