import { isSupportedPropertyLocation } from "@/utils/propertyLocation"

export type LocationAddress = {
  addressText: string
  provinceCode: string
  wardCode: string
  provinceName?: string
  wardName?: string
}
export type LocationCandidate = {
  location: {
    displayAddress: string
    precision: string
    provider: string
    coordinates: { latitude: number; longitude: number }
  }
  matchLevel: string
  reasons: string[]
}
export type LocationPreview = {
  status: string
  candidates: LocationCandidate[]
  providers: { provider: string; status: string }[]
}

function normalize(value: string) {
  return value.toLowerCase().normalize("NFD").replace(/[\u0300-\u036f]/g, "").replace(/đ/g, "d").replace(/[^a-z0-9]+/g, " ").trim()
}
export function propertyAddressQueries(address: LocationAddress) {
  const street = address.addressText.trim().replace(/^(\d+[\w/.-]*)\s*,\s*/u, "$1 ")
  // Keep names such as Đường số 8 intact.
  const shortened = street.replace(/(^|\s)đường\s+(?!số(?:\s|$)|\d)/iu, "$1")
  const road = shortened.replace(/^\d+[\w/.-]*\s+/, "")
  return [...new Set([
    [street, address.wardName, address.provinceName, "Việt Nam"].filter(Boolean).join(", "),
    [shortened, address.provinceName, "Việt Nam"].filter(Boolean).join(", "),
    [road, address.provinceName, "Việt Nam"].filter(Boolean).join(", "),
  ])]
}

type Options = { endpoint?: string; fetcher?: typeof fetch; timeoutMs?: number }
export function createPropertyLocationSearch(options: Options = {}) {
  const cache = new Map<string, { at: number; result: LocationPreview }>()
  return async (address: LocationAddress, signal?: AbortSignal): Promise<LocationPreview> => {
    signal?.throwIfAborted()
    const result = (status: string, candidates: LocationCandidate[] = []): LocationPreview => ({
      status, candidates, providers: [{ provider: "Photon", status }],
    })
    if (!address.addressText.trim() || !address.provinceName?.trim() || !address.wardName?.trim())
      return result("NoResults")
    let endpoint: URL
    try {
      endpoint = new URL(options.endpoint ?? import.meta.env.VITE_PROPERTY_GEOCODING_URL ?? "https://photon.komoot.io/api/")
    } catch {
      return result("Unavailable")
    }
    if (endpoint.protocol !== "https:" || endpoint.username || endpoint.password) return result("Unavailable")
    const queries = propertyAddressQueries(address)
    const key = JSON.stringify([endpoint.href, queries])
    const cached = cache.get(key)
    if (cached && Date.now() - cached.at < 600_000) return structuredClone(cached.result)
    const controller = new AbortController()
    const abort = () => controller.abort(signal?.reason)
    signal?.addEventListener("abort", abort, { once: true })
    const timer = setTimeout(() => controller.abort(), options.timeoutMs ?? 15_000)
    try {
      for (const query of queries) {
        controller.signal.throwIfAborted()
        const url = new URL(endpoint)
        url.searchParams.set("q", query)
        url.searchParams.set("limit", "8")
        url.searchParams.set("countrycode", "VN")
        url.searchParams.set("bbox", "102,6.5,117.5,24")
        const response = await (options.fetcher ?? fetch)(url.href, {
          signal: controller.signal, credentials: "omit", headers: { Accept: "application/json" },
        })
        if (!response.ok) return result(response.status === 429 ? "RateLimited" : "Unavailable")
        const body = await response.json()
        if (!Array.isArray(body?.features)) return result("Unavailable")
        const candidates: LocationCandidate[] = []
        const seen = new Set<string>()
        const requestedRoad = normalize(address.addressText.replace(/^(\d+[\w/.-]*)\s*,?\s*/u, "").replace(/^đường\s+/iu, ""))
        for (const feature of body.features.slice(0, 20)) {
          const p = feature?.properties
          const coordinates = feature?.geometry?.coordinates
          if (!p || feature?.geometry?.type !== "Point" || !Array.isArray(coordinates)) continue
          const point = { longitude: coordinates[0], latitude: coordinates[1] }
          if (typeof point.latitude !== "number" || typeof point.longitude !== "number" || !isSupportedPropertyLocation(point)) continue
          if (typeof p.countrycode === "string" && p.countrycode.toUpperCase() !== "VN") continue
          const text = (name: string): string => typeof p[name] === "string" ? p[name].trim().slice(0, 300) : ""
          const street = text("street") || (p.osm_key === "highway" ? text("name") : "")
          const actualRoad = normalize(street.replace(/^đường\s+/iu, ""))
          if (!requestedRoad || actualRoad !== requestedRoad) continue
          const displayAddress = [...new Set([
            [text("housenumber"), street].filter(Boolean).join(" "),
            text("name") !== street ? text("name") : "", text("district"), text("city"), text("state"), text("country"),
          ].filter(Boolean))].join(", ")
          const identity = `${point.latitude}:${point.longitude}:${displayAddress}`
          if (seen.has(identity)) continue
          seen.add(identity)
          candidates.push({ location: { displayAddress, precision: text("housenumber") ? "house" : "street", provider: "Photon", coordinates: point },
            matchLevel: "Suggestion", reasons: ["ward_unverified"] })
        }
        if (candidates.length) {
          const found = result("Suggestions", candidates)
          if (cache.size >= 50) cache.delete(cache.keys().next().value!)
          cache.set(key, { at: Date.now(), result: found })
          return structuredClone(found)
        }
      }
      return result("NoResults")
    } catch {
      if (signal?.aborted) signal.throwIfAborted()
      return result("Unavailable")
    } finally {
      clearTimeout(timer)
      signal?.removeEventListener("abort", abort)
    }
  }
}

export const previewPropertyLocation = createPropertyLocationSearch()

export type AreaFocus = {
  latitude: number
  longitude: number
  zoom: number
  bounds?: [[number, number], [number, number]]
}
type AreaQuery = { wardName?: string; provinceName?: string }
const AREA_LAYERS = ["city", "district", "locality", "county", "state"]
function areaCore(name: string) {
  return normalize(name.replace(/^(phường|xã|đặc khu|thị trấn|thành phố|tỉnh)\s+/iu, ""))
}
// Finds the selected ward (or, failing that, the province) so the map opens where the property is.
// Ward names after the 2025 merger may be missing from OpenStreetMap, hence the province fallback.
export function createPropertyAreaLocator(options: Options = {}) {
  const cache = new Map<string, AreaFocus | null>()
  return async (area: AreaQuery, signal?: AbortSignal): Promise<AreaFocus | null> => {
    signal?.throwIfAborted()
    const province = area.provinceName?.trim()
    const ward = area.wardName?.trim()
    if (!province) return null
    let endpoint: URL
    try {
      endpoint = new URL(options.endpoint ?? import.meta.env.VITE_PROPERTY_GEOCODING_URL ?? "https://photon.komoot.io/api/")
    } catch {
      return null
    }
    if (endpoint.protocol !== "https:" || endpoint.username || endpoint.password) return null
    const attempts = [
      ...(ward ? [{ query: `${ward}, ${province}`, name: ward, zoom: 15 }] : []),
      { query: province, name: province, zoom: 11 },
    ]
    for (const attempt of attempts) {
      const key = JSON.stringify([endpoint.href, attempt.query])
      if (cache.has(key)) {
        const cached = cache.get(key)
        if (cached) return structuredClone(cached)
        continue
      }
      const url = new URL(endpoint)
      url.searchParams.set("q", attempt.query)
      url.searchParams.set("limit", "10")
      url.searchParams.set("countrycode", "VN")
      url.searchParams.set("bbox", "102,6.5,117.5,24")
      for (const layer of AREA_LAYERS) url.searchParams.append("layer", layer)
      let found: AreaFocus | null = null
      try {
        const response = await (options.fetcher ?? fetch)(url.href, {
          signal, credentials: "omit", headers: { Accept: "application/json" },
        })
        if (!response.ok) return null
        const body = await response.json()
        const features: unknown[] = Array.isArray(body?.features) ? body.features.slice(0, 20) : []
        const wanted = areaCore(attempt.name)
        const provinceCore = areaCore(province)
        const matches = features.flatMap((raw) => {
          const feature = raw as { geometry?: { type?: string; coordinates?: unknown }; properties?: Record<string, unknown> }
          const p = feature?.properties
          const c = feature?.geometry?.coordinates
          if (!p || feature?.geometry?.type !== "Point" || !Array.isArray(c)) return []
          const point = { longitude: Number(c[0]), latitude: Number(c[1]) }
          if (!isSupportedPropertyLocation(point)) return []
          if (typeof p.name !== "string" || areaCore(p.name) !== wanted) return []
          const parents = ["state", "city", "county"].map((k) => typeof p[k] === "string" ? areaCore(p[k] as string) : "")
          const extent = Array.isArray(p.extent) && p.extent.length === 4 && p.extent.every((v) => typeof v === "number")
            ? p.extent as number[] : undefined
          return [{ point, inProvince: parents.includes(provinceCore), extent }]
        })
        const inProvince = matches.filter((m) => m.inProvince)
        // A ward name can repeat across provinces: without a province match, only accept a unique hit.
        const best = inProvince[0] ?? (matches.length === 1 ? matches[0] : undefined)
        if (best) {
          found = { ...best.point, zoom: attempt.zoom }
          if (best.extent) {
            const [minLon, maxLat, maxLon, minLat] = best.extent
            found.bounds = [[minLat, minLon], [maxLat, maxLon]]
          }
        }
      } catch {
        if (signal?.aborted) signal.throwIfAborted()
        return null
      }
      if (cache.size >= 100) cache.delete(cache.keys().next().value!)
      cache.set(key, found)
      if (found) return structuredClone(found)
    }
    return null
  }
}

export const locatePropertyArea = createPropertyAreaLocator()
