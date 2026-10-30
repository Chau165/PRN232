import type {
  PublicPropertyListQuery,
  PublicRoomListQuery,
} from "@/types/publicBrowsing"

export interface PropertySearchDraft {
  keyword: string
  city: string
  district: string
  ward: string
  minPrice: string
  maxPrice: string
  roomTypeName: string
  amenityIds: string
  sortBy: string
}

export interface RoomSearchDraft {
  minPrice: string
  maxPrice: string
  roomTypeName: string
  amenityIds: string
  sortBy: string
}

export function propertyQueryFromSearch(
  search: string,
): PublicPropertyListQuery {
  const params = new URLSearchParams(search)
  const page = params.get("page") ?? undefined
  const pageSize = params.get("pageSize") ?? undefined
  const sortBy = params.get("sortBy") ?? undefined

  return {
    keyword: params.get("keyword") ?? undefined,
    city: params.get("city") ?? undefined,
    district: params.get("district") ?? undefined,
    ward: params.get("ward") ?? undefined,
    minPrice: params.get("minPrice") ?? undefined,
    maxPrice: params.get("maxPrice") ?? undefined,
    roomTypeName: params.get("roomTypeName") ?? undefined,
    amenityIds: params.getAll("amenityIds"),
    sortBy,
    page,
    pageSize,
  }
}

export function propertySearchDraft(
  query: PublicPropertyListQuery,
): PropertySearchDraft {
  return {
    keyword: String(query.keyword ?? ""),
    city: String(query.city ?? ""),
    district: String(query.district ?? ""),
    ward: String(query.ward ?? ""),
    minPrice: String(query.minPrice ?? ""),
    maxPrice: String(query.maxPrice ?? ""),
    roomTypeName: String(query.roomTypeName ?? ""),
    amenityIds: (query.amenityIds ?? []).join(", "),
    sortBy: String(query.sortBy ?? "propertyId"),
  }
}

export function propertySearchParams(
  draft: PropertySearchDraft,
): URLSearchParams {
  const params = new URLSearchParams()
  const fields: Array<[string, string]> = [
    ["keyword", draft.keyword],
    ["city", draft.city],
    ["district", draft.district],
    ["ward", draft.ward],
    ["minPrice", draft.minPrice],
    ["maxPrice", draft.maxPrice],
    ["roomTypeName", draft.roomTypeName],
  ]
  for (const [key, value] of fields) {
    if (value.trim()) params.set(key, value.trim())
  }

  for (const amenityId of new Set(
    draft.amenityIds.split(/[ ,]+/).filter(Boolean),
  )) {
    params.append("amenityIds", amenityId)
  }
  params.set("sortBy", draft.sortBy || "propertyId")
  params.set("page", "1")
  params.set("pageSize", "10")
  return params
}

export function roomQueryFromSearch(search: string): PublicRoomListQuery {
  const params = new URLSearchParams(search)
  return {
    minPrice: params.get("minPrice") ?? undefined,
    maxPrice: params.get("maxPrice") ?? undefined,
    roomTypeName: params.get("roomTypeName") ?? undefined,
    amenityIds: params.getAll("amenityIds"),
    sortBy: params.get("sortBy") ?? undefined,
    page: params.get("page") ?? undefined,
    pageSize: params.get("pageSize") ?? undefined,
  }
}

export function roomSearchDraft(query: PublicRoomListQuery): RoomSearchDraft {
  return {
    minPrice: String(query.minPrice ?? ""),
    maxPrice: String(query.maxPrice ?? ""),
    roomTypeName: String(query.roomTypeName ?? ""),
    amenityIds: (query.amenityIds ?? []).join(", "),
    sortBy: String(query.sortBy ?? "roomId"),
  }
}

export function roomSearchParams(draft: RoomSearchDraft): URLSearchParams {
  const params = new URLSearchParams()
  for (const field of ["minPrice", "maxPrice", "roomTypeName"] as const) {
    const value = draft[field].trim()
    if (value) params.set(field, value)
  }
  for (const amenityId of new Set(
    draft.amenityIds.split(/[ ,]+/).filter(Boolean),
  )) {
    params.append("amenityIds", amenityId)
  }
  params.set("sortBy", draft.sortBy || "roomId")
  params.set("page", "1")
  params.set("pageSize", "10")
  return params
}
