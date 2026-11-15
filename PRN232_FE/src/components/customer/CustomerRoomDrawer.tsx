import type { Rental } from "@/types/rental"
import PortalDrawer from "@/components/customer/PortalDrawer"
import RoomDetailView from "@/components/room/RoomDetailView"

type CustomerRoomDrawerProps = {
  room: Rental
  onClose: () => void
  onReportDamage?: (room: Rental) => void
}

export default function CustomerRoomDrawer({
  room,
  onClose,
  onReportDamage,
}: CustomerRoomDrawerProps) {
  const images =
    room.images.length > 0
      ? room.images
      : room.primaryImageUrl
        ? [room.primaryImageUrl]
        : []

  return (
    <PortalDrawer
      title={`Phòng ${room.roomCode}`}
      eyebrow={room.propertyName}
      onClose={onClose}
    >
      <RoomDetailView
        viewerRole="customer"
        room={{
          roomId: room.roomId,
          roomCode: room.roomCode,
          propertyId: room.propertyId,
          propertyName: room.propertyName,
          address: room.propertyAddress,
          roomTypeName: room.roomTypeName,
          price: room.monthlyRent,
          areaSqm: room.areaSqm,
          images,
          imageAltSubject: `Phòng ${room.roomCode}`,
          amenities: room.amenities,
          equipments: room.equipments.map((item) => ({
            equipmentId: item.equipmentId,
            name: item.name,
            status: item.status,
          })),
          contractInfo: {
            contractId: room.contractId,
            startDate: room.startDate,
            endDate: room.endDate,
          },
        }}
        actions={
          onReportDamage ? (
            <button
              type="button"
              onClick={() => onReportDamage(room)}
              className="w-full rounded-lg bg-teal-50 py-3 text-center text-[13px] font-bold text-[#087775] hover:bg-teal-100"
            >
              Báo hư hỏng
            </button>
          ) : undefined
        }
      />
    </PortalDrawer>
  )
}
