import AmenityMultiSelect from "@/components/common/AmenityMultiSelect"
import type { usePublicAmenities } from "@/hooks/usePublicAmenities"
import type { usePublicAreas } from "@/hooks/usePublicAreas"
import type { PublicAreaOption } from "@/types/publicBrowsing"
import type { PropertySearchDraft } from "@/utils/publicQueryState"

type AreaField = "city" | "district" | "ward"

type FilterChipsProps = {
  draft: PropertySearchDraft
  amenities: ReturnType<typeof usePublicAmenities>
  areas: ReturnType<typeof usePublicAreas>
  onChange: <Field extends keyof PropertySearchDraft>(field: Field, value: PropertySearchDraft[Field]) => void
}

const AREA_FIELDS: ReadonlyArray<readonly [AreaField, string]> = [
  ["city", "Thành phố"],
  ["district", "Quận/Huyện"],
  ["ward", "Phường/Xã"],
]

const fieldClass =
  "mt-2 h-12 w-full rounded-xl border border-slate-200 bg-white px-3 text-sm text-slate-800 outline-none placeholder:text-slate-400 focus:border-teal-600 focus:ring-2 focus:ring-teal-100 disabled:bg-slate-50"

// Each list only offers values that still match the levels chosen above it.
function optionsFor(field: AreaField, areas: PublicAreaOption[], draft: PropertySearchDraft) {
  const values = areas
    .filter((area) =>
      (field === "city" || !draft.city || area.city === draft.city) &&
      (field !== "ward" || !draft.district || area.district === draft.district))
    .map((area) => area[field])
    .filter((value) => value.trim())
  // numeric: "Quận 3" comes before "Quận 10".
  const unique = [...new Set(values)].sort((left, right) => left.localeCompare(right, "vi", { numeric: true }))
  // A value from a shared link that is not in the list stays visible, so the applied filter is not hidden.
  return draft[field] && !unique.includes(draft[field]) ? [draft[field], ...unique] : unique
}

export default function FilterChips({ draft, amenities, areas, onChange }: FilterChipsProps) {
  function chooseArea(field: AreaField, value: string) {
    onChange(field, value)
    // A new city or district invalidates the levels below it.
    if (field === "city") {
      onChange("district", "")
      onChange("ward", "")
    }
    if (field === "district") onChange("ward", "")
  }

  return (
    <div className="space-y-5">
      <div className="grid gap-4 sm:grid-cols-3">
        {AREA_FIELDS.map(([field, label]) => (
          <label key={field} className="text-sm font-medium text-slate-700">
            {label}
            {areas.status === "error" ? (
              // Without the area list, typing still works: the API normalizes names.
              <input
                value={draft[field]}
                onChange={(event) => onChange(field, event.target.value)}
                placeholder={label}
                className={fieldClass}
              />
            ) : (
              <select
                value={draft[field]}
                disabled={areas.status === "loading"}
                onChange={(event) => chooseArea(field, event.target.value)}
                className={fieldClass}
              >
                <option value="">{areas.status === "loading" ? "Đang tải..." : "Tất cả"}</option>
                {areas.status === "success" &&
                  optionsFor(field, areas.data, draft).map((value) => (
                    <option key={value} value={value}>{value}</option>
                  ))}
              </select>
            )}
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
