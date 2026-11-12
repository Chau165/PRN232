import AmenityMultiSelect from "@/components/common/AmenityMultiSelect"
import type { usePublicAmenities } from "@/hooks/usePublicAmenities"
import type { PropertySearchDraft } from "@/utils/publicQueryState"

type FilterChipsProps = {
  draft: PropertySearchDraft
  amenities: ReturnType<typeof usePublicAmenities>
  onChange: <Field extends keyof PropertySearchDraft>(field: Field, value: PropertySearchDraft[Field]) => void
}

export default function FilterChips({ draft, amenities, onChange }: FilterChipsProps) {
  return (
    <div className="space-y-5">
      <div className="grid gap-4 sm:grid-cols-3">
        {([
          ["city", "Thành phố"],
          ["district", "Quận/Huyện"],
          ["ward", "Phường/Xã"],
        ] as const).map(([field, label]) => (
          <label key={field} className="text-sm font-medium text-slate-700">
            {label}
            <input
              value={draft[field]}
              onChange={(event) => onChange(field, event.target.value)}
              placeholder={label}
              className="mt-2 h-12 w-full rounded-xl border border-slate-200 bg-white px-3 text-sm text-slate-800 outline-none placeholder:text-slate-400 focus:border-teal-600 focus:ring-2 focus:ring-teal-100"
            />
          </label>
        ))}
      </div>
      <AmenityMultiSelect
        resource={amenities}
        selectedAmenityIds={draft.amenityIds}
        onChange={(ids) => onChange("amenityIds", ids)}
      />
    </div>
  )
}
