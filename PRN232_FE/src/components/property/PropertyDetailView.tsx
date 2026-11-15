import type { ReactNode } from "react"
import Icon, { type IconName } from "@/components/common/Icon"
import DetailSection from "@/components/property/DetailSection"
import { formatEquipmentSummary } from "@/utils/equipmentValidation"
import { formatVnd, serviceRateUnitLabel } from "@/utils/publicFormatting"

export type DetailViewerRole = "guest" | "customer" | "manager" | "admin"

export interface PropertyDetailAmenity {
  amenityId: number
  name: string
  isCommon?: boolean
  rentableRoomCount?: number
}

export interface PropertyDetailServiceItem {
  key: string
  serviceName: string
  price: number
  unitCode: string
  billingBasis: string
}

export interface PropertyDetailEquipmentItem {
  equipmentId: number
  name: string
  quantity?: number | null
  compensationValue?: number | null
  roomCode?: string | null
  status?: string | null
}

export interface PropertyDetailRoomTypePrice {
  roomTypeId: number
  roomTypeName: string
  monthlyRent: number
  availableRoomCount?: number
  totalRoomCount?: number
  isActive?: boolean
}

export interface PropertyDetailData {
  propertyId: number
  propertyName: string
  address: string
  primaryImageUrl?: string | null
  description?: string | null
  startingPrice?: number | null
  availableRoomCount?: number
  roomCount?: number
  roomTypes?: PropertyDetailRoomTypePrice[]
  amenities?: PropertyDetailAmenity[]
  services?: PropertyDetailServiceItem[]
  equipments?: PropertyDetailEquipmentItem[]
  equipmentsLoading?: boolean
  // Thông tin nội bộ (chỉ hiện cho Manager và Admin).
  ownerName?: string | null
  latitude?: number | null
  longitude?: number | null
}

type PropertyDetailViewProps = {
  property: PropertyDetailData
  viewerRole: DetailViewerRole
  layout?: "page" | "drawer"
  servicesHeading?: string
  servicesSlot?: ReactNode
  actions?: ReactNode
}

function amenityIcon(name: string): IconName {
  const normalizedName = name.trim().toLowerCase()

  if (normalizedName.includes("wi-fi") || normalizedName.includes("wifi")) {
    return "wifi"
  }
  if (
    normalizedName.includes("air conditioner") ||
    normalizedName.includes("máy lạnh")
  ) {
    return "snow"
  }
  if (normalizedName.includes("parking") || normalizedName.includes("bãi xe")) {
    return "car"
  }
  if (normalizedName.includes("camera")) {
    return "camera"
  }
  if (normalizedName.includes("toilet") || normalizedName.includes("wc")) {
    return "toilet"
  }
  if (
    normalizedName.includes("washing machine") ||
    normalizedName.includes("laundry") ||
    normalizedName.includes("máy giặt")
  ) {
    return "washer"
  }
  if (
    normalizedName.includes("elevator") ||
    normalizedName.includes("thang máy")
  ) {
    return "lift"
  }
  if (normalizedName.includes("lock") || normalizedName.includes("khóa")) {
    return "lock"
  }

  return "home"
}

function serviceCostPresentation(serviceName: string): {
  label: string
  icon: IconName
} {
  const normalizedName = serviceName.trim().toLowerCase()

  if (
    normalizedName.includes("electric") ||
    normalizedName.includes("power") ||
    normalizedName.includes("điện")
  ) {
    return { label: "Điện", icon: "bolt" }
  }
  if (normalizedName.includes("water") || normalizedName.includes("nước")) {
    return { label: "Nước", icon: "droplet" }
  }
  if (
    normalizedName.includes("internet") ||
    normalizedName.includes("wi-fi") ||
    normalizedName.includes("wifi")
  ) {
    return { label: "Internet", icon: "wifi" }
  }
  if (
    normalizedName.includes("trash") ||
    normalizedName.includes("garbage") ||
    normalizedName.includes("waste") ||
    normalizedName.includes("rác")
  ) {
    return { label: "Rác", icon: "trash" }
  }

  return { label: serviceName, icon: "receipt" }
}

function formatPublicEquipment(item: PropertyDetailEquipmentItem): string {
  if (item.quantity && item.quantity > 1) {
    return `${item.name} × ${item.quantity}`
  }
  return item.name
}

function SectionBlock({
  title,
  layout,
  children,
}: {
  title: string
  layout: "page" | "drawer"
  children: ReactNode
}) {
  if (layout === "page") {
    return <DetailSection title={title}>{children}</DetailSection>
  }

  return (
    <section className="mt-6 border-t border-slate-100 pt-5">
      <h3 className="text-sm font-bold text-slate-900">{title}</h3>
      {children}
    </section>
  )
}

export default function PropertyDetailView({
  property,
  viewerRole,
  layout = "drawer",
  servicesHeading = "Chi phí sinh hoạt",
  servicesSlot,
  actions,
}: PropertyDetailViewProps) {
  const isManagement = viewerRole === "manager" || viewerRole === "admin"
  const description = property.description?.trim() ?? ""
  const amenities = property.amenities ?? []
  const showAmenitiesSection = property.amenities !== undefined
  const services = property.services ?? []
  const equipments = property.equipments
  const showEquipmentsSection =
    isManagement ||
    Boolean(equipments && equipments.length > 0) ||
    Boolean(property.equipmentsLoading)

  const activeRoomTypes = (property.roomTypes ?? []).filter(
    (item) => item.isActive !== false,
  )
  const resolvedStartingPrice =
    property.startingPrice !== undefined
      ? property.startingPrice
      : activeRoomTypes.length > 0
        ? Math.min(...activeRoomTypes.map((item) => item.monthlyRent))
        : null

  return (
    <div>
      {layout === "page" ? (
        <div className="mt-8 grid grid-cols-1 items-start gap-8 lg:grid-cols-[minmax(0,1fr)_312px] lg:gap-20">
          <div>
            <h1 className="font-display text-[37px] font-bold tracking-[-.04em] text-slate-900">
              {property.propertyName}
            </h1>
            {property.address && (
              <p className="mt-2 flex items-center gap-1.5 text-[15px] text-slate-500">
                <Icon name="pin" size={16} />
                {property.address}
              </p>
            )}
            {property.availableRoomCount !== undefined && (
              <p className="mt-4 inline-flex rounded-full bg-emerald-50 px-3 py-1.5 text-[13px] font-medium text-emerald-700">
                {property.availableRoomCount} phòng hiện có thể thuê
              </p>
            )}
          </div>
          <div className="pt-1">
            <p className="text-[13px] font-medium text-slate-500">Giá từ</p>
            <p className="mt-1 text-[25px] font-bold tracking-[-.04em] text-[#087775]">
              {resolvedStartingPrice === null
                ? "Chưa có giá"
                : formatVnd(resolvedStartingPrice)}
            </p>
            {actions && <div className="mt-5 space-y-2.5">{actions}</div>}
          </div>
        </div>
      ) : (
        <div className="rounded-xl bg-slate-50 p-4 text-sm text-slate-700">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div className="min-w-0 flex-1 space-y-1.5">
              <p className="flex items-start gap-1.5 text-slate-600">
                <span className="mt-0.5 shrink-0 text-[#087775]">
                  <Icon name="pin" size={15} />
                </span>
                <strong>{property.address || "Chưa có địa chỉ"}</strong>
              </p>
              {property.roomCount !== undefined && (
                <p className="text-xs text-slate-500">
                  Quy mô: <strong className="text-slate-700">{property.roomCount} phòng</strong>
                </p>
              )}
            </div>
            <div className="text-right">
              <p className="text-xs text-slate-500">Giá từ</p>
              <p className="mt-0.5 text-xl font-bold tracking-[-.03em] text-[#087775]">
                {resolvedStartingPrice === null
                  ? "Chưa có giá"
                  : formatVnd(resolvedStartingPrice)}
              </p>
            </div>
          </div>
          {activeRoomTypes.length > 0 && (
            <div className="mt-3 flex flex-wrap gap-2 border-t border-slate-200/70 pt-3">
              {activeRoomTypes.map((roomType) => (
                <span
                  key={roomType.roomTypeId}
                  className="inline-flex items-center gap-1.5 rounded-full bg-white px-3 py-1 text-xs font-medium text-slate-700 shadow-xs"
                >
                  <span>{roomType.roomTypeName}:</span>
                  <strong className="text-[#087775]">
                    {formatVnd(roomType.monthlyRent)}
                  </strong>
                </span>
              ))}
            </div>
          )}
        </div>
      )}

      <div
        className={
          layout === "page"
            ? "mt-10 grid min-h-[280px] place-items-center overflow-hidden rounded-3xl bg-slate-100"
            : "mt-5 grid min-h-[180px] place-items-center overflow-hidden rounded-2xl bg-slate-100"
        }
      >
        {property.primaryImageUrl ? (
          <img
            src={property.primaryImageUrl}
            alt={`Ảnh ${property.propertyName}`}
            className={
              layout === "page"
                ? "h-full max-h-[480px] w-full object-cover"
                : "max-h-60 w-full object-cover"
            }
          />
        ) : (
          <div
            role="img"
            aria-label="Ảnh khu trọ chưa được cung cấp"
            className="px-6 text-center text-[14px] text-slate-500"
          >
            Ảnh khu trọ chưa được cung cấp
          </div>
        )}
      </div>

      {isManagement && (
        <section
          aria-label="Thông tin nội bộ khu trọ"
          className="mt-5 grid gap-3 rounded-xl border border-slate-200/80 bg-slate-50/70 p-4 text-sm sm:grid-cols-3"
        >
          <div>
            <span className="text-xs text-slate-400">Chủ sở hữu</span>
            <p className="mt-0.5 font-semibold text-slate-800">
              {property.ownerName || "Chưa có"}
            </p>
          </div>
          <div>
            <span className="text-xs text-slate-400">Tọa độ</span>
            <p className="mt-0.5 font-semibold text-slate-800">
              {property.latitude ?? 0}, {property.longitude ?? 0}
            </p>
          </div>
          <div>
            <span className="text-xs text-slate-400">Số phòng</span>
            <p className="mt-0.5 font-semibold text-slate-800">
              {property.roomCount ?? 0}
            </p>
          </div>
        </section>
      )}

      <div className={layout === "page" ? "mt-12" : "mt-2"}>
        <SectionBlock title="Giới thiệu" layout={layout}>
          {description ? (
            <p className="mt-4 max-w-[680px] whitespace-pre-line text-[15px] leading-7 text-slate-600">
              {property.description}
            </p>
          ) : (
            <p
              role="status"
              className="mt-4 max-w-[680px] text-[15px] leading-7 text-slate-500"
            >
              Thông tin giới thiệu đang được cập nhật.
            </p>
          )}
        </SectionBlock>

        {showAmenitiesSection && (
          <SectionBlock title="Tiện ích" layout={layout}>
            {amenities.length === 0 ? (
              <p role="status" className="mt-4 text-[14px] text-slate-500">
                Chưa có thông tin tiện ích.
              </p>
            ) : (
              <ul className="mt-5 grid grid-cols-1 gap-4 sm:grid-cols-2">
                {amenities.map((amenity) => {
                  const availableRoomCount = property.availableRoomCount ?? 0
                  const roomScope =
                    amenity.rentableRoomCount === availableRoomCount &&
                    availableRoomCount > 0
                      ? "tất cả phòng hiện có thể thuê"
                      : `${amenity.rentableRoomCount} trên ${availableRoomCount} phòng hiện có thể thuê`
                  const scopeLabel =
                    amenity.rentableRoomCount === undefined
                      ? "Tiện ích chung của khu trọ"
                      : amenity.isCommon
                        ? `Có tại khu trọ và ${roomScope}`
                        : amenity.rentableRoomCount === availableRoomCount &&
                            availableRoomCount > 0
                          ? "Có tại tất cả phòng hiện có thể thuê"
                          : `Có tại ${roomScope}`

                  return (
                    <li
                      key={amenity.amenityId}
                      className="flex items-start gap-3"
                    >
                      <span
                        aria-hidden="true"
                        className="grid h-9 w-9 shrink-0 place-items-center rounded-lg bg-[#f1f8f7] text-[#087775]"
                      >
                        <Icon name={amenityIcon(amenity.name)} size={17} />
                      </span>
                      <span>
                        <span className="block text-[14px] font-medium text-slate-700">
                          {amenity.name}
                        </span>
                        <span className="mt-1 block text-[12px] text-slate-500">
                          {scopeLabel}
                        </span>
                      </span>
                    </li>
                  )
                })}
              </ul>
            )}
          </SectionBlock>
        )}

        {showEquipmentsSection && (
          <SectionBlock title="Danh sách thiết bị" layout={layout}>
            {property.equipmentsLoading ? (
              <p role="status" className="mt-3 text-sm text-slate-500">
                Đang tải thiết bị...
              </p>
            ) : !equipments || equipments.length === 0 ? (
              <p className="mt-3 text-sm text-slate-500">
                Chưa có thiết bị nào được ghi nhận tại khu trọ.
              </p>
            ) : isManagement ? (
              <ul className="mt-3 divide-y divide-slate-100 rounded-xl border border-slate-100 bg-white">
                {equipments.map((item) => (
                  <li
                    key={item.equipmentId}
                    className="flex flex-wrap items-center justify-between gap-2 px-4 py-3 text-sm"
                  >
                    <div className="min-w-0">
                      <p className="font-semibold text-slate-800">
                        {formatEquipmentSummary({
                          name: item.name,
                          quantity: item.quantity ?? 1,
                          compensationValue: item.compensationValue ?? 0,
                        })}
                      </p>
                      {item.roomCode && (
                        <p className="mt-0.5 text-xs text-slate-500">
                          Phòng {item.roomCode}
                        </p>
                      )}
                    </div>
                  </li>
                ))}
              </ul>
            ) : (
              <ul className="mt-3 flex flex-wrap gap-2">
                {equipments.map((item) => (
                  <li key={item.equipmentId} className="room-amenity">
                    {formatPublicEquipment(item)}
                  </li>
                ))}
              </ul>
            )}
          </SectionBlock>
        )}

        {servicesSlot ? (
          <div className="mt-6 border-t border-slate-100 pt-1">
            {servicesSlot}
          </div>
        ) : (
          <SectionBlock title={servicesHeading} layout={layout}>
            {services.length === 0 ? (
              <p role="status" className="mt-4 text-[14px] text-slate-500">
                Chi phí sinh hoạt đang được cập nhật.
              </p>
            ) : (
              <ul className="mt-4 divide-y divide-slate-100 rounded-xl border border-slate-100 bg-white px-4">
                {services.map((rate) => {
                  const unitLabel = serviceRateUnitLabel(
                    rate.unitCode,
                    rate.billingBasis,
                  )
                  const presentation = serviceCostPresentation(rate.serviceName)

                  return (
                    <li
                      key={rate.key}
                      className="flex flex-wrap items-center justify-between gap-3 py-3 text-[14px]"
                    >
                      <span className="flex min-w-0 items-center gap-3 text-slate-600">
                        <span
                          aria-hidden="true"
                          className="grid h-9 w-9 shrink-0 place-items-center rounded-lg bg-[#f1f8f7] text-[#087775]"
                        >
                          <Icon name={presentation.icon} size={17} />
                        </span>
                        <span>{presentation.label}</span>
                      </span>
                      {unitLabel ? (
                        <strong className="font-semibold text-[#087775]">
                          {formatVnd(rate.price)}/{unitLabel}
                        </strong>
                      ) : (
                        <span role="status" className="text-[13px] text-slate-500">
                          Đơn vị tính đang được cập nhật.
                        </span>
                      )}
                    </li>
                  )
                })}
              </ul>
            )}
          </SectionBlock>
        )}
      </div>

      {layout === "drawer" && actions && (
        <div
          aria-label="Thao tác khu trọ"
          className="mt-8 flex flex-wrap gap-2.5 border-t border-slate-100 pt-5"
        >
          {actions}
        </div>
      )}
    </div>
  )
}
