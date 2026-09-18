import type { RoomRental } from "@/types/customer"
import PortalDrawer from "@/components/customer/PortalDrawer"
import StatusBadge from "@/components/customer/StatusBadge"

type CustomerRoomDrawerProps = {
  room: RoomRental
  onClose: () => void
}

export default function CustomerRoomDrawer({
  room,
  onClose,
}: CustomerRoomDrawerProps) {
  return (
    <PortalDrawer
      title={`Phòng ${room.roomCode}`}
      eyebrow={room.propertyName}
      onClose={onClose}
    >
      <div className="grid grid-cols-2 gap-2 overflow-hidden rounded-2xl bg-slate-100">
        {room.images.map((image) => (
          <img
            key={image}
            src={image}
            alt={`Không gian phòng ${room.roomCode}`}
            className="h-44 w-full object-cover"
          />
        ))}
      </div>
      <div className="mt-7 flex items-start justify-between gap-4">
        <div>
          <p className="text-[13px] text-slate-500">Giá thuê mỗi tháng</p>
          <p className="mt-1 text-[25px] font-bold tracking-[-.04em] text-[#087775]">
            {room.monthlyRent}
            <span className="ml-1 text-[13px] font-normal text-slate-400">
              / tháng
            </span>
          </p>
        </div>
        <StatusBadge tone="green">ĐANG THUÊ</StatusBadge>
      </div>
      <div className="mt-7 grid grid-cols-2 gap-3 rounded-xl bg-slate-50 p-4 text-[14px]">
        <div>
          <span className="text-slate-400">Địa chỉ</span>
          <p className="mt-1 font-semibold text-slate-700">
            {room.propertyAddress}
          </p>
        </div>
        <div>
          <span className="text-slate-400">Diện tích</span>
          <p className="mt-1 font-semibold text-slate-700">{room.roomSize}</p>
        </div>
        <div>
          <span className="text-slate-400">Thời hạn hợp đồng</span>
          <p className="mt-1 font-semibold text-slate-700">
            {room.contractStart} - {room.contractEnd}
          </p>
        </div>
        <div>
          <span className="text-slate-400">Số người ở</span>
          <p className="mt-1 font-semibold text-slate-700">{room.occupants}</p>
        </div>
      </div>
      <div className="mt-8">
        <h3 className="font-display text-[19px] font-bold text-slate-900">
          Trang thiết bị
        </h3>
        <div className="mt-3 flex flex-wrap gap-2">
          {room.equipment.map((item) => (
            <span key={item} className="room-amenity">
              {item}
            </span>
          ))}
        </div>
      </div>
      <div className="mt-8 rounded-2xl border border-slate-100 p-5">
        <div className="flex items-center justify-between">
          <h3 className="font-display text-[19px] font-bold text-slate-900">
            Hóa đơn gần nhất
          </h3>
          <StatusBadge tone="amber">{room.latestBill.status}</StatusBadge>
        </div>
        <div className="mt-4 flex items-end justify-between">
          <div>
            <p className="text-[13px] text-slate-500">
              Tháng {room.latestBill.period}
            </p>
            <p className="mt-1 text-[20px] font-bold text-[#087775]">
              {room.latestBill.total}
            </p>
          </div>
          <p className="text-[12px] text-slate-500">
            Hạn {room.latestBill.due}
          </p>
        </div>
      </div>
    </PortalDrawer>
  )
}
