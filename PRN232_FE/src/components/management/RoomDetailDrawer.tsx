import ManagementDetailDrawer from "@/components/management/ManagementDetailDrawer"
import type { ManagementRoomDto } from "@/types/managementRoom"

type RoomDetailDrawerProps = {
  room: ManagementRoomDto
  onClose: () => void
}

export default function RoomDetailDrawer({
  room,
  onClose,
}: RoomDetailDrawerProps) {
  return (
    <ManagementDetailDrawer
      title={`Phòng ${room.roomCode}`}
      eyebrow="Chi tiết phòng"
      onClose={onClose}
    >
      <div className="space-y-4 rounded-xl bg-slate-50 p-4 text-sm text-slate-700">
        <p>
          <span className="text-slate-400">Khu trọ:</span>{" "}
          <strong>{room.propertyName}</strong>
        </p>
        <p>
          <span className="text-slate-400">Loại phòng:</span>{" "}
          <strong>{room.roomTypeName}</strong>
        </p>
        <p>
          <span className="text-slate-400">Giá loại phòng:</span>{" "}
          <strong>
            {new Intl.NumberFormat("vi-VN", {
              style: "currency",
              currency: "VND",
            }).format(room.roomTypePrice)}
          </strong>
        </p>
        <p>
          <span className="text-slate-400">Diện tích:</span>{" "}
          <strong>{room.areaSqm} m²</strong>
        </p>
        <p>
          <span className="text-slate-400">Trạng thái:</span>{" "}
          <strong>{room.status}</strong>
        </p>
      </div>
    </ManagementDetailDrawer>
  )
}
