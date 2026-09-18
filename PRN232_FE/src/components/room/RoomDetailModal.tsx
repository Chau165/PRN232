import type { Room } from "@/types/room"

type RoomDetailModalProps = {
  room: Room
  onClose: () => void
  onLoginRequired: () => void
}

export default function RoomDetailModal({
  room,
  onClose,
  onLoginRequired,
}: RoomDetailModalProps) {
  return (
    <div className="fixed inset-0 z-[60] flex justify-end bg-slate-950/40">
      <div className="h-full w-full max-w-[720px] overflow-y-auto bg-white shadow-2xl">
        <div className="sticky top-0 z-10 flex items-center justify-between border-b border-slate-100 bg-white px-7 py-5">
          <div>
            <p className="text-[11px] font-bold uppercase tracking-[.14em] text-[#087775]">
              Sunrise Residence
            </p>
            <h2 className="mt-1 font-display text-[25px] font-bold tracking-[-.04em] text-slate-900">
              Phòng {room.code}
            </h2>
          </div>
          <button
            onClick={onClose}
            className="grid h-9 w-9 place-items-center rounded-full bg-slate-50 text-xl text-slate-500 hover:bg-slate-100"
          >
            ×
          </button>
        </div>
        <div className="p-7">
          <div className="grid h-[295px] grid-cols-[1.55fr_1fr] grid-rows-2 gap-1 overflow-hidden rounded-2xl bg-slate-100">
            <img
              src={room.image}
              alt={`Phòng ${room.code}`}
              className="row-span-2 h-full w-full object-cover"
            />
            <img
              src="https://images.unsplash.com/photo-1615874694520-474822394e73?auto=format&fit=crop&w=700&q=85"
              alt="Nội thất phòng"
              className="h-full w-full object-cover"
            />
            <img
              src="https://images.unsplash.com/photo-1616486338812-3dadae4b4ace?auto=format&fit=crop&w=700&q=85"
              alt="Không gian phòng"
              className="h-full w-full object-cover"
            />
          </div>
          <div className="mt-7 flex items-end justify-between">
            <div>
              <p className="text-[12px] text-slate-500">Giá thuê mỗi tháng</p>
              <p className="mt-1 text-[26px] font-bold tracking-[-.04em] text-[#087775]">
                {room.price}
                <span className="ml-1 text-[12px] font-normal text-slate-400">
                  / tháng
                </span>
              </p>
            </div>
            <span className="rounded-full bg-emerald-50 px-3 py-1.5 text-[11px] font-bold tracking-[.08em] text-emerald-700">
              TRỐNG
            </span>
          </div>
          <div className="mt-7 grid grid-cols-2 gap-3 rounded-xl bg-slate-50 p-4 text-[13px]">
            <div>
              <span className="text-slate-400">Diện tích</span>
              <p className="mt-1 font-semibold text-slate-700">{room.size}</p>
            </div>
            <div>
              <span className="text-slate-400">Số người ở tối đa</span>
              <p className="mt-1 font-semibold text-slate-700">
                {room.occupants}
              </p>
            </div>
            <div>
              <span className="text-slate-400">Vị trí</span>
              <p className="mt-1 font-semibold text-slate-700">{room.floor}</p>
            </div>
            <div>
              <span className="text-slate-400">Ngày trống</span>
              <p className="mt-1 font-semibold text-[#087775]">
                {room.available}
              </p>
            </div>
          </div>
          <div className="mt-8">
            <h3 className="font-display text-[19px] font-bold text-slate-900">
              Trang bị & tiện ích
            </h3>
            <div className="mt-3 flex flex-wrap gap-2">
              {room.detailAmenities.map((amenity) => (
                <span key={amenity} className="room-amenity">
                  {amenity}
                </span>
              ))}
            </div>
          </div>
          <div className="mt-8">
            <h3 className="font-display text-[19px] font-bold text-slate-900">
              Mô tả
            </h3>
            <p className="mt-3 text-[14px] leading-7 text-slate-500">
              {room.description}
            </p>
          </div>
          <div className="mt-9 grid grid-cols-2 gap-3">
            <button
              onClick={onLoginRequired}
              className="rounded-lg bg-teal-50 py-3.5 text-[13px] font-bold text-[#087775]"
            >
              Đặt lịch xem
            </button>
            <button
              onClick={onLoginRequired}
              className="rounded-lg bg-[#087775] py-3.5 text-[13px] font-bold text-white shadow-sm"
            >
              Đặt cọc giữ phòng
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}
