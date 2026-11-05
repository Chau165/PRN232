import ManagementDetailDrawer from "@/components/management/ManagementDetailDrawer"
import type { EquipmentDto } from "@/types/equipment"

type EquipmentDetailModalProps = {
  equipment: EquipmentDto
  onClose: () => void
}

export default function EquipmentDetailModal({
  equipment,
  onClose,
}: EquipmentDetailModalProps) {
  const isUnderWarranty = Date.parse(equipment.warrantyExpiry) >= Date.now()
  return (
    <ManagementDetailDrawer
      title={equipment.name}
      eyebrow="Chi tiết thiết bị"
      onClose={onClose}
    >
      <div className="space-y-4 rounded-xl bg-slate-50 p-4 text-sm text-slate-700">
        <p>
          <span className="text-slate-400">Mã thiết bị:</span>{" "}
          <strong>#{equipment.equipmentId}</strong>
        </p>
        <p>
          <span className="text-slate-400">Khu trọ:</span>{" "}
          <strong>{equipment.propertyName || "Chưa xác định"}</strong>
        </p>
        <p>
          <span className="text-slate-400">Phòng:</span>{" "}
          <strong>{equipment.roomCode || equipment.roomId}</strong>
        </p>
        <p>
          <span className="text-slate-400">Ngày lắp đặt:</span>{" "}
          <strong>{equipment.installDate}</strong>
        </p>
        <p>
          <span className="text-slate-400">Hạn bảo hành:</span>{" "}
          <strong>{equipment.warrantyExpiry}</strong>
        </p>
        <p>
          <span className="text-slate-400">Bảo hành:</span>{" "}
          <strong>{isUnderWarranty ? "Còn bảo hành" : "Hết bảo hành"}</strong>
        </p>
        <p>
          <span className="text-slate-400">Nhà cung cấp:</span>{" "}
          <strong>{equipment.supplier}</strong>
        </p>
        <p>
          <span className="text-slate-400">Trạng thái:</span>{" "}
          <strong>{equipment.status}</strong>
        </p>
      </div>
    </ManagementDetailDrawer>
  )
}
