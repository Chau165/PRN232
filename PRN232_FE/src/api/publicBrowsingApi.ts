export interface PublicAmenitySummary {
  amenityId: number
  name: string
}

export interface PublicPage<TItem> {
  items: TItem[]
  totalCount: number
  page: number
  pageSize: number
}

export interface PublicPropertyCard {
  propertyId: number
  propertyName: string
  addressText: string
  ward: string
  district: string
  city: string
  primaryImageUrl: string | null
  amenitiesSummary: PublicAmenitySummary[]
  availableRoomCount: number
  startingPrice: number | null
}

export interface PublicPropertyDetail {
  propertyId: number
  propertyName: string
  addressText: string
  ward: string
  district: string
  city: string
  primaryImageUrl: string | null
  availableRoomCount: number
  startingPrice: number | null
  description?: string | null
  commonAmenities?: PublicAmenitySummary[]
  roomAmenities?: PublicPropertyRoomAmenity[]
  livingCosts?: PublicPropertyServiceRate[]
}

export interface PublicPropertyServiceRate {
  rateId?: number
  serviceId: number
  serviceName: string
  price: number
  unitCode: string
  billingBasis: string
  effectiveFrom: string
  effectiveTo?: string | null
}

export interface PublicPropertyRoomAmenity extends PublicAmenitySummary {
  rentableRoomCount: number
}

export interface PublicPropertyRoomCard {
  roomId: number
  roomCode: string
  roomTypeId: number
  roomTypeName: string
  baseRent: number
  areaSqm: number
  primaryImageUrl: string | null
  amenitiesSummary: PublicAmenitySummary[]
}

export interface PublicRoomDetail {
  roomId: number
  roomCode: string
  areaSqm: number
  roomType: {
    roomTypeId: number
    roomTypeName: string
  }
  baseRent: number
  property: {
    propertyId: number
    propertyName: string
    addressText: string
    ward: string
    district: string
    city: string
  }
  images: string[]
  amenities: PublicAmenitySummary[]
  services: Array<{
    serviceId: number
    name: string
    price: number
  }>
  equipments: Array<{
    equipmentId: number
    name: string
  }>
}

export interface ValidationProblemDetails {
  type?: string
  title?: string
  status?: number
  errors?: Record<string, string[]>
}

export type PublicApiErrorKind = "validation" | "not-found" | "network" | "server" | "unexpected"

export class PublicApiError extends Error {
  readonly kind: PublicApiErrorKind
  readonly status?: number
  readonly validationErrors?: Record<string, string[]>

  constructor(
    kind: PublicApiErrorKind,
    message: string,
    options: {
      status?: number
      validationErrors?: Record<string, string[]>
    } = {},
  ) {
    super(message)
    this.name = "PublicApiError"
    this.kind = kind
    this.status = options.status
    this.validationErrors = options.validationErrors
  }
}

export type PublicPropertySort = "propertyId" | "price_asc" | "price_desc"
export type PublicRoomSort = "roomId" | "price_asc" | "price_desc"

type PriceInput = string | number

type PageInput = string | number

interface PublicListQuery {
  minPrice?: PriceInput
  maxPrice?: PriceInput
  roomTypeName?: string
  amenityIds?: readonly (string | number)[]
  sortBy?: string
  page?: PageInput
  pageSize?: PageInput
}

export interface PublicPropertyListQuery extends PublicListQuery {
  keyword?: string
  city?: string
  district?: string
  ward?: string
}

export type PublicRoomListQuery = PublicListQuery

const DEFAULT_PAGE_SIZE = 10
const MAX_PAGE_SIZE = 50
const MAX_AMENITY_IDS = 20
const MAX_OFFSET = 2_147_483_647

function validationError(field: string, message: string): PublicApiError {
  return new PublicApiError(
    "validation",
    "Hãy kiểm tra lại điều kiện tìm kiếm.",
    {
      validationErrors: { [field]: [message] },
    },
  )
}

function optionalText(value: string | undefined): string | undefined {
  const normalized = value?.trim()
  return normalized ? normalized : undefined
}

function normalizePrice(
  value: PriceInput | undefined,
  field: string,
): string | undefined {
  if (value === undefined || String(value).trim() === "") return undefined

  const normalized = String(value).trim()
  if (!/^(?:\d+(?:\.\d+)?|\.\d+)$/.test(normalized)) {
    throw validationError(field, "Giá phải là số không âm.")
  }

  return normalized
}

function compareDecimalStrings(left: string, right: string): number {
  const normalize = (value: string) => {
    const [integer = "0", fraction = ""] = value.split(".")
    const normalizedInteger = integer.replace(/^0+(?=\d)/, "")
    const normalizedFraction = fraction.replace(/0+$/, "")
    return { integer: normalizedInteger || "0", fraction: normalizedFraction }
  }

  const normalizedLeft = normalize(left)
  const normalizedRight = normalize(right)
  if (normalizedLeft.integer.length !== normalizedRight.integer.length) {
    return normalizedLeft.integer.length < normalizedRight.integer.length
      ? -1
      : 1
  }
  if (normalizedLeft.integer !== normalizedRight.integer) {
    return normalizedLeft.integer < normalizedRight.integer ? -1 : 1
  }

  const fractionLength = Math.max(
    normalizedLeft.fraction.length,
    normalizedRight.fraction.length,
  )
  const leftFraction = normalizedLeft.fraction.padEnd(fractionLength, "0")
  const rightFraction = normalizedRight.fraction.padEnd(fractionLength, "0")
  if (leftFraction === rightFraction) return 0
  return leftFraction < rightFraction ? -1 : 1
}

function normalizePositiveInteger(
  value: PageInput | undefined,
  field: string,
  defaultValue: number,
  maximum?: number,
): number {
  if (value === undefined || String(value).trim() === "") return defaultValue

  const normalized = String(value).trim()
  if (!/^\d+$/.test(normalized)) {
    throw validationError(field, "Giá trị phải là số nguyên dương.")
  }

  const parsed = Number(normalized)
  if (
    !Number.isSafeInteger(parsed) ||
    parsed < 1 ||
    (maximum !== undefined && parsed > maximum)
  ) {
    throw validationError(field, "Giá trị nằm ngoài giới hạn cho phép.")
  }

  return parsed
}

function appendPageAndSort(
  params: URLSearchParams,
  pageInput: PageInput | undefined,
  pageSizeInput: PageInput | undefined,
  sortInput: string | undefined,
  defaultSort: string,
  allowedSorts: readonly string[],
): void {
  const page = normalizePositiveInteger(pageInput, "page", 1)
  const pageSize = normalizePositiveInteger(
    pageSizeInput,
    "pageSize",
    DEFAULT_PAGE_SIZE,
    MAX_PAGE_SIZE,
  )

  if ((page - 1) * pageSize > MAX_OFFSET) {
    throw validationError("page", "Trang vượt quá giới hạn cho phép.")
  }

  const sortValue = optionalText(sortInput) ?? defaultSort
  const matchingSort = allowedSorts.find(
    (candidate) => candidate.toLowerCase() === sortValue.toLowerCase(),
  )
  if (!matchingSort) {
    throw validationError("sortBy", "Thứ tự sắp xếp không được hỗ trợ.")
  }

  params.set("sortBy", matchingSort)
  params.set("page", String(page))
  params.set("pageSize", String(pageSize))
}

function appendPricesAndRoomType(
  params: URLSearchParams,
  query: PublicListQuery,
): void {
  const minPrice = normalizePrice(query.minPrice, "minPrice")
  const maxPrice = normalizePrice(query.maxPrice, "maxPrice")

  if (minPrice && maxPrice && compareDecimalStrings(minPrice, maxPrice) > 0) {
    throw validationError(
      "maxPrice",
      "Giá tối đa phải lớn hơn hoặc bằng giá tối thiểu.",
    )
  }

  if (minPrice) params.set("minPrice", minPrice)
  if (maxPrice) params.set("maxPrice", maxPrice)

  const roomTypeName = optionalText(query.roomTypeName)
  if (roomTypeName) params.set("roomTypeName", roomTypeName)
}

function appendAmenityIds(
  params: URLSearchParams,
  amenityIds: readonly (string | number)[] | undefined,
): void {
  const normalizedIds = new Set<number>()
  for (const value of amenityIds ?? []) {
    const normalized = String(value).trim()
    if (!/^\d+$/.test(normalized)) {
      throw validationError(
        "amenityIds",
        "Mã tiện ích phải là số nguyên dương.",
      )
    }

    const id = Number(normalized)
    if (!Number.isSafeInteger(id) || id < 1) {
      throw validationError(
        "amenityIds",
        "Mã tiện ích phải là số nguyên dương.",
      )
    }
    normalizedIds.add(id)
  }

  if (normalizedIds.size > MAX_AMENITY_IDS) {
    throw validationError("amenityIds", "Chỉ có thể lọc tối đa 20 tiện ích.")
  }

  for (const id of [...normalizedIds].sort((left, right) => left - right)) {
    params.append("amenityIds", String(id))
  }
}

export function buildPropertyListParams(
  query: PublicPropertyListQuery = {},
): URLSearchParams {
  const params = new URLSearchParams()
  const keyword = optionalText(query.keyword)
  const city = optionalText(query.city)
  const district = optionalText(query.district)
  const ward = optionalText(query.ward)

  if (keyword) params.set("keyword", keyword)
  if (city) params.set("city", city)
  if (district) params.set("district", district)
  if (ward) params.set("ward", ward)

  appendPricesAndRoomType(params, query)
  appendAmenityIds(params, query.amenityIds)
  appendPageAndSort(
    params,
    query.page,
    query.pageSize,
    query.sortBy,
    "propertyId",
    ["propertyId", "price_asc", "price_desc"],
  )

  return params
}

export function buildRoomListParams(
  query: PublicRoomListQuery = {},
): URLSearchParams {
  const params = new URLSearchParams()
  appendPricesAndRoomType(params, query)
  appendAmenityIds(params, query.amenityIds)
  appendPageAndSort(
    params,
    query.page,
    query.pageSize,
    query.sortBy,
    "roomId",
    ["roomId", "price_asc", "price_desc"],
  )

  return params
}

function normalizeApiBaseUrl(baseUrl: string | undefined): string {
  const candidate = baseUrl?.trim() || "/api"
  if (/^https?:\/\//i.test(candidate)) {
    try {
      const parsed = new URL(candidate)
      if (parsed.username || parsed.password || parsed.search || parsed.hash) {
        throw new Error("invalid base URL")
      }
      const path = parsed.pathname.replace(/\/+$/, "")
      parsed.pathname = path.endsWith("/api") ? path : path + "/api"
      return parsed.origin + parsed.pathname
    } catch {
      throw new PublicApiError(
        "unexpected",
        "Cấu hình địa chỉ API không hợp lệ.",
      )
    }
  }

  if (
    !candidate.startsWith("/") ||
    candidate.includes("?") ||
    candidate.includes("#")
  ) {
    throw new PublicApiError("unexpected", "Cấu hình địa chỉ API không hợp lệ.")
  }

  const path = candidate.replace(/\/+$/, "") || "/api"
  return path.endsWith("/api") ? path : path + "/api"
}

function buildRequestUrl(
  apiBaseUrl: string,
  endpoint: string,
  params: URLSearchParams,
): string {
  const query = params.toString()
  return apiBaseUrl + endpoint + (query ? "?" + query : "")
}

function getValidationErrors(
  body: unknown,
): Record<string, string[]> | undefined {
  if (typeof body !== "object" || body === null || !("errors" in body)) {
    return undefined
  }

  const errors = (body as ValidationProblemDetails).errors
  if (!errors || typeof errors !== "object") return undefined

  return Object.fromEntries(
    Object.entries(errors).filter(
      ([, messages]) =>
        Array.isArray(messages) &&
        messages.every((message) => typeof message === "string"),
    ),
  ) as Record<string, string[]>
}

async function readJson(response: Response): Promise<unknown> {
  try {
    return await response.json()
  } catch {
    return undefined
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

async function requestJson<T>(
  fetchImplementation: typeof fetch,
  url: string,
  signal?: AbortSignal,
): Promise<T> {
  let response: Response
  try {
    response = await fetchImplementation(url, {
      method: "GET",
      headers: { Accept: "application/json" },
      credentials: "omit",
      cache: "no-store",
      signal,
    })
  } catch (error) {
    if (isAbortError(error)) throw error
    throw new PublicApiError(
      "network",
      "Không thể kết nối đến dịch vụ tìm phòng. Vui lòng thử lại.",
    )
  }

  if (!response.ok) {
    const body = await readJson(response)
    if (response.status === 400) {
      throw new PublicApiError(
        "validation",
        "Hãy kiểm tra lại điều kiện tìm kiếm.",
        {
          status: response.status,
          validationErrors: getValidationErrors(body),
        },
      )
    }
    if (response.status === 404) {
      throw new PublicApiError("not-found", "Thông tin hiện không khả dụng.", {
        status: response.status,
      })
    }
    if (response.status >= 500) {
      throw new PublicApiError(
        "server",
        "Dịch vụ đang tạm thời không khả dụng. Vui lòng thử lại.",
        { status: response.status },
      )
    }
    throw new PublicApiError(
      "unexpected",
      "Không thể tải dữ liệu. Vui lòng thử lại.",
      { status: response.status },
    )
  }

  try {
    return (await response.json()) as T
  } catch {
    throw new PublicApiError("unexpected", "Dữ liệu trả về không hợp lệ.", {
      status: response.status,
    })
  }
}

export function createPublicApiClient(
  baseUrl: string | undefined = import.meta.env.VITE_API_BASE_URL,
  fetchImplementation?: typeof fetch,
) {
  const apiBaseUrl = normalizeApiBaseUrl(baseUrl)
  const get = <T,>(
    endpoint: string,
    params: URLSearchParams,
    signal?: AbortSignal,
  ) =>
    requestJson<T>(
      fetchImplementation ?? fetch,
      buildRequestUrl(apiBaseUrl, endpoint, params),
      signal,
    )

  return {
    async listProperties(
      query: PublicPropertyListQuery = {},
      signal?: AbortSignal,
    ): Promise<PublicPage<PublicPropertyCard>> {
      return get("/public/properties", buildPropertyListParams(query), signal)
    },
    async getProperty(
      propertyId: number,
      signal?: AbortSignal,
    ): Promise<PublicPropertyDetail> {
      if (!Number.isSafeInteger(propertyId) || propertyId < 1) {
        throw validationError("propertyId", "Mã khu trọ không hợp lệ.")
      }
      return get(
        "/public/properties/" + propertyId,
        new URLSearchParams(),
        signal,
      )
    },
    async listRooms(
      propertyId: number,
      query: PublicRoomListQuery = {},
      signal?: AbortSignal,
    ): Promise<PublicPage<PublicPropertyRoomCard>> {
      if (!Number.isSafeInteger(propertyId) || propertyId < 1) {
        throw validationError("propertyId", "Mã khu trọ không hợp lệ.")
      }
      return get(
        "/public/properties/" + propertyId + "/rooms",
        buildRoomListParams(query),
        signal,
      )
    },
    async getRoom(
      roomId: number,
      signal?: AbortSignal,
    ): Promise<PublicRoomDetail> {
      if (!Number.isSafeInteger(roomId) || roomId < 1) {
        throw validationError("roomId", "Mã phòng không hợp lệ.")
      }
      return get("/public/rooms/" + roomId, new URLSearchParams(), signal)
    },
  }
}

export const publicApi = createPublicApiClient()
