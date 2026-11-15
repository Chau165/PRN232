import { useEffect, useState } from "react"
import { useNavigate } from "react-router"
import { useAuth } from "@/auth/AuthContext"
import ManagementDetailDrawer from "@/components/management/ManagementDetailDrawer"
import PropertyServiceRatesPanel from "@/components/management/PropertyServiceRatesPanel"
import PropertyDetailView from "@/components/property/PropertyDetailView"
import {
  managementPropertyRoomsPath,
  managementPropertyRoomTypesPath,
} from "@/constants/routes"
import { getEquipments } from "@/services/equipmentService"
import { getPropertyRoomTypes } from "@/services/propertyRoomTypeService"
import type { EquipmentDto } from "@/types/equipment"
import type { PropertyRoomTypeDto } from "@/types/propertyRoomType"
import type { PropertyDto } from "@/types/user"
import { formatAddress } from "@/utils/publicFormatting"

type PropertyDetailDrawerProps = {
  property: PropertyDto
  onClose: () => void
  onEdit?: (property: PropertyDto) => void
  onDelete?: (property: PropertyDto) => void
  deleting?: boolean
}

export default function PropertyDetailDrawer({
  property,
  onClose,
  onEdit,
  onDelete,
  deleting = false,
}: PropertyDetailDrawerProps) {
  const { accessToken, user } = useAuth()
  const navigate = useNavigate()
  const isAdmin = user?.role === "ADMIN"
  const [roomTypes, setRoomTypes] = useState<PropertyRoomTypeDto[]>([])
  const [equipments, setEquipments] = useState<EquipmentDto[]>([])
  const [equipmentsLoading, setEquipmentsLoading] = useState(false)

  useEffect(() => {
    if (!accessToken) {
      setRoomTypes([])
      setEquipments([])
      setEquipmentsLoading(false)
      return
    }
    const controller = new AbortController()
    setEquipmentsLoading(true)

    void Promise.all([
      getPropertyRoomTypes(accessToken, property.propertyId, controller.signal).catch(
        () => [] as PropertyRoomTypeDto[],
      ),
      getEquipments(
        accessToken,
        { propertyId: property.propertyId, pageSize: 50 },
        controller.signal,
      ).catch(() => [] as EquipmentDto[]),
    ])
      .then(([loadedRoomTypes, loadedEquipments]) => {
        if (controller.signal.aborted) return
        setRoomTypes(loadedRoomTypes)
        setEquipments(
          loadedEquipments.filter(
            (item) => item.propertyId === null || item.propertyId === property.propertyId,
          ),
        )
      })
      .finally(() => {
        if (!controller.signal.aborted) setEquipmentsLoading(false)
      })

    return () => controller.abort()
  }, [accessToken, property.propertyId])

  const address = formatAddress(property.addressText, property.ward, property.city)

  return (
    <ManagementDetailDrawer
      title={property.propertyName}
      eyebrow="Chi tiết khu trọ"
      onClose={onClose}
    >
      <PropertyDetailView
        viewerRole={isAdmin ? "admin" : "manager"}
        layout="drawer"
        property={{
          propertyId: property.propertyId,
          propertyName: property.propertyName,
          address,
          primaryImageUrl: property.primaryImageUrl,
          description: property.description,
          roomCount: property.roomCount,
          ownerName: property.ownerName,
          latitude: property.latitude,
          longitude: property.longitude,
          roomTypes: roomTypes.map((item) => ({
            roomTypeId: item.roomTypeId,
            roomTypeName: item.roomTypeName,
            monthlyRent: item.monthlyRent,
            availableRoomCount: item.availableRoomCount,
            totalRoomCount: item.totalRoomCount,
            isActive: item.isActive,
          })),
          equipments: equipments.map((item) => ({
            equipmentId: item.equipmentId,
            name: item.name,
            quantity: item.quantity,
            compensationValue: item.compensationValue,
            roomCode: item.roomCode,
            status: item.status,
          })),
          equipmentsLoading,
        }}
        servicesSlot={<PropertyServiceRatesPanel propertyId={property.propertyId} />}
        actions={
          <>
            <button
              type="button"
              onClick={() => {
                onClose()
                navigate(managementPropertyRoomsPath(property.propertyId))
              }}
              className="rounded-lg bg-[#087775] px-4 py-2.5 text-[13px] font-bold text-white hover:bg-[#066a68]"
            >
              Quản lý phòng
            </button>
            {isAdmin && (
              <>
                <button
                  type="button"
                  onClick={() => {
                    onClose()
                    navigate(managementPropertyRoomTypesPath(property.propertyId))
                  }}
                  className="rounded-lg bg-emerald-50 px-4 py-2.5 text-[13px] font-bold text-emerald-800 hover:bg-emerald-100"
                >
                  Loại phòng & giá
                </button>
                {onEdit && (
                  <button
                    type="button"
                    onClick={() => onEdit(property)}
                    className="rounded-lg bg-teal-50 px-4 py-2.5 text-[13px] font-bold text-[#087775] hover:bg-teal-100"
                  >
                    Sửa khu trọ
                  </button>
                )}
                {onDelete && (
                  <button
                    type="button"
                    disabled={deleting}
                    onClick={() => onDelete(property)}
                    className="rounded-lg bg-rose-50 px-4 py-2.5 text-[13px] font-bold text-rose-700 hover:bg-rose-100 disabled:opacity-50"
                  >
                    {deleting ? "Đang xóa..." : "Xóa khu trọ"}
                  </button>
                )}
              </>
            )}
          </>
        }
      />
    </ManagementDetailDrawer>
  )
}
