import { useEffect, useState, type FormEvent } from "react"

import type {
  PublicPage,
  PublicPropertyCard,
  PublicPropertyListQuery,
} from "@/types/publicBrowsing"

import Icon from "@/components/common/Icon"

import FilterChips from "@/components/home/FilterChips"

import HorizontalPropertyCard from "@/components/home/HorizontalPropertyCard"

import ResultsHeader from "@/components/home/ResultsHeader"

import type { PublicResourceState } from "@/hooks/usePublicApiResource"
import { usePublicAmenities } from "@/hooks/usePublicAmenities"
import { buildPropertyListParams } from "@/utils/publicBrowsingQuery"

import {
  publicApiErrorMessage,
  publicApiValidationMessages,
} from "@/utils/publicApiDisplay"

import {
  propertySearchDraft,
  type PropertySearchDraft,
} from "@/utils/publicQueryState"

type SearchSectionProps = {
  resource: PublicResourceState<PublicPage<PublicPropertyCard>> & {
    retry: () => void
  }

  query: PublicPropertyListQuery

  onSearch: (draft: PropertySearchDraft) => void

  onPageChange: (page: number) => void
  onClear: () => void
}

export default function SearchSection({
  resource,
  query,
  onSearch,
  onPageChange,
  onClear,
}: SearchSectionProps) {
  const [draft, setDraft] = useState(() => propertySearchDraft(query))
  const amenities = usePublicAmenities()
  const [filterError, setFilterError] = useState<string | null>(null)
  const canApplyAmenities = draft.amenityIds.length === 0 || amenities.status === "success"

  useEffect(() => setDraft(propertySearchDraft(query)), [query])

  function updateDraft<Field extends keyof PropertySearchDraft>(field: Field, value: PropertySearchDraft[Field]) {
    setDraft((current) => ({ ...current, [field]: value }))
  }

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (!canApplyAmenities) return
    const availableIds = new Set(amenities.status === "success" ? amenities.data.map((amenity) => amenity.amenityId) : [])
    const searchDraft = { ...draft, amenityIds: draft.amenityIds.filter((id) => availableIds.has(id)) }
    try {
      buildPropertyListParams(searchDraft)
      setFilterError(null)
      onSearch(searchDraft)
    } catch (error) {
      setFilterError(error instanceof Error ? error.message : "Hãy kiểm tra lại bộ lọc.")
    }
  }

  const resultPage = resource.status === "success" ? resource.data : null

  const pageCount = resultPage
    ? Math.ceil(resultPage.totalCount / resultPage.pageSize)
    : 0

  return (
    <section id="tim-phong" className="scroll-mt-24">
      <div className="public-container py-10 lg:py-12">
        <form
          onSubmit={submit}
          className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm sm:p-6"
        >
          <div className="mb-6">
            <h2 className="font-display text-2xl font-bold tracking-tight text-slate-900">Tìm phòng phù hợp với bạn</h2>
            <p className="mt-2 text-sm leading-6 text-slate-500">Chọn khu vực và tiện ích phù hợp với nhu cầu của bạn.</p>
          </div>
          <FilterChips draft={draft} amenities={amenities} onChange={updateDraft} />
          {filterError && <p role="alert" className="mt-3 text-sm text-rose-700">{filterError}</p>}
          <div className="mt-6 flex flex-col gap-3 border-t border-slate-100 pt-5 sm:flex-row sm:justify-end">
            <button
              type="button"
              onClick={() => {
                setDraft(propertySearchDraft({}))
                setFilterError(null)
                onClear()
              }}
              className="min-h-12 rounded-xl border border-slate-200 px-5 text-sm font-semibold text-slate-600 hover:bg-slate-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-teal-700"
            >
              Xóa bộ lọc
            </button>
            <button
              type="submit"
              disabled={!canApplyAmenities}
              className="flex min-h-12 items-center justify-center gap-2 rounded-xl bg-[#087775] px-6 text-sm font-bold text-white shadow-sm hover:bg-[#066a68] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-teal-700 disabled:opacity-50"
            >
              <Icon name="search" size={16} />
              Tìm phòng phù hợp
            </button>
          </div>
        </form>

        <div className="mt-8">
          {resource.status === "success" && resultPage && (
            <ResultsHeader
              totalCount={resultPage.totalCount}
              sortBy={draft.sortBy}
              onSortChange={(sortBy) => updateDraft("sortBy", sortBy)}
            />
          )}
          {resource.status === "loading" && (
            <p
              role="status"
              className="rounded-xl bg-white px-5 py-8 text-center text-[14px] text-slate-500"
            >
              Đang tải danh sách nhà trọ...
            </p>
          )}
          {resource.status === "error" && (
            <div
              role="alert"
              className="rounded-xl border border-rose-100 bg-white px-5 py-6 text-center"
            >
              {resource.error.kind === "validation" &&
              publicApiValidationMessages(resource.error).length > 0 ? (
                <ul className="space-y-1 text-[14px] text-rose-700">
                  {publicApiValidationMessages(resource.error).map(
                    (message, index) => (
                      <li key={`${message}-${index}`}>{message}</li>
                    ),
                  )}
                </ul>
              ) : (
                <p className="text-[14px] text-rose-700">
                  {publicApiErrorMessage(resource.error)}
                </p>
              )}
              <button
                type="button"
                onClick={resource.retry}
                className="mt-3 rounded-lg bg-[#087775] px-4 py-2 text-[13px] font-semibold text-white"
              >
                Thử lại
              </button>
            </div>
          )}
          {resource.status === "success" &&
            resultPage &&
            resultPage.items.length === 0 && (
              <p className="rounded-xl bg-white px-5 py-8 text-center text-[14px] text-slate-500">
                Không tìm thấy khu trọ phù hợp
              </p>
            )}
          {resource.status === "success" &&
            resultPage &&
            resultPage.items.length > 0 && (
              <div className="space-y-5">
                {resultPage.items.map((property) => (
                  <HorizontalPropertyCard
                    key={property.propertyId}
                    property={property}
                  />
                ))}
              </div>
            )}
          {resource.status === "success" && resultPage && pageCount > 1 && (
            <nav
              aria-label="Phân trang kết quả"
              className="mt-6 flex items-center justify-center gap-4"
            >
              <button
                type="button"
                onClick={() => onPageChange(resultPage.page - 1)}
                disabled={resultPage.page <= 1}
                className="rounded-lg border border-slate-200 px-4 py-2 text-[13px] disabled:opacity-40"
              >
                Trang trước
              </button>
              <span aria-live="polite" className="text-[13px] text-slate-500">
                Trang {resultPage.page} / {pageCount}
              </span>
              <button
                type="button"
                onClick={() => onPageChange(resultPage.page + 1)}
                disabled={resultPage.page >= pageCount}
                className="rounded-lg border border-slate-200 px-4 py-2 text-[13px] disabled:opacity-40"
              >
                Trang tiếp
              </button>
            </nav>
          )}
        </div>
      </div>
    </section>
  )
}
