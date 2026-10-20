import Icon from "@/components/common/Icon"

import type { PropertySearchDraft } from "@/utils/publicQueryState"

type FilterChipsProps = {
  draft: PropertySearchDraft

  onChange: (field: keyof PropertySearchDraft, value: string) => void
}

export default function FilterChips({ draft, onChange }: FilterChipsProps) {
  return (
    <details open className="mt-3 rounded-lg border-t border-slate-100 pt-3">
      <summary className="flex cursor-pointer list-none items-center gap-2 text-[13px] font-medium text-slate-500 hover:text-[#087775]">
        <Icon name="sliders" size={15} />
        Khu vực và tiện ích
        <Icon name="chevron" size={14} />
      </summary>
      <div className="mt-3 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        {([
          ["city", "Thành phố", draft.city],

          ["district", "Quận/Huyện", draft.district],

          ["ward", "Phường/Xã", draft.ward],

          ["amenityIds", "Mã tiện ích", draft.amenityIds],
        ] as const).map(([field, label, value]) => (
          <label key={field} className="text-[12px] font-medium text-slate-500">
            {label}
            <input
              aria-label={label}
              value={value}
              onChange={(event) => onChange(field, event.target.value)}
              placeholder={field === "amenityIds" ? "Ví dụ: 2, 5" : label}
              className="mt-1 h-10 w-full rounded-lg border border-slate-200 px-3 text-[13px] text-slate-700 outline-none focus:border-teal-600"
            />
          </label>
        ))}
      </div>
    </details>
  )
}
