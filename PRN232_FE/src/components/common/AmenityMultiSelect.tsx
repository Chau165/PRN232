import { useEffect, useId, useRef, useState, type KeyboardEvent } from "react"
import Icon from "@/components/common/Icon"
import type { PublicResourceState } from "@/hooks/usePublicApiResource"
import type { PublicAmenitySummary } from "@/types/publicBrowsing"
import { MAX_AMENITY_IDS } from "@/utils/publicBrowsingQuery"

type AmenityMultiSelectProps = {
  resource: PublicResourceState<PublicAmenitySummary[]> & { retry: () => void }
  selectedAmenityIds: number[]
  onChange: (ids: number[]) => void
}

function searchableName(value: string): string {
  return value.normalize("NFD").replace(/[\u0300-\u036f]/g, "").replace(/đ/gi, "d").toLowerCase().trim()
}

export default function AmenityMultiSelect({ resource, selectedAmenityIds, onChange }: AmenityMultiSelectProps) {
  const id = useId()
  const containerRef = useRef<HTMLDivElement>(null)
  const triggerRef = useRef<HTMLButtonElement>(null)
  const searchRef = useRef<HTMLInputElement>(null)
  const panelRef = useRef<HTMLDivElement>(null)
  const [isOpen, setIsOpen] = useState(false)
  const [amenitySearchKeyword, setAmenitySearchKeyword] = useState("")
  const availableAmenities = resource.status === "success" ? resource.data : []
  const availableIds = new Set(availableAmenities.map((amenity) => amenity.amenityId))
  const selectedIds = new Set(selectedAmenityIds)
  const selectedAmenities = availableAmenities.filter((amenity) => selectedIds.has(amenity.amenityId))
  const invalidIds = resource.status === "success" ? selectedAmenityIds.filter((amenityId) => !availableIds.has(amenityId)) : []
  const keyword = searchableName(amenitySearchKeyword)
  const filteredAmenities = availableAmenities.filter((amenity) => searchableName(amenity.name).includes(keyword))

  useEffect(() => {
    if (!isOpen) return
    searchRef.current?.focus()
    function closeOutside(event: PointerEvent) {
      if (event.target instanceof Node && !containerRef.current?.contains(event.target)) {
        setIsOpen(false)
      }
    }
    document.addEventListener("pointerdown", closeOutside)
    return () => document.removeEventListener("pointerdown", closeOutside)
  }, [isOpen])

  function closeDropdown() {
    setIsOpen(false)
    triggerRef.current?.focus()
  }

  function handleKeyboard(event: KeyboardEvent<HTMLDivElement>) {
    if (isOpen && event.key === "Enter" && event.target === searchRef.current) {
      event.preventDefault()
      const firstAmenity = filteredAmenities[0]
      if (firstAmenity) {
        toggleAmenity(firstAmenity.amenityId)
      }
      return
    }
    if (event.key === "Escape" && isOpen) {
      event.preventDefault()
      event.stopPropagation()
      closeDropdown()
    }
    if (!isOpen || !["ArrowDown", "ArrowUp"].includes(event.key)) return
    const options = [...(panelRef.current?.querySelectorAll<HTMLInputElement>('input[type="checkbox"]:not(:disabled)') ?? [])]
    if (!options.length) return
    event.preventDefault()
    const currentIndex = options.indexOf(document.activeElement as HTMLInputElement)
    const nextIndex = event.key === "ArrowDown" ? (currentIndex + 1) % options.length : (currentIndex <= 0 ? options.length - 1 : currentIndex - 1)
    options[nextIndex]?.focus()
  }

  function toggleAmenity(amenityId: number) {
    if (selectedIds.has(amenityId)) {
      onChange(selectedAmenityIds.filter((selectedId) => selectedId !== amenityId))
    } else if (selectedIds.size < MAX_AMENITY_IDS) {
      onChange([...selectedAmenityIds, amenityId])
    }
  }

  return (
    <div
      ref={containerRef}
      className="relative min-w-0"
      onKeyDown={handleKeyboard}
      onBlur={(event) => {
        if (!event.currentTarget.contains(event.relatedTarget)) setIsOpen(false)
      }}
    >
      <label id={`${id}-label`} htmlFor={`${id}-trigger`} className="text-sm font-medium text-slate-700">Tiện ích mong muốn</label>
      <div className="mt-2 flex min-h-12 flex-wrap items-center gap-2 rounded-xl border border-slate-200 bg-white p-2 focus-within:border-teal-600 focus-within:ring-2 focus-within:ring-teal-100">
        {selectedAmenities.length > 0 && <div className="flex max-h-28 w-full min-w-0 max-w-full flex-wrap gap-2 overflow-y-auto sm:w-auto">
          {selectedAmenities.map((amenity) => (
            <span key={amenity.amenityId} className="inline-flex max-w-full items-center gap-1 rounded-lg bg-teal-50 py-1 pl-2.5 pr-1 text-xs font-medium text-teal-800">
              <span className="min-w-0 break-words">{amenity.name}</span>
              <button type="button" onClick={() => toggleAmenity(amenity.amenityId)} aria-label={`Bỏ tiện ích ${amenity.name}`} className="shrink-0 rounded-md px-1.5 py-1 text-base leading-none hover:bg-teal-100 focus-visible:outline-2 focus-visible:outline-teal-700">×</button>
            </span>
          ))}
        </div>}
        <button
          id={`${id}-trigger`}
          ref={triggerRef}
          type="button"
          role="combobox"
          aria-labelledby={`${id}-label`}
          aria-expanded={isOpen}
          aria-haspopup="dialog"
          aria-controls={isOpen ? `${id}-panel` : undefined}
          onClick={() => {
            setAmenitySearchKeyword("")
            setIsOpen((open) => !open)
          }}
          onKeyDown={(event) => {
            if (!isOpen && ["ArrowDown", "ArrowUp"].includes(event.key)) {
              event.preventDefault()
              setIsOpen(true)
            }
          }}
          className="flex min-h-8 min-w-40 flex-1 items-center justify-between gap-3 rounded-lg px-2 text-left text-sm text-slate-500 outline-none focus-visible:ring-2 focus-visible:ring-teal-600"
        >
          <span>{selectedAmenities.length ? `Đã chọn ${selectedAmenities.length} tiện ích` : "Chọn tiện ích bạn cần..."}</span>
          <span className={`shrink-0 transition-transform ${isOpen ? "rotate-180" : ""}`}><Icon name="chevron" size={16} /></span>
        </button>
      </div>
      {resource.status === "loading" && <p role="status" className="mt-2 text-xs text-slate-500">Đang tải tiện ích...</p>}
      {resource.status === "error" && (
        <div role="alert" className="mt-2 text-sm text-rose-700">
          Không thể tải danh sách tiện ích.
          <button type="button" onClick={resource.retry} className="ml-2 rounded underline focus-visible:outline-2 focus-visible:outline-teal-700">Thử lại</button>
        </div>
      )}
      {invalidIds.length > 0 && (
        <div role="status" className="mt-2 text-xs leading-5 text-amber-800">
          Có {invalidIds.length} tiện ích đã chọn không còn tồn tại. Chúng sẽ được bỏ khỏi lần tìm kiếm tiếp theo.
          <button type="button" onClick={() => onChange(selectedAmenityIds.filter((amenityId) => availableIds.has(amenityId)))} className="ml-2 rounded font-semibold underline focus-visible:outline-2 focus-visible:outline-teal-700">Bỏ lựa chọn lỗi thời</button>
        </div>
      )}
      {isOpen && (
        <div ref={panelRef} id={`${id}-panel`} role="dialog" aria-labelledby={`${id}-label`} className="absolute left-0 right-0 top-full z-[1100] mt-2 overflow-hidden rounded-xl border border-slate-200 bg-white shadow-lg">
          <div className="p-3">
            <input ref={searchRef} type="search" value={amenitySearchKeyword} onChange={(event) => setAmenitySearchKeyword(event.target.value)} aria-label="Tìm tiện ích" placeholder="Tìm tiện ích..." className="h-10 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-teal-600 focus:ring-2 focus:ring-teal-100" />
          </div>
          <div className="max-h-48 overflow-y-auto px-2 pb-2" role="group" aria-label="Danh sách tiện ích">
            {resource.status === "success" && availableAmenities.length === 0 && <p className="px-3 py-4 text-sm text-slate-500">Hiện chưa có tiện ích nào.</p>}
            {availableAmenities.length > 0 && filteredAmenities.length === 0 && <p className="px-3 py-4 text-sm text-slate-500">Không tìm thấy tiện ích phù hợp.</p>}
            {filteredAmenities.map((amenity) => (
              <label key={amenity.amenityId} className="flex cursor-pointer items-center gap-3 rounded-lg px-3 py-2.5 text-sm text-slate-700 hover:bg-slate-50 has-checked:bg-teal-50 has-checked:text-teal-800 has-disabled:cursor-not-allowed has-disabled:opacity-50">
                <input type="checkbox" checked={selectedIds.has(amenity.amenityId)} disabled={!selectedIds.has(amenity.amenityId) && selectedIds.size >= MAX_AMENITY_IDS} onChange={() => toggleAmenity(amenity.amenityId)} onKeyDown={(event) => {
                  if (event.key === "Enter") {
                    event.preventDefault()
                    toggleAmenity(amenity.amenityId)
                  }
                }} className="h-4 w-4 shrink-0 accent-teal-700 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-teal-700" />
                <span className="min-w-0 break-words">{amenity.name}</span>
              </label>
            ))}
          </div>
          <div className="flex flex-wrap items-center justify-between gap-2 border-t border-slate-100 bg-slate-50 px-3 py-2.5 text-xs text-slate-600">
            <span aria-live="polite">Đã chọn {selectedAmenities.length}/{MAX_AMENITY_IDS}</span>
            <div className="flex gap-3">
              <button type="button" disabled={selectedAmenityIds.length === 0} onClick={() => onChange([])} className="rounded px-1 py-1 font-medium hover:text-teal-700 focus-visible:outline-2 focus-visible:outline-teal-700 disabled:opacity-40">Xóa lựa chọn</button>
              <button type="button" onClick={closeDropdown} className="rounded-lg bg-[#087775] px-3 py-2 font-semibold text-white hover:bg-[#066a68] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-teal-700">Hoàn tất</button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
