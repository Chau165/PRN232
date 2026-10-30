import { PublicApiError } from "@/services/publicBrowsingError"
import type {
  PublicPropertyListQuery,
  PublicRoomListQuery,
} from "@/types/publicBrowsing"

type PriceInput = string | number
type PageInput = string | number

const DEFAULT_PAGE_SIZE = 10
const MAX_PAGE_SIZE = 50
const MAX_AMENITY_IDS = 20
const MAX_OFFSET = 2_147_483_647

function validationError(field: string, message: string): PublicApiError {
  return new PublicApiError(
    "validation",
    "Hãy kiểm tra lại điều kiện tìm kiếm.",
    { validationErrors: { [field]: [message] } },
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
    return normalizedLeft.integer.length < normalizedRight.integer.length ? -1 : 1
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
  query: PublicPropertyListQuery | PublicRoomListQuery,
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
