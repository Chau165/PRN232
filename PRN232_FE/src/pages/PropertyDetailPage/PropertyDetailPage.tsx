import { Link, useParams } from "react-router"
import { PublicApiError, publicApi } from "@/services/publicBrowsingService"
import Footer from "@/components/common/Footer"
import Icon, { type IconName } from "@/components/common/Icon"
import Navbar from "@/components/common/Navbar"
import DetailSection from "@/components/property/DetailSection"
import {
  availableRoomsPath,
  parsePositiveRouteId,
  routes,
} from "@/constants/routes"
import { usePublicApiResource } from "@/hooks/usePublicApiResource"
import { publicApiErrorMessage } from "@/utils/publicApiDisplay"
import { formatVnd, serviceRateUnitLabel } from "@/utils/publicFormatting"

function amenityIcon(name: string): IconName {
  const normalizedName = name.trim().toLowerCase()

  if (normalizedName.includes("wi-fi") || normalizedName.includes("wifi")) {
    return "wifi"
  }
  if (normalizedName.includes("air conditioner") || normalizedName.includes("máy lạnh")) {
    return "snow"
  }
  if (normalizedName.includes("parking") || normalizedName.includes("bãi xe")) {
    return "car"
  }
  if (normalizedName.includes("camera")) {
    return "camera"
  }
  if (normalizedName.includes("toilet") || normalizedName.includes("wc")) {
    return "toilet"
  }
  if (normalizedName.includes("washing machine") || normalizedName.includes("laundry") || normalizedName.includes("máy giặt")) {
    return "washer"
  }
  if (normalizedName.includes("elevator") || normalizedName.includes("thang máy")) {
    return "lift"
  }
  if (normalizedName.includes("lock") || normalizedName.includes("khóa")) {
    return "lock"
  }

  return "home"
}

function serviceCostPresentation(serviceName: string): {
  label: string
  icon: IconName
} {
  const normalizedName = serviceName.trim().toLowerCase()

  if (
    normalizedName.includes("electric") ||
    normalizedName.includes("power") ||
    normalizedName.includes("điện")
  ) {
    return { label: "Điện", icon: "bolt" }
  }
  if (normalizedName.includes("water") || normalizedName.includes("nước")) {
    return { label: "Nước", icon: "droplet" }
  }
  if (
    normalizedName.includes("internet") ||
    normalizedName.includes("wi-fi") ||
    normalizedName.includes("wifi")
  ) {
    return { label: "Internet", icon: "wifi" }
  }
  if (
    normalizedName.includes("trash") ||
    normalizedName.includes("garbage") ||
    normalizedName.includes("waste") ||
    normalizedName.includes("rác")
  ) {
    return { label: "Rác", icon: "trash" }
  }

  return { label: serviceName, icon: "receipt" }
}

export default function PropertyDetailPage() {
  const { propertyId: routePropertyId } = useParams()
  const propertyId = parsePositiveRouteId(routePropertyId)
  const resource = usePublicApiResource(
    String(propertyId ?? "invalid"),
    (signal) =>
      propertyId === null
        ? Promise.reject(
            new PublicApiError("validation", "Mã khu trọ không hợp lệ."),
          )
        : publicApi.getProperty(propertyId, signal),
  )

  const property = resource.status === "success" ? resource.data : null
  const address = property
    ? [property.addressText, property.ward, property.district, property.city]
        .filter((part) => part?.trim())
        .join(", ")
    : ""
  const roomAmenities = property?.roomAmenities ?? []
  const commonAmenities = property?.commonAmenities ?? []
  const amenitiesById = new Map<
    number,
    {
      amenityId: number
      name: string
      isCommon: boolean
      rentableRoomCount?: number
    }
  >()
  commonAmenities.forEach((amenity) => {
    amenitiesById.set(amenity.amenityId, { ...amenity, isCommon: true })
  })
  roomAmenities.forEach((amenity) => {
    const existingAmenity = amenitiesById.get(amenity.amenityId)
    if (existingAmenity) {
      existingAmenity.rentableRoomCount = amenity.rentableRoomCount
      return
    }

    amenitiesById.set(amenity.amenityId, { ...amenity, isCommon: false })
  })
  const amenities = Array.from(amenitiesById.values())
  const livingCosts = property?.livingCosts ?? []
  const description = property?.description?.trim() ?? ""

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
            <span className="text-slate-600">{property.propertyName}</span>
          ) : (
            <span>Khu trọ</span>
          )}
        </nav>

        {resource.status === "loading" && (
          <p
            role="status"
            className="mt-10 rounded-2xl bg-white px-6 py-12 text-center text-slate-500"
          >
            Đang tải thông tin khu trọ...
          </p>
        )}
        {resource.status === "error" && (
          <section
            role="alert"
            className="mt-10 rounded-2xl bg-white px-6 py-12 text-center"
          >
            <h1 className="font-display text-2xl font-bold text-slate-900">
              {resource.error.kind === "not-found" ||
              resource.error.kind === "validation"
                ? "Khu trọ hiện không khả dụng"
                : "Không thể tải thông tin khu trọ"}
            </h1>
            {(resource.error.kind === "network" ||
              resource.error.kind === "server" ||
              resource.error.kind === "unexpected") && (
              <p className="mt-2 text-[14px] text-slate-500">
                {publicApiErrorMessage(resource.error)}
              </p>
            )}
            <button
              type="button"
              onClick={resource.retry}
              className="mt-5 rounded-lg bg-[#087775] px-5 py-3 text-[13px] font-bold text-white"
            >
              Thử lại
            </button>
          </section>
        )}

        {property && (
          <>
            <div className="mt-8 grid grid-cols-1 items-start gap-8 lg:grid-cols-[minmax(0,1fr)_312px] lg:gap-20">
              <div>
                <p className="mb-2 text-[12px] font-bold uppercase tracking-[.16em] text-[#087775]">
                  Khu trọ công khai
                </p>
                <h1 className="font-display text-[37px] font-bold tracking-[-.05em] text-slate-900">
                  {property.propertyName}
                </h1>
                {address && (
                  <p className="mt-2 flex items-center gap-1.5 text-[15px] text-slate-500">
                    <Icon name="pin" size={16} />
                    {address}
                  </p>
                )}
                <p className="mt-4 inline-flex rounded-full bg-emerald-50 px-3 py-1.5 text-[13px] font-medium text-emerald-700">
                  {property.availableRoomCount} phòng hiện có thể thuê
                </p>
              </div>
              <div className="pt-1">
                <p className="text-[13px] font-medium text-slate-500">Giá từ</p>
                <p className="mt-1 text-[25px] font-bold tracking-[-.04em] text-[#087775]">
                  {property.startingPrice === null
                    ? "Chưa có giá"
                    : formatVnd(property.startingPrice)}
                </p>
                <div className="mt-5 grid grid-cols-2 gap-2">
                  <Link
                    to={availableRoomsPath(property.propertyId)}
                    className="rounded-lg bg-[#087775] px-2 py-3 text-center text-[13px] font-bold text-white shadow-sm hover:bg-[#066a68]"
                  >
                    Xem phòng trống
                  </Link>
                  <button
                    type="button"
                    disabled
                    title="Đặt lịch xem trực tuyến chưa được hỗ trợ"
                    className="cursor-not-allowed rounded-lg bg-slate-100 px-2 py-3 text-[13px] font-bold text-slate-400"
                  >
                    Đặt lịch chưa hỗ trợ
                  </button>
                </div>
              </div>
            </div>

            <div className="mt-10 grid min-h-[280px] place-items-center overflow-hidden rounded-3xl bg-slate-100">
              {property.primaryImageUrl ? (
                <img
                  src={property.primaryImageUrl}
                  alt={`Ảnh ${property.propertyName}`}
                  className="h-full max-h-[480px] w-full object-cover"
                />
              ) : (
                <div
                  role="img"
                  aria-label="Ảnh khu trọ chưa được cung cấp"
                  className="px-6 text-center text-[14px] text-slate-500"
                >
                  Ảnh khu trọ chưa được cung cấp
                </div>
              )}
            </div>

            <div className="mt-12">
              <DetailSection title="Giới thiệu">
                {description ? (
                  <p className="mt-4 max-w-[680px] whitespace-pre-line text-[15px] leading-7 text-slate-600">
                    {property.description}
                  </p>
                ) : (
                  <p role="status" className="mt-4 max-w-[680px] text-[15px] leading-7 text-slate-500">
                    Thông tin giới thiệu đang được cập nhật.
                  </p>
                )}
              </DetailSection>

              <DetailSection title="Tiện ích">
                {amenities.length === 0 ? (
                  <p role="status" className="mt-4 text-[14px] text-slate-500">
                    Chưa có thông tin tiện ích.
                  </p>
                ) : (
                  <ul className="mt-5 grid grid-cols-1 gap-4 sm:grid-cols-2">
                    {amenities.map((amenity) => {
                      const roomScope =
                        amenity.rentableRoomCount === property.availableRoomCount &&
                        property.availableRoomCount > 0
                          ? "tất cả phòng hiện có thể thuê"
                          : `${amenity.rentableRoomCount} trên ${property.availableRoomCount} phòng hiện có thể thuê`
                      const scopeLabel = amenity.rentableRoomCount === undefined
                        ? "Tiện ích chung của khu trọ"
                        : amenity.isCommon
                          ? `Có tại khu trọ và ${roomScope}`
                          : amenity.rentableRoomCount === property.availableRoomCount &&
                              property.availableRoomCount > 0
                            ? "Có tại tất cả phòng hiện có thể thuê"
                            : `Có tại ${roomScope}`

                      return (
                        <li key={amenity.amenityId} className="flex items-start gap-3">
                          <span
                            aria-hidden="true"
                            className="grid h-9 w-9 shrink-0 place-items-center rounded-lg bg-[#f1f8f7] text-[#087775]"
                          >
                            <Icon name={amenityIcon(amenity.name)} size={17} />
                          </span>
                          <span>
                            <span className="block text-[14px] font-medium text-slate-700">
                              {amenity.name}
                            </span>
                            <span className="mt-1 block text-[12px] text-slate-500">
                              {scopeLabel}
                            </span>
                          </span>
                        </li>
                      )
                    })}
                  </ul>
                )}
              </DetailSection>

              <DetailSection title="Chi phí sinh hoạt">
                {livingCosts.length === 0 ? (
                  <p role="status" className="mt-4 text-[14px] text-slate-500">
                    Chi phí sinh hoạt đang được cập nhật.
                  </p>
                ) : (
                  <ul className="mt-4 divide-y divide-slate-100 rounded-xl border border-slate-100 bg-white px-4">
                    {livingCosts.map((rate) => {
                      const unitLabel = serviceRateUnitLabel(
                        rate.unitCode,
                        rate.billingBasis,
                      )
                      const presentation = serviceCostPresentation(rate.serviceName)

                      return (
                        <li
                          key={`${rate.serviceId}-${rate.effectiveFrom}`}
                          className="flex flex-wrap items-center justify-between gap-3 py-3 text-[14px]"
                        >
                          <span className="flex min-w-0 items-center gap-3 text-slate-600">
                            <span
                              aria-hidden="true"
                              className="grid h-9 w-9 shrink-0 place-items-center rounded-lg bg-[#f1f8f7] text-[#087775]"
                            >
                              <Icon name={presentation.icon} size={17} />
                            </span>
                            <span>{presentation.label}</span>
                          </span>
                          {unitLabel ? (
                            <strong className="font-semibold text-[#087775]">
                              {formatVnd(rate.price)}/{unitLabel}
                            </strong>
                          ) : (
                            <span role="status" className="text-[13px] text-slate-500">
                              Đơn vị tính đang được cập nhật.
                            </span>
                          )}
                        </li>
                      )
                    })}
                  </ul>
                )}
              </DetailSection>
            </div>
          </>
        )}
      </div>
      <Footer />
    </main>
  )
}
