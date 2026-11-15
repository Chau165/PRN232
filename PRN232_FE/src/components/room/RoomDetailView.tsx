import type { ReactNode } from "react"
import type { DetailViewerRole } from "@/components/property/PropertyDetailView"
import { formatEquipmentSummary } from "@/utils/equipmentValidation"
import { formatVnd, serviceRateUnitLabel } from "@/utils/publicFormatting"
import { formatRoomFloor } from "@/utils/roomValidation"

export interface RoomDetailEquipmentItem {
  equipmentId: number
  name: string
  quantity?: number | null
  // Chỉ hiển thị cho Manager và Admin (C7); ẩn hoàn toàn với Khách (C8).
  compensationValue?: number | null
  status?: string | null
  supplier?: string | null
  warrantyExpiry?: string | null
}

export interface RoomDetailServiceItem {
  serviceId: number
  name: string
  price?: number | null
  unitCode?: string | null
  billingBasis?: string | null
  note?: string | null
}

export interface RoomDetailData {
  roomId: number
  roomCode?: string | null
  propertyId: number
  propertyName: string
  address?: string | null
  roomTypeName?: string | null
  price: number | null
  areaSqm: number
  maxOccupants?: number | null
  floor?: number | null
  description?: string | null
  status?: string | null
  images: string[]
  imageAltSubject?: string
  amenities?: Array<{ amenityId: number; name: string }>
  services?: RoomDetailServiceItem[]
  servicesLoading?: boolean
  equipments?: RoomDetailEquipmentItem[]
  equipmentsLoading?: boolean
  contractInfo?: {
    contractId: number
    startDate: string
    endDate: string
  }
}

type RoomDetailViewProps = {
  room: RoomDetailData
  viewerRole: DetailViewerRole
  mediaManagementSlot?: ReactNode
  actions?: ReactNode
}

function roomStatusLabel(status: string): string {
  const labels: Record<string, string> = {
    Available: "Trống",
    Reserved: "Đã đặt cọc",
    Occupied: "Đang thuê",
    Maintenance: "Đang sửa chữa",
  }
  return labels[status] ?? status
}

function formatPublicEquipment(item: RoomDetailEquipmentItem): string {
  if (item.quantity && item.quantity > 1) {
    return `${item.name} × ${item.quantity}`
  }
  return item.name
}

function formatContractDate(value: string): string {
  const parsed = new Date(`${value}T00:00:00`)
  return Number.isNaN(parsed.getTime())
    ? value
    : parsed.toLocaleDateString("vi-VN")
}

export default function RoomDetailView({
  room,
  viewerRole,
  mediaManagementSlot,
  actions,
}: RoomDetailViewProps) {
  const isManagement = viewerRole === "manager" || viewerRole === "admin"
  const floorLabel = formatRoomFloor(room.floor)
  const imageAltSubject =
    room.imageAltSubject || room.roomTypeName || `Phòng ${room.roomCode ?? room.roomId}`
  const description = room.description?.trim() ?? ""
  const equipments = room.equipments ?? []
  const showEquipmentsSection =
    isManagement || equipments.length > 0 || Boolean(room.equipmentsLoading)

  return (
    <div>
      {room.images.length > 0 ? (
        <div className="grid grid-cols-2 gap-2 overflow-hidden rounded-2xl bg-slate-100">
          {room.images.map((image, index) => (
            <img
              key={`${image}-${index}`}
              src={image}
              alt={`Ảnh ${imageAltSubject}`}
              className="max-h-[300px] w-full object-cover"
            />
          ))}
        </div>
      ) : (
        <div
          role="img"
          aria-label="Ảnh phòng chưa được cung cấp"
          className="grid h-[220px] place-items-center rounded-2xl bg-slate-100 text-[14px] text-slate-500"
        >
          Ảnh phòng chưa được cung cấp
        </div>
      )}

      <section className="mt-7 flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-[12px] text-slate-500">Giá thuê</p>
          <p className="mt-1 text-[26px] font-bold tracking-[-.04em] text-[#087775]">
            {formatVnd(room.price)}
          </p>
        </div>
        <div className="flex flex-wrap items-center justify-end gap-x-4 gap-y-1 text-[13px] font-medium text-slate-600">
          <span>{room.areaSqm} m²</span>
          {room.maxOccupants !== undefined && room.maxOccupants !== null && (
            <span>{`Tối đa ${room.maxOccupants} người`}</span>
          )}
          {floorLabel && <span>{floorLabel}</span>}
        </div>
      </section>

      {isManagement && (
        <section
          aria-label="Thông tin nội bộ phòng"
          className="mt-6 grid grid-cols-2 gap-3 rounded-xl border border-slate-200/80 bg-slate-50 p-4 text-sm text-slate-700 sm:grid-cols-3"
        >
          <div>
            <span className="text-xs text-slate-400">Mã phòng</span>
            <p className="mt-0.5 font-semibold text-slate-800">
              {room.roomCode ? `Phòng ${room.roomCode}` : "Chưa có"}
            </p>
          </div>
          <div>
            <span className="text-xs text-slate-400">Loại phòng</span>
            <p className="mt-0.5 font-semibold text-slate-800">
              {room.roomTypeName || "Chưa cập nhật"}
            </p>
          </div>
          <div>
            <span className="text-xs text-slate-400">Trạng thái</span>
            <p className="mt-0.5 font-semibold text-slate-800">
              {room.status ? roomStatusLabel(room.status) : "Chưa cập nhật"}
            </p>
          </div>
          {!floorLabel && (
            <div>
              <span className="text-xs text-slate-400">Tầng</span>
              <p className="mt-0.5 font-semibold text-slate-800">Chưa cập nhật</p>
            </div>
          )}
        </section>
      )}

      {viewerRole === "customer" && room.contractInfo && (
        <section
          aria-label="Thông tin hợp đồng thuê"
          className="mt-6 grid grid-cols-2 gap-3 rounded-xl bg-slate-50 p-4 text-[14px]"
        >
          <div>
            <span className="text-slate-400">Thời hạn hợp đồng</span>
            <p className="mt-1 font-semibold text-slate-700">
              {formatContractDate(room.contractInfo.startDate)} -{" "}
              {formatContractDate(room.contractInfo.endDate)}
            </p>
          </div>
          <div>
            <span className="text-slate-400">Mã hợp đồng</span>
            <p className="mt-1 font-semibold text-slate-700">
              #{room.contractInfo.contractId}
            </p>
          </div>
        </section>
      )}

      <section className="mt-7">
        <h3 className="font-display text-[19px] font-bold text-slate-900">
          Mô tả phòng
        </h3>
        {description ? (
          <p className="mt-2 whitespace-pre-line text-[14px] leading-6 text-slate-600">
            {room.description}
          </p>
        ) : (
          <p className="mt-2 text-[14px] text-slate-500">Chưa có mô tả</p>
        )}
      </section>

      <section className="mt-7">
        <h3 className="font-display text-[19px] font-bold text-slate-900">
          Địa chỉ
        </h3>
        <p className="mt-2 text-[14px] text-slate-600">{room.propertyName}</p>
        {room.address ? (
          <p className="mt-1 text-[14px] leading-6 text-slate-500">
            {room.address}
          </p>
        ) : isManagement ? (
          <p className="mt-1 text-[14px] text-slate-400">
            Chưa cập nhật địa chỉ khu trọ.
          </p>
        ) : null}
      </section>

      {room.amenities !== undefined && (
        <section className="mt-7">
          <h3 className="font-display text-[19px] font-bold text-slate-900">
            Tiện ích phòng
          </h3>
          {room.amenities.length > 0 ? (
            <ul className="mt-3 flex flex-wrap gap-2">
              {room.amenities.map((amenity) => (
                <li key={amenity.amenityId} className="room-amenity">
                  {amenity.name}
                </li>
              ))}
            </ul>
          ) : (
            <p className="mt-2 text-[14px] text-slate-500">
              {isManagement
                ? "Chưa có thông tin tiện ích."
                : "Chưa có thông tin tiện ích công khai."}
            </p>
          )}
        </section>
      )}

      {(room.services !== undefined || room.servicesLoading) && (
        <section className="mt-7">
          <h3 className="font-display text-[19px] font-bold text-slate-900">
            Dịch vụ
          </h3>
          {room.servicesLoading ? (
            <p role="status" className="mt-2 text-[14px] text-slate-500">
              Đang tải dịch vụ...
            </p>
          ) : room.services && room.services.length > 0 ? (
            <ul className="mt-3 divide-y divide-slate-100 rounded-xl bg-slate-50 px-4">
              {room.services.map((service) => {
                const unitLabel =
                  service.unitCode && service.billingBasis
                    ? serviceRateUnitLabel(
                        service.unitCode,
                        service.billingBasis,
                      )
                    : null

                return (
                  <li
                    key={service.serviceId}
                    className="flex justify-between gap-4 py-3 text-[14px]"
                  >
                    <span className="text-slate-600">{service.name}</span>
                    {service.price !== undefined &&
                    service.price !== null &&
                    unitLabel ? (
                      <strong className="font-semibold text-slate-800">
                        {formatVnd(service.price)}/{unitLabel}
                      </strong>
                    ) : (
                      <span className="text-right text-[12px] text-slate-500">
                        {service.note ?? "Biểu phí theo khu trọ đang được cập nhật."}
                      </span>
                    )}
                  </li>
                )
              })}
            </ul>
          ) : (
            <p className="mt-2 text-[14px] text-slate-500">
              {isManagement
                ? "Chưa có bảng giá dịch vụ đang áp dụng."
                : "Chưa có thông tin dịch vụ công khai."}
            </p>
          )}
        </section>
      )}

      {showEquipmentsSection && (
        <section className="mt-7">
          <h3 className="font-display text-[19px] font-bold text-slate-900">
            Thiết bị trong phòng
          </h3>
          {room.equipmentsLoading ? (
            <p role="status" className="mt-2 text-[14px] text-slate-500">
              Đang tải thiết bị...
            </p>
          ) : equipments.length === 0 ? (
            <p className="mt-2 text-[14px] text-slate-500">
              Chưa có thiết bị nào trong phòng.
            </p>
          ) : isManagement ? (
            <ul className="mt-3 divide-y divide-slate-100 rounded-xl border border-slate-100 bg-white">
              {equipments.map((equipment) => (
                <li
                  key={equipment.equipmentId}
                  className="px-4 py-3 text-sm"
                >
                  <p className="font-semibold text-slate-800">
                    {formatEquipmentSummary({
                      name: equipment.name,
                      quantity: equipment.quantity ?? 1,
                      compensationValue: equipment.compensationValue ?? 0,
                    })}
                  </p>
                  {(equipment.supplier || equipment.warrantyExpiry) && (
                    <p className="mt-0.5 text-xs text-slate-500">
                      {equipment.supplier ? `NCC: ${equipment.supplier}` : ""}
                      {equipment.supplier && equipment.warrantyExpiry ? " · " : ""}
                      {equipment.warrantyExpiry
                        ? `Bảo hành đến ${formatContractDate(equipment.warrantyExpiry)}`
                        : ""}
                    </p>
                  )}
                </li>
              ))}
            </ul>
          ) : (
            <ul className="mt-3 flex flex-wrap gap-2">
              {equipments.map((equipment) => (
                <li key={equipment.equipmentId} className="room-amenity">
                  {formatPublicEquipment(equipment)}
                </li>
              ))}
            </ul>
          )}
        </section>
      )}

      {isManagement && mediaManagementSlot && (
        <div className="mt-7 border-t border-slate-100 pt-1">
          {mediaManagementSlot}
        </div>
      )}

      {actions && (
        <div
          aria-label="Thao tác phòng"
          className="mt-9 border-t border-slate-100 pt-5"
        >
          {actions}
        </div>
      )}
    </div>
  )
}
