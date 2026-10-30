import type { Rental } from "@/types/rental"
import PortalDrawer from "@/components/customer/PortalDrawer"

type CustomerRoomDrawerProps = {
  room: Rental
  onClose: () => void
}

export default function CustomerRoomDrawer({
  room,
  onClose,
}: CustomerRoomDrawerProps) {
  const images = room.images.length > 0 ? room.images : room.primaryImageUrl ? [room.primaryImageUrl] : []
  return (
    <PortalDrawer
      title={`Phòng ${room.roomCode}`}
      eyebrow={room.propertyName}
      onClose={onClose}
    >
      {images.length > 0 ? <div className="grid grid-cols-2 gap-2 overflow-hidden rounded-2xl bg-slate-100">
        {images.map((image) => <img key={image} src={image} alt={`Không gian phòng ${room.roomCode}`} className="h-44 w-full object-cover" />)}
      </div> : <p role="status" className="rounded-xl bg-slate-50 p-6 text-center text-[13px] text-slate-500">Chưa có hình ảnh phòng.</p>}
      <div className="mt-7 flex items-start justify-between gap-4">
        <div>
          <p className="text-[13px] text-slate-500">Giá thuê mỗi tháng</p>
          <p className="mt-1 text-[25px] font-bold tracking-[-.04em] text-[#087775]">
            {formatCurrency(room.monthlyRent)}
            <span className="ml-1 text-[13px] font-normal text-slate-400">
              / tháng
            </span>
          </p>
        </div>
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
          <p className="mt-1 font-semibold text-slate-700">{room.areaSqm} m²</p>
        </div>
        <div>
          <span className="text-slate-400">Thời hạn hợp đồng</span>
          <p className="mt-1 font-semibold text-slate-700">
            {formatDate(room.startDate)} - {formatDate(room.endDate)}
          </p>
        </div>
        <div>
          <span className="text-slate-400">Mã hợp đồng</span>
          <p className="mt-1 font-semibold text-slate-700">#{room.contractId}</p>
        </div>
      </div>
      <div className="mt-8">
        <h3 className="font-display text-[19px] font-bold text-slate-900">
          Trang thiết bị
        </h3>
        <div className="mt-3 flex flex-wrap gap-2">
          {room.equipments.map((item) => (
            <span key={item.equipmentId} className="room-amenity">
              {item.name}
            </span>
          ))}
        </div>
      </div>
      <div className="mt-8">
        <h3 className="font-display text-[19px] font-bold text-slate-900">Tiện nghi</h3>
        <div className="mt-3 flex flex-wrap gap-2">
          {room.amenities.map((item) => <span key={item.amenityId} className="room-amenity">{item.name}</span>)}
        </div>
      </div>
    </PortalDrawer>
  )
}

function formatCurrency(amount: number) {
  return new Intl.NumberFormat("vi-VN", { style: "currency", currency: "VND" }).format(amount)
}

function formatDate(value: string) {
  return new Date(`${value}T00:00:00`).toLocaleDateString("vi-VN")
}
