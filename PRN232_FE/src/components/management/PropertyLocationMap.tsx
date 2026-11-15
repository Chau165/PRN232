import { useEffect } from "react"
import L from "leaflet"
import {
  MapContainer,
  Marker,
  TileLayer,
  useMap,
  useMapEvents,
} from "react-leaflet"
import "leaflet/dist/leaflet.css"
export type LocationPoint = { latitude: number; longitude: number }
const icon = L.divIcon({
  className: "",
  html: '<span style="display:block;width:22px;height:22px;background:#087775;border:3px solid white;border-radius:50%;box-shadow:0 1px 6px #333"></span>',
  iconSize: [22, 22],
  iconAnchor: [11, 11],
})
function MapEvents({
  point,
  onPick,
  disabled,
}: {
  point: LocationPoint | null
  onPick: (point: LocationPoint) => void
  disabled: boolean
}) {
  const map = useMap()
  useEffect(() => {
    if (point) map.panTo([point.latitude, point.longitude])
  }, [map, point])
  useMapEvents({
    click: (e) => { if (!disabled) onPick({ latitude: e.latlng.lat, longitude: e.latlng.lng }) },
  })
  return point ? (
    <Marker
      position={[point.latitude, point.longitude]}
      icon={icon}
      draggable={!disabled}
      eventHandlers={{
        dragend: (e) => {
          const p = (e.target as L.Marker).getLatLng()
          if (!disabled) onPick({ latitude: p.lat, longitude: p.lng })
        },
      }}
    />
  ) : null
}
export default function PropertyLocationMap({
  point,
  initialPoint,
  onPick,
  disabled = false,
}: {
  point: LocationPoint | null
  initialPoint?: LocationPoint
  onPick: (point: LocationPoint) => void
  disabled?: boolean
}) {
  const center = point ??
    initialPoint ?? { latitude: 10.8231, longitude: 106.6297 }
  return (
    <div
      className="relative z-0 h-72 overflow-hidden rounded-lg border border-slate-200"
      aria-label="Bản đồ chọn vị trí khu trọ"
    >
      <MapContainer
        center={[center.latitude, center.longitude]}
        zoom={15}
        style={{ height: "100%", width: "100%" }}
      >
        <TileLayer
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        />
        <MapEvents point={point} onPick={onPick} disabled={disabled} />
      </MapContainer>
    </div>
  )
}
