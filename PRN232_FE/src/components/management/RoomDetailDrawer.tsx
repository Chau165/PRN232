import { useEffect, useState } from "react"
import { useNavigate } from "react-router"
import { useAuth } from "@/auth/AuthContext"
import ManagementDetailDrawer from "@/components/management/ManagementDetailDrawer"
import RoomImagesManager from "@/components/management/RoomImagesManager"
import RoomDetailView from "@/components/room/RoomDetailView"
import { managementRoomEquipmentPath } from "@/constants/routes"
import { getEquipments } from "@/services/equipmentService"
import { getProperty } from "@/services/propertyService"
import { getPropertyServiceRates } from "@/services/serviceRateService"
import type { EquipmentDto } from "@/types/equipment"
import type { ManagementRoomDto, RoomImageDto } from "@/types/managementRoom"
import type { PropertyServiceRateDto } from "@/types/serviceRate"
import type { PropertyDto } from "@/types/user"
import { formatAddress } from "@/utils/publicFormatting"

type RoomDetailDrawerProps = {
  room: ManagementRoomDto
  property?: PropertyDto
  onClose: () => void
  accessToken: string
  onImagesChanged: () => void
  onEdit?: (room: ManagementRoomDto) => void
  onDelete?: (room: ManagementRoomDto) => void
  deleting?: boolean
}

function currentMonthStart(now = new Date()) {
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-01`
}

function isRateActive(rate: PropertyServiceRateDto, month: string) {
  return (
    rate.effectiveFrom <= month &&
    (rate.effectiveTo === null || rate.effectiveTo > month)
  )
}

export default function RoomDetailDrawer({
  room,
  property: initialProperty,
  onClose,
  accessToken,
  onImagesChanged,
  onEdit,
  onDelete,
  deleting = false,
}: RoomDetailDrawerProps) {
  const { user } = useAuth()
  const navigate = useNavigate()
  const isAdmin = user?.role === "ADMIN"
  const [loadedProperty, setLoadedProperty] = useState<PropertyDto | undefined>(
    initialProperty,
  )
  const [roomImages, setRoomImages] = useState<RoomImageDto[] | null>(null)
  const [equipments, setEquipments] = useState<EquipmentDto[]>([])
  const [equipmentsLoading, setEquipmentsLoading] = useState(true)
  const [serviceRates, setServiceRates] = useState<PropertyServiceRateDto[]>([])
  const [servicesLoading, setServicesLoading] = useState(true)

  useEffect(() => {
    setLoadedProperty(initialProperty)
  }, [initialProperty])

  useEffect(() => {
    setRoomImages(null)
    const controller = new AbortController()
    setEquipmentsLoading(true)
    setServicesLoading(true)

    const loadProperty =
      initialProperty && initialProperty.propertyId === room.propertyId
        ? Promise.resolve(initialProperty)
        : getProperty(accessToken, room.propertyId, controller.signal).catch(
            () => undefined,
          )

    void Promise.all([
      loadProperty,
      getEquipments(
        accessToken,
        { roomId: room.roomId, pageSize: 50 },
        controller.signal,
      ).catch(() => [] as EquipmentDto[]),
      getPropertyServiceRates(
        accessToken,
        room.propertyId,
        controller.signal,
      ).catch(() => [] as PropertyServiceRateDto[]),
    ])
      .then(([resolvedProperty, loadedEquipments, loadedRates]) => {
        if (controller.signal.aborted) return
        if (resolvedProperty) setLoadedProperty(resolvedProperty)
        setEquipments(
          loadedEquipments.filter((item) => item.roomId === room.roomId),
        )
        const thisMonth = currentMonthStart()
        setServiceRates(
          loadedRates.filter((rate) => isRateActive(rate, thisMonth)),
        )
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setEquipmentsLoading(false)
          setServicesLoading(false)
        }
      })

    return () => controller.abort()
  }, [accessToken, initialProperty, room.propertyId, room.roomId])

  const imageUrls =
    roomImages && roomImages.length > 0
      ? roomImages.map((item) => item.imageUrl)
      : room.primaryImageUrl
        ? [room.primaryImageUrl]
        : []

  const propertySource =
    loadedProperty?.propertyId === room.propertyId ? loadedProperty : undefined
  const address = propertySource
    ? formatAddress(
        propertySource.addressText,
        propertySource.ward,
        propertySource.city,
      )
    : ""

  return (
    <ManagementDetailDrawer
      title={`Phòng ${room.roomCode}`}
      eyebrow="Chi tiết phòng"
      onClose={onClose}
    >
      <RoomDetailView
        viewerRole={isAdmin ? "admin" : "manager"}
        room={{
          roomId: room.roomId,
          roomCode: room.roomCode,
          propertyId: room.propertyId,
          propertyName: room.propertyName,
          address,
          roomTypeName: room.roomTypeName,
          price: room.roomTypePrice,
          areaSqm: room.areaSqm,
          maxOccupants: room.maxOccupants,
          floor: room.floor,
          description: room.description,
          status: room.status,
          images: imageUrls,
          imageAltSubject: `Phòng ${room.roomCode}`,
          services: serviceRates.map((rate) => ({
            serviceId: rate.serviceId,
            name: rate.serviceName,
            price: rate.price,
            unitCode: rate.unitCode,
            billingBasis: rate.billingBasis,
          })),
          servicesLoading,
          equipments: equipments.map((item) => ({
            equipmentId: item.equipmentId,
            name: item.name,
            quantity: item.quantity,
            compensationValue: item.compensationValue,
            status: item.status,
            supplier: item.supplier,
            warrantyExpiry: item.warrantyExpiry,
          })),
          equipmentsLoading,
        }}
        mediaManagementSlot={
          <RoomImagesManager
            key={room.roomId}
            accessToken={accessToken}
            roomId={room.roomId}
            onImagesChanged={onImagesChanged}
            onImagesLoaded={setRoomImages}
          />
        }
        actions={
          <div className="flex flex-wrap gap-2.5">
            <button
              type="button"
              onClick={() => {
                onClose()
                navigate(managementRoomEquipmentPath(room.propertyId, room.roomId))
              }}
              className="rounded-lg bg-[#087775] px-4 py-2.5 text-[13px] font-bold text-white hover:bg-[#066a68]"
            >
              {isAdmin ? "Thêm / Quản lý thiết bị" : "Quản lý thiết bị"}
            </button>
            {isAdmin && onEdit && (
              <button
                type="button"
                onClick={() => onEdit(room)}
                className="rounded-lg bg-teal-50 px-4 py-2.5 text-[13px] font-bold text-[#087775] hover:bg-teal-100"
              >
                Sửa phòng
              </button>
            )}
            {isAdmin && onDelete && (
              <button
                type="button"
                disabled={deleting}
                onClick={() => onDelete(room)}
                className="rounded-lg bg-rose-50 px-4 py-2.5 text-[13px] font-bold text-rose-700 hover:bg-rose-100 disabled:opacity-50"
              >
                {deleting ? "Đang xóa..." : "Xóa phòng"}
              </button>
            )}
          </div>
        }
      />
    </ManagementDetailDrawer>
  )
}
