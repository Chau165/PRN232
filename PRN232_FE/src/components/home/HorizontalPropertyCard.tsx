import { Link } from "react-router"

import type { PublicPropertyCard } from "@/api/publicBrowsingApi"

import Icon from "@/components/common/Icon"

import { propertyDetailPath } from "@/constants/routes"

import { formatVnd } from "@/utils/publicFormatting"

type HorizontalPropertyCardProps = {
  property: PublicPropertyCard
}

export default function HorizontalPropertyCard({
  property,
}: HorizontalPropertyCardProps) {
  const address = [
    property.addressText,
    property.ward,
    property.district,
    property.city,
  ]

    .filter((part) => part.trim().length > 0)

    .join(", ")

  return (
    <article className="group grid overflow-hidden rounded-2xl bg-white shadow-[0_2px_16px_rgba(15,54,60,.06)] transition-shadow hover:shadow-[0_8px_28px_rgba(15,54,60,.1)] md:grid-cols-[220px_1fr_190px]">
      <div className="relative grid h-[190px] place-items-center overflow-hidden bg-slate-100">
        {property.primaryImageUrl ? (
          <img
            src={property.primaryImageUrl}
            alt={property.propertyName}
            className="h-full w-full object-cover transition duration-500 group-hover:scale-105"
          />
        ) : (
          <span className="text-[13px] text-slate-400">Ảnh chưa có</span>
        )}
        <span className="absolute left-3 top-3 rounded-md bg-white/95 px-2.5 py-1 text-[12px] font-bold text-[#087775] shadow-sm">
          Còn {property.availableRoomCount} phòng
        </span>
      </div>
      <div className="min-w-0 px-6 py-5">
        <h3 className="text-[17px] font-bold tracking-[-.02em] text-slate-800">
          {property.propertyName}
        </h3>
        <p className="mt-1 flex items-center gap-1.5 text-[13px] text-slate-500">
          <Icon name="pin" size={14} />
          {address || "Địa chỉ chưa được cung cấp"}
        </p>
        {property.amenitiesSummary.length > 0 && (
          <div
            className="mt-4 flex flex-wrap gap-x-4 gap-y-2"
            aria-label="Tiện ích phòng đại diện"
          >
            {property.amenitiesSummary.map((item) => (
              <span
                key={item.amenityId}
                className="flex items-center gap-1.5 text-[12px] text-slate-500"
              >
                <Icon name="check" size={14} />
                {item.name}
              </span>
            ))}
            <p className="w-full text-[12px] text-slate-400">
              Tiện ích phòng đại diện
            </p>
          </div>
        )}
      </div>
      <div className="flex flex-col border-t border-slate-100 px-5 py-5 md:border-l md:border-t-0">
        <p className="text-[17px] font-bold tracking-[-.03em] text-[#087775]">
          {property.startingPrice === null
            ? "Chưa có giá"
            : `Từ ${formatVnd(property.startingPrice)}`}
        </p>
        <p className="mt-3 text-[13px] font-medium text-slate-500">
          {property.availableRoomCount} phòng phù hợp
        </p>
        <Link
          to={propertyDetailPath(property.propertyId)}
          className="mt-auto rounded-lg bg-[#087775] py-2.5 text-center text-[13px] font-bold text-white shadow-sm hover:bg-[#066a68]"
        >
          Xem chi tiết
        </Link>
      </div>
    </article>
  )
}
