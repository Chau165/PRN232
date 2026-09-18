import Icon from "@/components/common/Icon"
import type { Property } from "@/types/property"
import { properties } from "@/data/properties"

type MapSectionProps = {
  selected: Property
  onSelect: (property: Property) => void
}

export default function MapSection({ selected, onSelect }: MapSectionProps) {
  return (
    <section id="khu-vuc" className="scroll-mt-24">
      <div className="mx-auto max-w-[1180px] px-6 pb-20 pt-4 lg:pt-8">
        <div className="mb-6">
          <p className="text-[11px] font-bold uppercase tracking-[.16em] text-[#087775]">
            Khám phá khu vực
          </p>
          <h2 className="mt-1 font-display text-[27px] font-bold tracking-[-.04em] text-slate-900">
            Khu Vực
          </h2>
          <p className="mt-1 text-[13px] text-slate-500">
            So sánh khoảng cách và tìm khu vực thuận tiện nhất cho bạn.
          </p>
        </div>
        <div className="relative h-[560px] overflow-hidden rounded-3xl bg-[#e9f0f2] shadow-[0_6px_28px_rgba(15,54,60,.1)]">
          <div className="map-roads absolute inset-0" />
          <div className="absolute left-[22%] top-[20%] h-[48%] w-[55%] rounded-[47%] border-[23px] border-[#d8e8e1]/80 bg-[#dfede6]/80" />
          <div className="absolute left-[17%] top-[18%] text-[10px] font-bold tracking-[.16em] text-slate-400">
            THỦ ĐỨC
          </div>
          <div className="absolute left-[8%] top-[70%] text-[10px] font-bold tracking-[.12em] text-slate-400">
            LINH CHIỂU
          </div>
          <div className="absolute right-[12%] top-[31%] text-[10px] font-bold tracking-[.12em] text-slate-400">
            BÌNH THỌ
          </div>
          <div className="absolute left-[47%] top-[47%] z-10">
            <span className="relative flex h-5 w-5">
              <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-blue-400 opacity-50" />
              <span className="relative inline-flex h-5 w-5 rounded-full border-[3px] border-white bg-blue-600 shadow-md" />
            </span>
          </div>
          {properties.map((property) => (
            <button
              key={property.id}
              onClick={() => onSelect(property)}
              style={{ left: property.x, top: property.y }}
              className={`absolute z-20 -translate-x-1/2 -translate-y-1/2 rounded-full border-2 border-white px-3 py-1.5 text-[11px] font-bold shadow-lg transition hover:scale-105 ${
                selected.id === property.id
                  ? "bg-[#087775] text-white ring-4 ring-teal-100"
                  : "bg-white text-[#087775]"
              }`}
            >
              {property.price.replace(".000.000đ", "tr")}
            </button>
          ))}
          <div className="absolute right-5 top-5 z-20 overflow-hidden rounded-xl bg-white shadow-sm">
            <button className="grid h-10 w-10 place-items-center border-b border-slate-100 text-xl text-slate-700">
              +
            </button>
            <button className="grid h-10 w-10 place-items-center text-xl text-slate-700">
              −
            </button>
          </div>
          <button className="absolute bottom-5 right-5 z-20 grid h-11 w-11 place-items-center rounded-xl bg-white text-[#087775] shadow-sm">
            <Icon name="gps" size={20} />
          </button>
          <div className="absolute bottom-5 left-5 z-30 flex w-[310px] gap-3 rounded-2xl bg-white p-3 shadow-[0_12px_28px_rgba(24,54,58,.16)]">
            <img
              src={selected.image}
              alt={selected.name}
              className="h-[80px] w-[90px] rounded-xl object-cover"
            />
            <div className="min-w-0 flex-1">
              <p className="truncate text-[13px] font-bold text-slate-800">
                {selected.name}
              </p>
              <p className="mt-1 text-[12px] font-bold text-[#087775]">
                Từ {selected.price}
                <span className="font-normal text-slate-400"> / tháng</span>
              </p>
              <p className="mt-1 text-[11px] text-slate-500">
                {selected.distance} · {selected.rooms} phòng trống
              </p>
              <button className="mt-2 text-[11px] font-bold text-[#087775]">
                Xem chi tiết <span>→</span>
              </button>
            </div>
          </div>
        </div>
      </div>
    </section>
  )
}
