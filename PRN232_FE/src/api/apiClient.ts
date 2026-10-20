export class ApiError extends Error {
  readonly status?: number
  readonly validationErrors?: Record<string, string[]>

  constructor(
    message: string,
    options: { status?: number; validationErrors?: Record<string, string[]> } = {},
  ) {
    super(message)
    this.name = "ApiError"
    this.status = options.status
    this.validationErrors = options.validationErrors
  }
}

type ApiRequestOptions = RequestInit & {
  accessToken?: string | null
}

let unauthorizedHandler: () => void = () => {}

export function setUnauthorizedHandler(handler: () => void) {
  unauthorizedHandler = handler
}

function normalizeApiBaseUrl(value: string | undefined): string {
  const candidate = value?.trim() || "/api"
  if (/^https?:\/\//i.test(candidate)) {
    const parsed = new URL(candidate)
    if (parsed.username || parsed.password || parsed.search || parsed.hash) {
      throw new ApiError("Cấu hình địa chỉ API không hợp lệ.")
    }
    const path = parsed.pathname.replace(/\/+$/, "")
    parsed.pathname = path.endsWith("/api") ? path : `${path}/api`
    return parsed.origin + parsed.pathname
  }

  if (!candidate.startsWith("/") || candidate.includes("?") || candidate.includes("#")) {
    throw new ApiError("Cấu hình địa chỉ API không hợp lệ.")
  }
  const path = candidate.replace(/\/+$/, "") || "/api"
  return path.endsWith("/api") ? path : `${path}/api`
}

const apiBaseUrl = normalizeApiBaseUrl(import.meta.env.VITE_API_BASE_URL)

export async function apiRequest<T>(
  endpoint: string,
  options: ApiRequestOptions = {},
): Promise<T> {
  const { accessToken, headers: inputHeaders, ...requestOptions } = options
  const headers = new Headers(inputHeaders)
  headers.set("Accept", "application/json")
  if (requestOptions.body && !(requestOptions.body instanceof FormData)) {
    headers.set("Content-Type", "application/json")
  }
  if (accessToken) headers.set("Authorization", `Bearer ${accessToken}`)

  let response: Response
  try {
    response = await fetch(`${apiBaseUrl}${endpoint}`, {
      ...requestOptions,
      headers,
    })
  } catch {
    throw new ApiError("Không thể kết nối đến máy chủ. Vui lòng thử lại.")
  }

  if (response.status === 401 && accessToken) unauthorizedHandler()
  if (response.status === 204) return undefined as T

  const body = await response.json().catch(() => undefined) as
    | { title?: string; detail?: string; message?: string; errors?: Record<string, string[]> }
    | undefined
  if (!response.ok) {
    throw new ApiError(
      body?.detail || body?.title || body?.message || "Yêu cầu không thành công.",
      { status: response.status, validationErrors: body?.errors },
    )
  }
  return body as T
}
