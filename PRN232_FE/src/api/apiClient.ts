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

type ApiRequestOptions = Omit<RequestInit, "body"> & {
  accessToken?: string | null
  body?: BodyInit | null
  baseUrl?: string
  fetchImplementation?: typeof fetch
  json?: unknown
  requireJson?: boolean
}

let unauthorizedHandler: () => void = () => {}

export function setUnauthorizedHandler(handler: () => void) {
  unauthorizedHandler = handler
}

export function normalizeApiBaseUrl(value: string | undefined): string {
  const candidate = value?.trim() || "/api"
  if (/^https?:\/\//i.test(candidate)) {
    let parsed: URL
    try {
      parsed = new URL(candidate)
    } catch {
      throw new ApiError("Cấu hình địa chỉ API không hợp lệ.")
    }
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

function getValidationErrors(body: unknown): Record<string, string[]> | undefined {
  if (typeof body !== "object" || body === null || !("errors" in body)) {
    return undefined
  }

  const errors = (body as { errors?: unknown }).errors
  if (!errors || typeof errors !== "object") return undefined

  return Object.fromEntries(
    Object.entries(errors).filter(
      ([, messages]) =>
        Array.isArray(messages) &&
        messages.every((message) => typeof message === "string"),
    ),
  ) as Record<string, string[]>
}

function isAbortError(error: unknown): boolean {
  return (
    typeof error === "object" &&
    error !== null &&
    "name" in error &&
    (error as { name?: unknown }).name === "AbortError"
  )
}

export async function apiRequest<T>(
  endpoint: string,
  options: ApiRequestOptions = {},
): Promise<T> {
  const {
    accessToken,
    baseUrl,
    body: inputBody,
    fetchImplementation,
    headers: inputHeaders,
    json,
    requireJson,
    ...requestOptions
  } = options
  const headers = new Headers(inputHeaders)
  headers.set("Accept", "application/json")
  const requestBody = json !== undefined ? JSON.stringify(json) : inputBody
  const isFormData = typeof FormData !== "undefined" && requestBody instanceof FormData
  if (isFormData) headers.delete("Content-Type")
  else if (requestBody !== undefined && requestBody !== null) headers.set("Content-Type", "application/json")
  if (accessToken) headers.set("Authorization", `Bearer ${accessToken}`)

  let response: Response
  try {
    response = await (fetchImplementation ?? fetch)(`${normalizeApiBaseUrl(baseUrl ?? apiBaseUrl)}${endpoint}`, {
      ...requestOptions,
      body: requestBody,
      headers,
    })
  } catch (error) {
    if (isAbortError(error) || requestOptions.signal?.aborted) throw error
    throw new ApiError("Không thể kết nối đến máy chủ. Vui lòng thử lại.")
  }

  if (response.status === 401 && accessToken) unauthorizedHandler()
  if (response.status === 204) return undefined as T

  const responseBody = await response.json().catch(() => undefined) as
    | { title?: string; detail?: string; message?: string; errors?: unknown }
    | undefined
  if (!response.ok) {
    throw new ApiError(
      responseBody?.detail || responseBody?.title || responseBody?.message || "Yêu cầu không thành công.",
      { status: response.status, validationErrors: getValidationErrors(responseBody) },
    )
  }
  if (requireJson && responseBody === undefined) {
    throw new ApiError("Dữ liệu trả về không hợp lệ.", { status: response.status })
  }
  return responseBody as T
}
