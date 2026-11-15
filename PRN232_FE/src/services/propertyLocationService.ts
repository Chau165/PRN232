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
