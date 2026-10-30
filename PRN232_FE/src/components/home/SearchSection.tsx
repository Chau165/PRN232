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
}

export default function SearchSection({
  resource,
  query,
  onSearch,
  onPageChange,
}: SearchSectionProps) {
  const [draft, setDraft] = useState(() => propertySearchDraft(query))

  useEffect(() => setDraft(propertySearchDraft(query)), [query])

  function updateDraft(field: keyof PropertySearchDraft, value: string) {
    setDraft((current) => ({ ...current, [field]: value }))
  }

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    onSearch(draft)
  }

  const resultPage = resource.status === "success" ? resource.data : null

  const pageCount = resultPage
    ? Math.ceil(resultPage.totalCount / resultPage.pageSize)
    : 0

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
        <form
          onSubmit={submit}
          className="rounded-2xl bg-white p-3 shadow-[0_14px_36px_rgba(17,68,70,.10)]"
        >
          <div className="grid gap-2 md:grid-cols-2 lg:grid-cols-[minmax(220px,1.6fr)_1fr_1fr_1fr_auto]">
            <label className="flex h-11 min-w-0 items-center gap-3 rounded-lg px-3 text-slate-500">
              <Icon name="search" size={19} />
              <input
                type="search"
                aria-label="Từ khóa"
                value={draft.keyword}
                onChange={(event) => updateDraft("keyword", event.target.value)}
                placeholder="Khu vực, tên đường..."
                className="min-w-0 flex-1 bg-transparent text-[14px] outline-none placeholder:text-slate-400"
              />
            </label>
            <label className="flex h-11 items-center rounded-lg border border-slate-100 px-3 text-[13px] text-slate-500">
              <input
                aria-label="Giá tối thiểu"
                inputMode="decimal"
                value={draft.minPrice}
                onChange={(event) =>
                  updateDraft("minPrice", event.target.value)
                }
                placeholder="Giá từ"
                className="w-full min-w-0 bg-transparent outline-none"
              />
            </label>
            <label className="flex h-11 items-center rounded-lg border border-slate-100 px-3 text-[13px] text-slate-500">
              <input
                aria-label="Giá tối đa"
                inputMode="decimal"
                value={draft.maxPrice}
                onChange={(event) =>
                  updateDraft("maxPrice", event.target.value)
                }
                placeholder="Giá đến"
                className="w-full min-w-0 bg-transparent outline-none"
              />
            </label>
            <label className="flex h-11 items-center rounded-lg border border-slate-100 px-3 text-[13px] text-slate-500">
              <input
                aria-label="Loại phòng"
                value={draft.roomTypeName}
                onChange={(event) =>
                  updateDraft("roomTypeName", event.target.value)
                }
                placeholder="Loại phòng"
                className="w-full min-w-0 bg-transparent outline-none"
              />
            </label>
            <button
              type="submit"
              className="flex h-11 items-center justify-center gap-2 rounded-xl bg-[#087775] px-5 text-[14px] font-bold text-white shadow-sm hover:bg-[#066a68]"
            >
              <Icon name="search" size={16} />
              Tìm phòng
            </button>
          </div>
          <FilterChips draft={draft} onChange={updateDraft} />
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
