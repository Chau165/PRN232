import { lazy, Suspense, useEffect, useRef, useState } from "react"
import { previewPropertyLocation, type LocationAddress, type LocationPreview } from "@/services/propertyLocationService"
import { isSupportedPropertyLocation } from "@/utils/propertyLocation"
import type { LocationPoint } from "./PropertyLocationMap"

const LocationMap = lazy(() => import("./PropertyLocationMap"))
type Props = {
  address: LocationAddress
  initialPoint?: LocationPoint
  disabled: boolean
  onChange: (point: LocationPoint | null) => void
}

export default function PropertyLocationPicker({ address, initialPoint, disabled, onChange }: Props) {
  const key = JSON.stringify(address)
  const currentKey = useRef(key)
  currentKey.current = key
  const request = useRef<AbortController | null>(null)
  const [preview, setPreview] = useState<LocationPreview | null>(null)
  const [point, setPoint] = useState<LocationPoint | null>(initialPoint ?? null)
  const [source, setSource] = useState<"stored" | "suggestion" | "manual">("stored")
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState("")

  useEffect(() => () => request.current?.abort(), [])
  useEffect(() => {
    request.current?.abort()
    setBusy(false)
    setPreview(null)
    setPoint(initialPoint ?? null)
    setSource("stored")
    setError("")
  }, [key])

  const ready = Boolean(address.addressText.trim() && address.provinceCode && address.wardCode)
  function pick(next: LocationPoint, from: "manual" | "suggestion" = "manual") {
    if (disabled || !ready) return
    if (!isSupportedPropertyLocation(next)) {
      setError("Vị trí ngoài phạm vi hỗ trợ. Hãy chọn lại ghim trong khu vực Việt Nam.")
      return
    }
    request.current?.abort()
    setBusy(false)
    setPoint(next)
    setSource(from)
    setError("")
    onChange(next)
  }

  async function search() {
    if (disabled || !ready) return
    request.current?.abort()
    const controller = new AbortController()
    request.current = controller
    const started = key
    setBusy(true)
    setError("")
    setPreview(null)
    // Looking up the same address does not discard a pin already chosen by the Admin.
    try {
      const result = await previewPropertyLocation(address, controller.signal)
      if (!controller.signal.aborted && currentKey.current === started) setPreview(result)
    } catch (cause) {
      if (!controller.signal.aborted && currentKey.current === started)
        setError(cause instanceof Error ? cause.message : "Không tìm được vị trí.")
    } finally {
      if (request.current === controller) setBusy(false)
    }
  }

  return <section className="space-y-3 sm:col-span-2" aria-label="Vị trí khu trọ">
    <button type="button" disabled={disabled || busy || !ready || !address.provinceName || !address.wardName} onClick={search}
      className="rounded-lg bg-teal-700 px-4 py-2 text-sm font-bold text-white disabled:opacity-50">
      {busy ? "Đang tìm vị trí…" : "Tìm thử"}
    </button>
    <p className="text-xs text-slate-600">Chọn gợi ý hoặc bấm/kéo ghim đến đúng nhà trọ rồi lưu. Kéo ghim không đổi địa chỉ đã nhập. Hệ thống chưa tự xác minh ranh giới phường.</p>
    {!ready && <p className="text-xs text-slate-500">Nhập địa chỉ và chọn tỉnh/phường để chọn vị trí.</p>}
    {preview?.candidates.length === 0 && <p role="status" className="text-sm text-amber-800">
      {(preview.status === "Unavailable" || preview.status === "RateLimited") ? "Dịch vụ tìm vị trí đang không khả dụng. Bạn vẫn có thể chọn ghim thủ công." : "Chưa có kết quả phù hợp. Hãy kiểm tra địa chỉ hoặc chọn ghim thủ công."}
    </p>}
    {preview && preview.candidates.length > 0 && <ul className="max-h-52 space-y-2 overflow-auto">
      {preview.candidates.map((candidate, index) => <li key={index}>
        <button type="button" disabled={disabled || busy}
          aria-pressed={source === "suggestion" && point?.latitude === candidate.location.coordinates.latitude && point?.longitude === candidate.location.coordinates.longitude}
          onClick={() => pick(candidate.location.coordinates, "suggestion")}
          className="w-full rounded-lg border border-slate-200 p-3 text-left text-sm hover:border-teal-700 aria-pressed:border-teal-700 aria-pressed:bg-teal-50">
          <span className="block font-semibold">{candidate.location.displayAddress}</span>
          <span className="text-xs text-slate-500">{candidate.location.precision === "street" ? "Vị trí gần đúng cấp đường phố" : "Vị trí gợi ý"}{candidate.reasons.includes("ward_unverified") ? " · Chưa xác minh phường/xã" : ""}</span>
        </button>
      </li>)}
    </ul>}
    <p className="text-xs text-slate-500">Gợi ý địa chỉ: <a href="https://photon.komoot.io" target="_blank" rel="noreferrer">Photon</a> · <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noreferrer">© OpenStreetMap</a>. Gợi ý có thể khác số nhà hoặc phường đã nhập; hãy kiểm tra ghim.</p>
    <Suspense fallback={<p>Đang tải bản đồ…</p>}>
      <LocationMap point={point} initialPoint={initialPoint} disabled={disabled || !ready} onPick={next => pick(next)} />
    </Suspense>
    {point && <p role="status" className="text-xs text-slate-600">
      {source === "stored" ? "Vị trí đã lưu" : source === "suggestion" ? "Đã chọn gợi ý" : "Đã chỉnh ghim"}: {point.latitude.toFixed(6)}, {point.longitude.toFixed(6)}
    </p>}
    {error && <p role="alert" className="rounded-lg bg-rose-50 p-3 text-sm text-rose-700">{error}</p>}
  </section>
}
