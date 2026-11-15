import { Link, useParams } from "react-router"
import { PublicApiError, publicApi } from "@/services/publicBrowsingService"
import Footer from "@/components/common/Footer"
import Navbar from "@/components/common/Navbar"
import PropertyDetailView from "@/components/property/PropertyDetailView"
import {
  availableRoomsPath,
  parsePositiveRouteId,
  routes,
} from "@/constants/routes"
import { usePublicApiResource } from "@/hooks/usePublicApiResource"
import { publicApiErrorMessage } from "@/utils/publicApiDisplay"
import { formatAddress } from "@/utils/publicFormatting"

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
    ? formatAddress(property.addressText, property.ward, property.city)
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
          <PropertyDetailView
            viewerRole="guest"
            layout="page"
            servicesHeading="Chi phí sinh hoạt"
            property={{
              propertyId: property.propertyId,
              propertyName: property.propertyName,
              address,
              primaryImageUrl: property.primaryImageUrl,
              description: property.description,
              startingPrice: property.startingPrice,
              availableRoomCount: property.availableRoomCount,
              amenities,
              services: livingCosts.map((rate) => ({
                key: `${rate.serviceId}-${rate.effectiveFrom}`,
                serviceName: rate.serviceName,
                price: rate.price,
                unitCode: rate.unitCode,
                billingBasis: rate.billingBasis,
              })),
            }}
            actions={
              <Link
                to={availableRoomsPath(property.propertyId)}
                className="block rounded-lg bg-[#087775] px-4 py-3 text-center text-[13px] font-bold text-white shadow-sm hover:bg-[#066a68]"
              >
                Xem phòng trống
              </Link>
            }
          />
        )}
      </div>
      <Footer />
    </main>
  )
}
