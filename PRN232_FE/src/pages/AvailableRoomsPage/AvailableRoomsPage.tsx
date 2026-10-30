import { useEffect, useMemo, useState, type FormEvent } from "react"
import { Link, useLocation, useNavigate, useParams } from "react-router"
import {
  buildRoomListParams,
  PublicApiError,
  publicApi,
} from "@/services/publicBrowsingService"
import Footer from "@/components/common/Footer"
import Navbar from "@/components/common/Navbar"
import AvailableRooms from "@/components/room/AvailableRooms"
import RoomDetailModal from "@/components/room/RoomDetailModal"
import {
  parsePositiveRouteId,
  propertyDetailPath,
  routes,
} from "@/constants/routes"
import { usePublicApiResource } from "@/hooks/usePublicApiResource"
import {
  publicApiErrorMessage,
  publicApiValidationMessages,
} from "@/utils/publicApiDisplay"
import {
  roomQueryFromSearch,
  roomSearchDraft,
  roomSearchParams,
  type RoomSearchDraft,
} from "@/utils/publicQueryState"

export default function AvailableRoomsPage() {
  const { propertyId: routePropertyId } = useParams()
  const propertyId = parsePositiveRouteId(routePropertyId)
  const location = useLocation()
  const navigate = useNavigate()
  const query = useMemo(
    () => roomQueryFromSearch(location.search),
    [location.search],
  )
  const [draft, setDraft] = useState(() => roomSearchDraft(query))
  const [filterError, setFilterError] = useState<string | null>(null)
  const [selectedRoomId, setSelectedRoomId] = useState<number | null>(null)

  useEffect(() => setDraft(roomSearchDraft(query)), [query])

  const propertyResource = usePublicApiResource(
    `property-${propertyId ?? "invalid"}`,
    (signal) =>
      propertyId === null
        ? Promise.reject(
            new PublicApiError("validation", "Mã khu trọ không hợp lệ."),
          )
        : publicApi.getProperty(propertyId, signal),
  )
  const roomsResource = usePublicApiResource(
    `rooms-${propertyId ?? "invalid"}-${location.search}`,
    (signal) =>
      propertyId === null
        ? Promise.reject(
            new PublicApiError("validation", "Mã khu trọ không hợp lệ."),
          )
        : publicApi.listRooms(propertyId, query, signal),
  )

  function updateDraft(field: keyof RoomSearchDraft, value: string) {
    setDraft((current) => ({ ...current, [field]: value }))
  }

  function submitFilters(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    try {
      buildRoomListParams({
        minPrice: draft.minPrice,
        maxPrice: draft.maxPrice,
        roomTypeName: draft.roomTypeName,
        amenityIds: draft.amenityIds.split(/[ ,]+/).filter(Boolean),
        sortBy: draft.sortBy,
      })
      setFilterError(null)
      const search = roomSearchParams(draft).toString()
      navigate({
        pathname: location.pathname,
        search: `?${search}`,
        hash: location.hash,
      })
    } catch (error) {
      setFilterError(
        error instanceof PublicApiError
          ? publicApiValidationMessages(error).join(" ")
          : "Hãy kiểm tra lại điều kiện lọc.",
      )
    }
  }

  function changePage(page: number) {
    const params = new URLSearchParams(location.search)
    params.set("page", String(page))
    navigate({
      pathname: location.pathname,
      search: `?${params.toString()}`,
      hash: location.hash,
    })
  }

  const property =
    propertyResource.status === "success" ? propertyResource.data : null
  const roomPage =
    roomsResource.status === "success" ? roomsResource.data : null
  const address = property
    ? [property.addressText, property.ward, property.district, property.city]
        .filter((part) => part?.trim())
        .join(", ")
    : ""
  const pageCount = roomPage
    ? Math.ceil(roomPage.totalCount / roomPage.pageSize)
    : 0

  return (
    <main className="min-h-screen bg-[#fcfdfd] text-slate-800">
      <Navbar />
      <div className="mx-auto max-w-[1180px] px-6 pb-20 pt-7">
        <nav
          aria-label="Breadcrumb"
          className="flex items-center gap-2 text-[13px] font-medium text-slate-400"
        >
          <Link to={routes.home} className="hover:text-[#087775]">
            Trang chủ
          </Link>
          <span aria-hidden="true">›</span>
          {property ? (
            <Link
              to={propertyDetailPath(property.propertyId)}
              className="hover:text-[#087775]"
            >
              {property.propertyName}
            </Link>
          ) : (
            <span>Khu trọ</span>
          )}
          <span aria-hidden="true">›</span>
          <span className="text-slate-600">Phòng phù hợp</span>
        </nav>

        {propertyResource.status === "loading" && (
          <p
            role="status"
            className="mt-10 rounded-2xl bg-white px-6 py-10 text-center text-slate-500"
          >
            Đang tải thông tin khu trọ...
          </p>
        )}
        {propertyResource.status === "error" && (
          <section
            role="alert"
            className="mt-10 rounded-2xl bg-white px-6 py-12 text-center"
          >
            <h1 className="font-display text-2xl font-bold text-slate-900">
              {propertyResource.error.kind === "not-found" ||
              propertyResource.error.kind === "validation"
                ? "Khu trọ hiện không khả dụng"
                : "Không thể tải thông tin khu trọ"}
            </h1>
            {propertyResource.error.kind !== "not-found" &&
              propertyResource.error.kind !== "validation" && (
                <p className="mt-2 text-[14px] text-slate-500">
                  {publicApiErrorMessage(propertyResource.error)}
                </p>
              )}
            <button
              type="button"
              onClick={propertyResource.retry}
              className="mt-5 rounded-lg bg-[#087775] px-5 py-3 text-[13px] font-bold text-white"
            >
              Thử lại
            </button>
          </section>
        )}

        {property && (
          <>
            <header className="mt-8 border-b border-slate-100 pb-7">
              <p className="mb-2 text-[12px] font-bold uppercase tracking-[.16em] text-[#087775]">
                Khu trọ công khai
              </p>
              <h1 className="font-display text-[32px] font-bold tracking-[-.04em] text-slate-900">
                {property.propertyName}
              </h1>
              {address && (
                <p className="mt-2 text-[14px] text-slate-500">{address}</p>
              )}
            </header>

            <form
              onSubmit={submitFilters}
              className="mt-8 rounded-2xl bg-white p-4 shadow-sm"
            >
              <h2 className="mb-3 text-[15px] font-semibold text-slate-800">
                Lọc phòng
              </h2>
              <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
                <label className="text-[12px] font-medium text-slate-500">
                  Giá tối thiểu
                  <input
                    aria-label="Giá tối thiểu"
                    inputMode="decimal"
                    value={draft.minPrice}
                    onChange={(event) =>
                      updateDraft("minPrice", event.target.value)
                    }
                    className="mt-1 h-10 w-full rounded-lg border border-slate-200 px-3 text-[13px] text-slate-700"
                  />
                </label>
                <label className="text-[12px] font-medium text-slate-500">
                  Giá tối đa
                  <input
                    aria-label="Giá tối đa"
                    inputMode="decimal"
                    value={draft.maxPrice}
                    onChange={(event) =>
                      updateDraft("maxPrice", event.target.value)
                    }
                    className="mt-1 h-10 w-full rounded-lg border border-slate-200 px-3 text-[13px] text-slate-700"
                  />
                </label>
                <label className="text-[12px] font-medium text-slate-500">
                  Loại phòng
                  <input
                    aria-label="Loại phòng"
                    value={draft.roomTypeName}
                    onChange={(event) =>
                      updateDraft("roomTypeName", event.target.value)
                    }
                    className="mt-1 h-10 w-full rounded-lg border border-slate-200 px-3 text-[13px] text-slate-700"
                  />
                </label>
                <label className="text-[12px] font-medium text-slate-500">
                  Mã tiện ích
                  <input
                    aria-label="Mã tiện ích"
                    value={draft.amenityIds}
                    onChange={(event) =>
                      updateDraft("amenityIds", event.target.value)
                    }
                    placeholder="Ví dụ: 2, 5"
                    className="mt-1 h-10 w-full rounded-lg border border-slate-200 px-3 text-[13px] text-slate-700"
                  />
                </label>
                <label className="text-[12px] font-medium text-slate-500">
                  Sắp xếp
                  <select
                    aria-label="Sắp xếp"
                    value={draft.sortBy}
                    onChange={(event) =>
                      updateDraft("sortBy", event.target.value)
                    }
                    className="mt-1 h-10 w-full rounded-lg border border-slate-200 px-3 text-[13px] text-slate-700"
                  >
                    <option value="roomId">Mặc định</option>
                    <option value="price_asc">Giá thấp đến cao</option>
                    <option value="price_desc">Giá cao đến thấp</option>
                  </select>
                </label>
              </div>
              <button
                type="submit"
                className="mt-4 rounded-lg bg-[#087775] px-5 py-2.5 text-[13px] font-bold text-white"
              >
                Áp dụng bộ lọc
              </button>
              {filterError && (
                <p role="alert" className="mt-3 text-[13px] text-rose-700">
                  {filterError}
                </p>
              )}
            </form>

            <section className="mt-10 border-t border-slate-100 pt-8">
              {roomsResource.status === "loading" && (
                <p
                  role="status"
                  className="rounded-xl bg-white px-5 py-8 text-center text-[14px] text-slate-500"
                >
                  Đang tải danh sách phòng...
                </p>
              )}
              {roomsResource.status === "error" && (
                <div
                  role="alert"
                  className="rounded-xl bg-white px-5 py-8 text-center"
                >
                  <p className="text-[14px] text-rose-700">
                    {roomsResource.error.kind === "not-found"
                      ? "Khu trọ hiện không khả dụng"
                      : roomsResource.error.kind === "validation"
                        ? publicApiValidationMessages(roomsResource.error).join(
                            " ",
                          )
                        : publicApiErrorMessage(roomsResource.error)}
                  </p>
                  {roomsResource.error.kind !== "validation" && (
                    <button
                      type="button"
                      onClick={roomsResource.retry}
                      className="mt-3 rounded-lg bg-[#087775] px-4 py-2 text-[13px] font-semibold text-white"
                    >
                      Thử lại
                    </button>
                  )}
                </div>
              )}
              {roomPage && roomPage.items.length === 0 && (
                <p
                  role="status"
                  className="rounded-xl bg-white px-5 py-8 text-center text-[14px] text-slate-500"
                >
                  Không có phòng phù hợp với bộ lọc hiện tại
                </p>
              )}
              {roomPage && roomPage.items.length > 0 && (
                <AvailableRooms
                  rooms={roomPage.items}
                  totalCount={roomPage.totalCount}
                  onOpenRoom={setSelectedRoomId}
                />
              )}
              {roomPage && pageCount > 1 && (
                <nav
                  aria-label="Phân trang phòng"
                  className="mt-6 flex items-center justify-center gap-4"
                >
                  <button
                    type="button"
                    onClick={() => changePage(roomPage.page - 1)}
                    disabled={roomPage.page <= 1}
                    className="rounded-lg border border-slate-200 px-4 py-2 text-[13px] disabled:opacity-40"
                  >
                    Trang trước
                  </button>
                  <span
                    aria-live="polite"
                    className="text-[13px] text-slate-500"
                  >
                    Trang {roomPage.page} / {pageCount}
                  </span>
                  <button
                    type="button"
                    onClick={() => changePage(roomPage.page + 1)}
                    disabled={roomPage.page >= pageCount}
                    className="rounded-lg border border-slate-200 px-4 py-2 text-[13px] disabled:opacity-40"
                  >
                    Trang tiếp
                  </button>
                </nav>
              )}
            </section>
          </>
        )}
      </div>
      {selectedRoomId !== null && (
        <RoomDetailModal
          roomId={selectedRoomId}
          propertyServiceRates={property?.livingCosts ?? []}
          onClose={() => setSelectedRoomId(null)}
          onRefresh={roomsResource.retry}
        />
      )}
      <Footer />
    </main>
  )
}
