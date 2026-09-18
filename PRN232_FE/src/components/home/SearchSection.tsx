import Icon from "@/components/common/Icon"
import FilterChips from "@/components/home/FilterChips"
import HorizontalPropertyCard from "@/components/home/HorizontalPropertyCard"
import ResultsHeader from "@/components/home/ResultsHeader"
import SelectControl from "@/components/home/SelectControl"
import type { Property } from "@/types/property"

type SearchSectionProps = {
  properties: Property[]
  onMap: (property: Property) => void
}

export default function SearchSection({
  properties,
  onMap,
}: SearchSectionProps) {
  return (
    <section id="tim-phong" className="scroll-mt-24">
      <div className="mx-auto max-w-[1020px] px-6 py-10 lg:py-12">
        <div className="mb-6">
          <p className="text-[11px] font-bold uppercase tracking-[.16em] text-[#087775]">
            Khám phá lựa chọn phù hợp
          </p>
          <h2 className="mt-2 font-display text-[30px] font-bold tracking-[-.045em] text-slate-900">
            Tìm Phòng
          </h2>
        </div>
        <div className="rounded-2xl bg-white p-2 shadow-[0_14px_36px_rgba(17,68,70,.10)]">
          <div className="flex flex-col lg:flex-row lg:items-center">
            <label className="flex h-12 min-w-0 flex-1 items-center gap-3 px-4 text-slate-500">
              <Icon name="search" size={19} />
              <input
                type="search"
                aria-label="Tìm khu vực, tên đường hoặc trường học"
                placeholder="Nhập khu vực, tên đường, trường học..."
                className="min-w-0 flex-1 bg-transparent text-[14px] outline-none placeholder:text-slate-400"
              />
            </label>
            <SelectControl label="Khoảng giá" />
            <SelectControl label="Loại phòng" />
            <button className="m-1 flex h-11 items-center justify-center gap-2 rounded-xl bg-[#087775] px-6 text-[14px] font-bold text-white shadow-sm hover:bg-[#066a68]">
              <Icon name="search" size={16} />
              Tìm phòng
            </button>
          </div>
        </div>
        <FilterChips />
        <div className="mt-8">
          <ResultsHeader />
          <div className="space-y-5">
            {properties.map((property) => (
              <HorizontalPropertyCard
                key={property.id}
                property={property}
                onMap={() => onMap(property)}
              />
            ))}
          </div>
        </div>
      </div>
    </section>
  )
}
