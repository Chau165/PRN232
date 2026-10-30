import {
  apiRequest,
  ApiError,
  normalizeApiBaseUrl,
} from "@/api/apiClient"
import {
  PublicApiError,
  type PublicApiErrorKind,
} from "@/services/publicBrowsingError"
import type {
  PublicPage,
  PublicPropertyAreaCard,
  PublicPropertyAreaQuery,
  PublicPropertyCard,
  PublicPropertyDetail,
  PublicPropertyListQuery,
  PublicPropertyRoomCard,
  PublicRoomDetail,
  PublicRoomListQuery,
} from "@/types/publicBrowsing"
import {
  buildPropertyListParams,
  buildRoomListParams,
} from "@/utils/publicBrowsingQuery"

export { PublicApiError }
export type { PublicApiErrorKind }
export { buildPropertyListParams, buildRoomListParams }
export type {
  PublicPage,
  PublicPropertyAreaCard,
  PublicPropertyAreaQuery,
  PublicPropertyCard,
  PublicPropertyDetail,
  PublicPropertyListQuery,
  PublicPropertyRoomCard,
  PublicRoomDetail,
  PublicRoomListQuery,
}

function isAbortError(error: unknown): boolean {
  return (
    typeof error === "object" &&
    error !== null &&
    "name" in error &&
    (error as { name?: unknown }).name === "AbortError"
  )
}

function publicRequestError(error: unknown): PublicApiError {
  if (!(error instanceof ApiError)) {
    return new PublicApiError(
      "network",
      "Không thể kết nối đến dịch vụ tìm phòng. Vui lòng thử lại.",
    )
  }

  const { status, validationErrors } = error
  if (status === 400) {
    return new PublicApiError(
      "validation",
      "Hãy kiểm tra lại điều kiện tìm kiếm.",
      { status, validationErrors },
    )
  }
  if (status === 404) {
    return new PublicApiError("not-found", "Thông tin hiện không khả dụng.", {
      status,
    })
  }
  if (status !== undefined && status >= 500) {
    return new PublicApiError(
      "server",
      "Dịch vụ đang tạm thời không khả dụng. Vui lòng thử lại.",
      { status },
    )
  }
  if (status === undefined && error.message !== "Cấu hình địa chỉ API không hợp lệ.") {
    return new PublicApiError(
      "network",
      "Không thể kết nối đến dịch vụ tìm phòng. Vui lòng thử lại.",
    )
  }
  return new PublicApiError(
    "unexpected",
    "Không thể tải dữ liệu. Vui lòng thử lại.",
    { status },
  )
}

function buildRequestEndpoint(endpoint: string, params: URLSearchParams): string {
  const query = params.toString()
  return endpoint + (query ? `?${query}` : "")
}

export function createPublicApiClient(
  baseUrl: string | undefined = import.meta.env.VITE_API_BASE_URL,
  fetchImplementation?: typeof fetch,
) {
  let apiBaseUrl: string
  try {
    apiBaseUrl = normalizeApiBaseUrl(baseUrl)
  } catch {
    throw new PublicApiError(
      "unexpected",
      "Cấu hình địa chỉ API không hợp lệ.",
    )
  }

  const get = async <T,>(
    endpoint: string,
    params: URLSearchParams,
    signal?: AbortSignal,
  ): Promise<T> => {
    try {
      return await apiRequest<T>(buildRequestEndpoint(endpoint, params), {
        method: "GET",
        signal,
        credentials: "omit",
        cache: "no-store",
        baseUrl: apiBaseUrl,
        requireJson: true,
        ...(fetchImplementation ? { fetchImplementation } : {}),
      })
    } catch (error) {
      if (isAbortError(error) || signal?.aborted) throw error
      throw publicRequestError(error)
    }
  }

  return {
    async listProperties(
      query: PublicPropertyListQuery = {},
      signal?: AbortSignal,
    ): Promise<PublicPage<PublicPropertyCard>> {
      return get("/public/properties", buildPropertyListParams(query), signal)
    },
    async listPropertiesByArea(
      query: PublicPropertyAreaQuery,
      signal?: AbortSignal,
    ): Promise<PublicPropertyAreaCard[]> {
      const params = new URLSearchParams()
      const city = query.city?.trim()
      const district = query.district?.trim()
      const ward = query.ward?.trim()

      if (city) params.set("city", city)
      if (district) params.set("district", district)
      if (ward) params.set("ward", ward)
      if (!city && !district && !ward) {
        throw new PublicApiError(
          "validation",
          "Hãy kiểm tra lại điều kiện tìm kiếm.",
          {
            validationErrors: {
              district: [
                "Hãy cung cấp ít nhất thành phố, quận/huyện hoặc phường/xã.",
              ],
            },
          },
        )
      }

      return get("/public/properties/by-area", params, signal)
    },
    async getProperty(
      propertyId: number,
      signal?: AbortSignal,
    ): Promise<PublicPropertyDetail> {
      if (!Number.isSafeInteger(propertyId) || propertyId < 1) {
        throw new PublicApiError("validation", "Hãy kiểm tra lại điều kiện tìm kiếm.", {
          validationErrors: { propertyId: ["Mã khu trọ không hợp lệ."] },
        })
      }
      return get(
        `/public/properties/${propertyId}`,
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
        throw new PublicApiError("validation", "Hãy kiểm tra lại điều kiện tìm kiếm.", {
          validationErrors: { propertyId: ["Mã khu trọ không hợp lệ."] },
        })
      }
      return get(
        `/public/properties/${propertyId}/rooms`,
        buildRoomListParams(query),
        signal,
      )
    },
    async getRoom(
      roomId: number,
      signal?: AbortSignal,
    ): Promise<PublicRoomDetail> {
      if (!Number.isSafeInteger(roomId) || roomId < 1) {
        throw new PublicApiError("validation", "Hãy kiểm tra lại điều kiện tìm kiếm.", {
          validationErrors: { roomId: ["Mã phòng không hợp lệ."] },
        })
      }
      return get(`/public/rooms/${roomId}`, new URLSearchParams(), signal)
    },
  }
}

export const publicApi = createPublicApiClient()
