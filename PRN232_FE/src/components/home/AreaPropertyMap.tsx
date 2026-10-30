import { useEffect, useRef, useState } from "react"

import L, { type Marker as LeafletMarker } from "leaflet"
import {
  CircleMarker,
  MapContainer,
  Marker,
  Popup,
  TileLayer,
  Tooltip,
  useMap,
} from "react-leaflet"
import { Link } from "react-router"
import "leaflet/dist/leaflet.css"

import type { PublicPropertyAreaCard } from "@/types/publicBrowsing"
import { propertyDetailPath } from "@/constants/routes"
import { formatVnd } from "@/utils/publicFormatting"
import {
  buildGoogleMapsDirectionsUrl,
  type AreaCoordinates,
} from "@/utils/areaDiscovery"

interface AreaPropertyMapProps {
  properties: PublicPropertyAreaCard[]
  center: AreaCoordinates
  userLocation: AreaCoordinates | null
  selectedPropertyId: number | null
  onSelect: (propertyId: number) => void
}

function hasMapCoordinates(property: PublicPropertyAreaCard): boolean {
  return (
    Number.isFinite(property.latitude) &&
    Number.isFinite(property.longitude) &&
    property.latitude >= -90 &&
    property.latitude <= 90 &&
    property.longitude >= -180 &&
    property.longitude <= 180 &&
    (property.latitude !== 0 || property.longitude !== 0)
  )
}

function escapeHtml(value: string): string {
  return value.replace(/[&<>"']/g, (character) => {
    const escaped: Record<string, string> = {
      "&": "&amp;",
      "<": "&lt;",
      ">": "&gt;",
      '"': "&quot;",
      "'": "&#39;",
    }
    return escaped[character] ?? character
  })
}

function safeImageUrl(value: string | null): string | null {
  const imageUrl = value?.trim()
  if (!imageUrl) return null
  if (imageUrl.startsWith("/")) return imageUrl

  try {
    const parsed = new URL(imageUrl)
    return parsed.protocol === "https:" || parsed.protocol === "http:"
      ? parsed.toString()
      : null
  } catch {
    return null
  }
}

function markerIcon(
  property: PublicPropertyAreaCard,
  selected: boolean,
): L.DivIcon {
  const imageUrl = safeImageUrl(property.primaryImageUrl)
  const image = imageUrl
    ? `<img class="area-property-marker__image" src="${escapeHtml(imageUrl)}" alt="" loading="lazy" />`
    : `<span class="area-property-marker__placeholder" aria-hidden="true"><svg viewBox="0 0 24 24"><path d="M3 10.5 12 3l9 7.5M5.5 9.5v11h13v-11M9 20.5v-7h6v7" /></svg></span>`

  return L.divIcon({
    className: `area-property-marker${selected ? " area-property-marker--selected" : ""}`,
    html: `<span class="area-property-marker__card"><span class="area-property-marker__photo">${image}</span><span class="area-property-marker__price">${escapeHtml(formatVnd(property.startingPrice))}</span></span>`,
    iconSize: [142, 78],
    iconAnchor: [71, 78],
    popupAnchor: [0, -74],
  })
}

function MapViewport({
  properties,
  center,
  userLocation,
  selectedPropertyId,
  markerRefs,
}: AreaPropertyMapProps & {
  markerRefs: React.MutableRefObject<Map<number, LeafletMarker>>
}) {
  const map = useMap()

  useEffect(() => {
    if (selectedPropertyId !== null) return

    const propertyPoints = properties
      .filter(hasMapCoordinates)
      .map((property): L.LatLngTuple => [property.latitude, property.longitude])
    if (propertyPoints.length === 0) {
      map.setView(
        [center.latitude, center.longitude],
        userLocation ? 13 : 11,
        { animate: false },
      )
      return
    }

    const points: L.LatLngTuple[] = [
      [center.latitude, center.longitude],
      ...propertyPoints,
    ]
    map.fitBounds(points, { padding: [44, 44], maxZoom: 14 })
  }, [center, map, properties, selectedPropertyId, userLocation])

  useEffect(() => {
    if (selectedPropertyId === null) return
    const property = properties.find((item) => item.propertyId === selectedPropertyId)
    if (!property || !hasMapCoordinates(property)) return

    map.flyTo(
      [property.latitude, property.longitude],
      Math.max(map.getZoom(), 14),
      { duration: 0.45 },
    )
    markerRefs.current.get(property.propertyId)?.openPopup()
  }, [map, markerRefs, properties, selectedPropertyId])

  return null
}

export default function AreaPropertyMap({
  properties,
  center,
  userLocation,
  selectedPropertyId,
  onSelect,
}: AreaPropertyMapProps) {
  const markerRefs = useRef(new Map<number, LeafletMarker>())
  const [tileError, setTileError] = useState(false)
  const tileUrl =
    import.meta.env.VITE_PUBLIC_MAP_TILE_URL ||
    "https://tile.openstreetmap.de/{z}/{x}/{y}.png"
  const attribution =
    import.meta.env.VITE_PUBLIC_MAP_ATTRIBUTION ||
    '&copy; <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noopener noreferrer">OpenStreetMap contributors</a>'

  return (
    <div className="overflow-hidden rounded-2xl border border-slate-200 bg-white">
      <MapContainer
        center={[center.latitude, center.longitude]}
        zoom={13}
        minZoom={5}
        maxZoom={18}
        scrollWheelZoom={false}
        className="h-[390px] w-full sm:h-[470px]"
      >
        <MapViewport
          properties={properties}
          center={center}
          userLocation={userLocation}
          selectedPropertyId={selectedPropertyId}
          onSelect={onSelect}
          markerRefs={markerRefs}
        />
        <TileLayer
          url={tileUrl}
          attribution={attribution}
          eventHandlers={{ tileerror: () => setTileError(true) }}
        />
        {userLocation && (
          <CircleMarker
            center={[userLocation.latitude, userLocation.longitude]}
            radius={10}
            pathOptions={{ color: "#ffffff", fillColor: "#087775", fillOpacity: 1, weight: 3 }}
          >
            <Tooltip permanent direction="top" offset={[0, -8]}>
              Vị trí hiện tại của bạn
            </Tooltip>
          </CircleMarker>
        )}
        {properties.filter(hasMapCoordinates).map((property) => {
          const selected = property.propertyId === selectedPropertyId
          const directionsUrl = buildGoogleMapsDirectionsUrl(property)
          return (
            <Marker
              key={property.propertyId}
              ref={(marker) => {
                if (marker) markerRefs.current.set(property.propertyId, marker)
                else markerRefs.current.delete(property.propertyId)
              }}
              position={[property.latitude, property.longitude]}
              icon={markerIcon(property, selected)}
              title={`${property.propertyName} · ${formatVnd(property.startingPrice)}`}
              alt={`${property.propertyName}, ${formatVnd(property.startingPrice)}`}
              zIndexOffset={selected ? 1000 : 0}
              eventHandlers={{ click: () => onSelect(property.propertyId) }}
            >
              <Popup className="area-property-popup" maxWidth={290} minWidth={250}>
                <div className="space-y-2 p-1">
                  <h3 className="font-bold text-slate-900">{property.propertyName}</h3>
                  <p className="text-xs leading-5 text-slate-600">
                    {[property.addressText, property.ward, property.district, property.city]
                      .filter(Boolean)
                      .join(", ")}
                  </p>
                  <p className="text-xs text-slate-600">
                    Còn {property.availableRoomCount} phòng đang cho thuê
                  </p>
                  <p className="font-bold text-[#087775]">
                    {formatVnd(property.startingPrice)}
                    <span className="ml-1 text-xs font-medium text-slate-500">/tháng từ</span>
                  </p>
                  <div className="flex gap-2 pt-1">
                    <Link
                      to={propertyDetailPath(property.propertyId)}
                      className="rounded-lg bg-[#087775] px-3 py-2 text-xs font-bold text-white hover:bg-[#066a68]"
                    >
                      Xem chi tiết
                    </Link>
                    {directionsUrl && (
                      <a
                        href={directionsUrl}
                        target="_blank"
                        rel="noopener noreferrer"
                        className="rounded-lg border border-slate-200 px-3 py-2 text-xs font-bold text-slate-700 hover:bg-slate-50"
                      >
                        Chỉ đường
                      </a>
                    )}
                  </div>
                </div>
              </Popup>
            </Marker>
          )
        })}
      </MapContainer>
      {tileError && (
        <p role="status" className="border-t border-slate-100 px-4 py-2 text-xs text-amber-700">
          Nền bản đồ chưa tải được. Danh sách khu trọ vẫn sử dụng được.
        </p>
      )}
      <p className="flex items-center justify-between gap-3 border-t border-slate-100 px-4 py-2 text-[11px] text-slate-500">
        <span>Bản đồ © OpenStreetMap contributors</span>
        <a
          href="https://www.openstreetmap.org/fixthemap"
          target="_blank"
          rel="noopener noreferrer"
          className="font-semibold text-[#087775] hover:underline"
        >
          Báo lỗi bản đồ
        </a>
      </p>
    </div>
  )
}
