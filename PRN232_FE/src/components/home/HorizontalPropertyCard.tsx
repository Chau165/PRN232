import Icon from "@/components/common/Icon"
import type { Property } from "@/types/property"

function Amenity({ name }: { name: string }) {
  const icon =
    name === "Wi-Fi"
      ? "wifi"
      : name === "Máy lạnh"
        ? "snow"
        : name === "Bãi xe"
          ? "car"
          : "camera"

  return (
    <span className="flex items-center gap-1.5 text-[12px] text-slate-500">
      <Icon name={icon} size={15} />
      {name}
    </span>
  )
}

type HorizontalPropertyCardProps = {
  property: Property
  onMap: () => void
}

export default function HorizontalPropertyCard({
  property,
  onMap,
}: HorizontalPropertyCardProps) {
  return (
    <article className="group grid grid-cols-[242px_1fr_218px] overflow-hidden rounded-2xl bg-white shadow-[0_2px_16px_rgba(15,54,60,.06)] transition-shadow hover:shadow-[0_8px_28px_rgba(15,54,60,.1)]">
      <div className="relative h-[190px] overflow-hidden bg-slate-200">
        <img
          src={property.image}
          alt={`Nhà trọ ${property.name}`}
          className="h-full w-full object-cover transition duration-500 group-hover:scale-105"
        />
        <span className="absolute left-3 top-3 rounded-md bg-white/95 px-2.5 py-1 text-[12px] font-bold text-[#087775] shadow-sm">
          Còn {property.rooms} phòng
        </span>
      </div>
      <div className="min-w-0 px-6 py-5">
        <h3 className="text-[17px] font-bold tracking-[-.02em] text-slate-800">
          {property.name}
        </h3>
        <p className="mt-1 flex items-center gap-1.5 text-[13px] text-slate-500">
          <Icon name="pin" size={14} />
          {property.address}
          <span className="mx-1 text-slate-300">·</span>
          <span className="font-medium text-[#087775]">
            {property.distance}
          </span>
        </p>
        <p className="mt-3 text-[14px] leading-5 text-slate-500">
          {property.description}
        </p>
        <div className="mt-4 flex gap-4">
          {property.amenities.map((item) => (
            <Amenity key={item} name={item} />
          ))}
        </div>
      </div>
      <div className="flex flex-col border-l border-slate-100 px-5 py-5">
        <p className="text-[17px] font-bold tracking-[-.03em] text-[#087775]">
          Từ {property.price}
        </p>
        <p className="mt-0.5 text-[12px] text-slate-400">/ tháng</p>
        <p className="mt-3 text-[13px] font-medium text-slate-500">
          {property.rooms} phòng đang trống
        </p>
        <a
          href="/property/sunrise-residence"
          className="mt-auto rounded-lg bg-[#087775] py-2.5 text-center text-[13px] font-bold text-white shadow-sm hover:bg-[#066a68]"
        >
          Xem chi tiết
        </a>
        <button
          onClick={onMap}
          className="mt-2 text-[13px] font-semibold text-[#087775] hover:underline"
        >
          Xem trên bản đồ
        </button>
      </div>
    </article>
  )
}
