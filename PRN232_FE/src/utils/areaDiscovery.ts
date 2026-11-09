import type {
  PublicPropertyAreaCard,
  PublicPropertyAreaQuery,
} from "@/types/publicBrowsing"

export interface AreaCoordinates {
  latitude: number
  longitude: number
}

export interface ResolvedArea {
  district?: string
  ward?: string
  city?: string
}

export type MatchedAreaLevel = "district" | "ward" | "city"

export type AreaDiscoveryStep = "location" | "geocode" | "reverse-geocode" | "properties"
export type AreaDiscoveryErrorKind = "location-unavailable"
  | "permission-denied"
  | "reverse-geocode"
  | "area-not-specific"
  | "area-api" | "geocode"

export interface AreaDiscoveryResult {
  locationLabel?: string

  coordinates: AreaCoordinates
  area: ResolvedArea
  matchedLevel: MatchedAreaLevel
  properties: PublicPropertyAreaCard[]
}

interface DiscoverCurrentAreaOptions {
  geolocation: Geolocation | null
  fetchImplementation: typeof fetch
  listPropertiesByArea: (
    query: PublicPropertyAreaQuery,
    signal?: AbortSignal,
  ) => Promise<PublicPropertyAreaCard[]>
  onProgress?: (step: AreaDiscoveryStep) => void
  onLocation?: (coordinates: AreaCoordinates) => void
  signal?: AbortSignal
}

interface ReverseGeocodeResponse {
  address?: Record<string, unknown>
  features?: Array<{ properties?: Record<string, unknown> }>
}

export class AreaDiscoveryError extends Error {
  constructor(readonly kind: AreaDiscoveryErrorKind, message: string,
  ) {
    super(message)
    this.name = "AreaDiscoveryError"
  }
}

function isAbortError(error: unknown): boolean {
  return (
    typeof error === "object" &&
    error !== null &&
    "name" in error &&
    (error as { name?: unknown }).name === "AbortError"
  )
}

function readLocationName(
  address: Record<string, unknown>,
  candidates: readonly string[],
): string | undefined {
  for (const candidate of candidates) {
    const value = address[candidate]
    if (typeof value === "string" && value.trim()) return value.trim()
  }
  return undefined
}

function areaFromReverseGeocode(response: unknown): ResolvedArea {
  if (typeof response !== "object" || response === null) {
    throw new AreaDiscoveryError(
      "reverse-geocode",
      "Không xác định được khu vực hiện tại từ vị trí.",
    )
  }

  const payload = response as ReverseGeocodeResponse
  const address = payload.address ?? payload.features?.[0]?.properties
  if (!address || typeof address !== "object") {
    throw new AreaDiscoveryError(
      "reverse-geocode",
      "Không xác định được khu vực hiện tại từ vị trí.",
    )
  }

  const area: ResolvedArea = {
    district: readLocationName(address, [
      "city_district",
      "district",
      "county",
      "state_district",
    ]),
    ward: readLocationName(address, [
      "ward",
      "suburb",
      "city_subdivision",
      "quarter",
      "neighbourhood",
    ]),
    city: readLocationName(address, [
      "city",
      "town",
      "municipality",
      "province",
      "state",
    ]),
  }

  if (!area.district && !area.ward && !area.city) {
    throw new AreaDiscoveryError(
      "reverse-geocode",
      "Không xác định được khu vực hiện tại từ vị trí.",
    )
  }

  return area
}

function getCurrentCoordinates(geolocation: Geolocation | null,
): Promise<AreaCoordinates> {
  if (!geolocation) {
    return Promise.reject(
      new AreaDiscoveryError(
        "location-unavailable",
        "Trình duyệt này không hỗ trợ lấy vị trí hiện tại.",
      ),
    )
  }

  return new Promise((resolve, reject) => {
    geolocation.getCurrentPosition(
      (position) => {
        const { latitude, longitude } = position.coords
        if (
          !Number.isFinite(latitude) ||
          !Number.isFinite(longitude) ||
          latitude < -90 ||
          latitude > 90 ||
          longitude < -180 ||
          longitude > 180
        ) {
          reject(
            new AreaDiscoveryError(
              "location-unavailable",
              "Trình duyệt trả về vị trí không hợp lệ.",
            ),
          )
          return
        }
        resolve({ latitude, longitude })
      },
      (error) => {
        reject(
          error.code === 1
            ? new AreaDiscoveryError(
                "permission-denied",
                "Bạn đã từ chối quyền vị trí. Hãy bật quyền vị trí trong trình duyệt rồi thử lại.",
              )
            : new AreaDiscoveryError(
                "location-unavailable",
                "Không thể lấy vị trí hiện tại. Hãy kiểm tra dịch vụ vị trí rồi thử lại.",
              ),
        )
      },
      { enableHighAccuracy: false, maximumAge: 120_000, timeout: 15_000 },
    )
  })
}

async function reverseGeocode(
  coordinates: AreaCoordinates,
  fetchImplementation: typeof fetch,
  signal?: AbortSignal,
): Promise<ResolvedArea> {
  const endpoint =
    import.meta.env.VITE_REVERSE_GEOCODING_URL?.trim() ||
    "https://photon.komoot.io/reverse"
  let url: URL
  try {
    url = new URL(endpoint)
    if (url.protocol !== "https:" && url.protocol !== "http:") {
      throw new Error("unsupported protocol")
    }
  } catch {
    throw new AreaDiscoveryError(
      "reverse-geocode",
      "Cấu hình dịch vụ xác định khu vực không hợp lệ.",
    )
  }

  url.searchParams.set("lat", String(coordinates.latitude))
  url.searchParams.set("lon", String(coordinates.longitude))

  let response: Response
  try {
    response = await fetchImplementation(url.toString(), {
      method: "GET",
      headers: { Accept: "application/json", "Accept-Language": "vi" },
      credentials: "omit",
      cache: "default",
      signal,
    })
  } catch (error) {
    if (isAbortError(error)) throw error
    throw new AreaDiscoveryError(
      "reverse-geocode",
      "Không thể kết nối dịch vụ xác định khu vực. Vui lòng thử lại.",
    )
  }

  if (!response.ok) {
    throw new AreaDiscoveryError(
      "reverse-geocode",
      "Không xác định được khu vực hiện tại vì dịch vụ đang tạm thời không khả dụng.",
    )
  }

  try {
    return areaFromReverseGeocode(await response.json())
  } catch (error) {
    if (error instanceof AreaDiscoveryError) throw error
    throw new AreaDiscoveryError(
      "reverse-geocode",
      "Dữ liệu khu vực nhận được không hợp lệ.",
    )
  }
}

async function loadProperties(
  query: PublicPropertyAreaQuery,
  listPropertiesByArea: DiscoverCurrentAreaOptions["listPropertiesByArea"],
  signal?: AbortSignal,
): Promise<PublicPropertyAreaCard[]> {
  try {
    return await listPropertiesByArea(query, signal)
  } catch (error) {
    if (isAbortError(error)) throw error
    throw new AreaDiscoveryError(
      "area-api",
      "Không tải được danh sách khu trọ. Vui lòng thử lại.",
    )
  }
}

export async function discoverEnteredArea(
  location: string,

  options: Omit<DiscoverCurrentAreaOptions, "geolocation">,
): Promise<AreaDiscoveryResult> {
  const {
    fetchImplementation,
    listPropertiesByArea,
    signal,
    onProgress,
    onLocation,
  } = options

  if (!location.trim()) {
    throw new AreaDiscoveryError(
      "geocode",
      "Hãy nhập địa điểm hoặc chọn vị trí hiện tại của bạn.",
    )
  }

  onProgress?.("geocode")

  let coordinates: AreaCoordinates

  let area: ResolvedArea

  let locationLabel: string

  try {
    const url = new URL(
      import.meta.env.VITE_GEOCODING_URL?.trim() ||
        "https://photon.komoot.io/api/",
    )

    if (!["https:", "http:"].includes(url.protocol))
      throw new Error("Invalid URL")

    url.searchParams.set("q", location.trim())

    url.searchParams.set("countrycode", "VN")

    url.searchParams.set("limit", "1")

    const response = await fetchImplementation(url.toString(), {
      headers: { Accept: "application/json", "Accept-Language": "vi" },

      credentials: "omit",

      signal,
    })

    if (!response.ok) throw new Error("Geocoding unavailable")

    const payload = await response.json()

    const feature = payload?.features?.[0]

    if (!feature) {
      throw new AreaDiscoveryError(
        "geocode",
        "Không tìm thấy địa điểm. Hãy nhập địa chỉ cụ thể hơn, kèm quận hoặc thành phố.",
      )
    }

    const [longitude, latitude] = feature.geometry?.coordinates ?? []

    if (
      typeof latitude !== "number" ||
      typeof longitude !== "number" ||
      !Number.isFinite(latitude) ||
      !Number.isFinite(longitude) ||
      Math.abs(latitude) > 90 ||
      Math.abs(longitude) > 180
    )
      throw new Error("Invalid coordinates")

    coordinates = { latitude, longitude }

    try {
      area = areaFromReverseGeocode({ features: [feature] })
    } catch {
      area = {}
    }

    const properties = feature.properties ?? {}

    // Administrative places may expose their name without a separate city/district field.

    if (properties.osm_value === "city" || properties.type === "city") {
      area = { city: readLocationName(properties, ["name", "city", "state"]) }
    }

    locationLabel =
      [
        ...new Set(
          [
            readLocationName(properties, ["name", "street"]),
            area.ward,
            area.district,
            area.city,
          ].filter(Boolean),
        ),
      ].join(", ") || location.trim()
  } catch (error) {
    if (isAbortError(error) || signal?.aborted) throw error

    if (error instanceof AreaDiscoveryError && error.kind === "geocode")
      throw error

    throw new AreaDiscoveryError(
      "geocode",
      "Không thể xác định địa điểm. Vui lòng thử lại hoặc nhập địa chỉ cụ thể hơn.",
    )
  }

  signal?.throwIfAborted()

  onLocation?.(coordinates)

  // Use the selected point's administrative area, with city-wide search allowed for typed places.

  if (!area.district && !area.ward && !area.city) {
    area = await reverseGeocode(coordinates, fetchImplementation, signal)
  }

  const matchedLevel = area.district ? "district" : area.ward ? "ward" : "city"

  const query: PublicPropertyAreaQuery = {
    ...(area.city ? { city: area.city } : {}),

    ...(matchedLevel === "district" ? { district: area.district } : {}),

    ...(matchedLevel === "ward" ? { ward: area.ward } : {}),
  }

  onProgress?.("properties")

  const properties = await loadProperties(query, listPropertiesByArea, signal)

  return { coordinates, area, matchedLevel, properties, locationLabel }
}

export async function discoverCurrentArea({
  geolocation,
  fetchImplementation,
  listPropertiesByArea,
  onProgress,
  onLocation,
  signal,
}: DiscoverCurrentAreaOptions): Promise<AreaDiscoveryResult> {
  onProgress?.("location")
  const coordinates = await getCurrentCoordinates(geolocation)
  onLocation?.(coordinates)

  onProgress?.("reverse-geocode")
  const area = await reverseGeocode(coordinates, fetchImplementation, signal)

  const initialLevel: MatchedAreaLevel | null = area.district
    ? "district"
    : area.ward
      ? "ward"
      : null
  if (!initialLevel) {
    throw new AreaDiscoveryError(
      "area-not-specific",
      "Chỉ xác định được thành phố, chưa đủ thông tin để lọc khu trọ quanh bạn. Hãy thử lại sau.",
    )
  }
  const initialQuery: PublicPropertyAreaQuery = initialLevel === "district"
    ? { district: area.district!, ...(area.city ? { city: area.city } : {}) }
    : { ward: area.ward!, ...(area.city ? { city: area.city } : {}) }

  onProgress?.("properties")
  const areaProperties = await loadProperties(
    initialQuery,
    listPropertiesByArea,
    signal,
  )
  return {
    coordinates,
    area,
    matchedLevel: initialLevel,
    properties: areaProperties,
  }
}

export function buildGoogleMapsDirectionsUrl(
  property: Pick<
    PublicPropertyAreaCard,
    "latitude" | "longitude" | "addressText" | "ward" | "district" | "city"
  >,
): string | null {
  const hasUsableCoordinates =
    Number.isFinite(property.latitude) &&
    Number.isFinite(property.longitude) &&
    property.latitude >= -90 &&
    property.latitude <= 90 &&
    property.longitude >= -180 &&
    property.longitude <= 180 &&
    (property.latitude !== 0 || property.longitude !== 0)

  const address = [
    property.addressText,
    property.ward,
    property.district,
    property.city,
  ]
    .filter((part): part is string => typeof part === "string" && part.trim().length > 0,
    )
    .map((part) => part.trim())
    .join(", ")
  const destination = hasUsableCoordinates
    ? `${property.latitude},${property.longitude}`
    : address
  if (!destination) return null

  const parameters = new URLSearchParams({ api: "1", destination })
  return `https://www.google.com/maps/dir/?${parameters.toString()}`
}
