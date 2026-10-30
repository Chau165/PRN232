import { afterEach, describe, expect, it, vi } from "vitest"
import {
  apiRequest,
  ApiError,
  setUnauthorizedHandler,
} from "@/api/apiClient"

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  })
}

afterEach(() => setUnauthorizedHandler(() => undefined))

describe("shared API client", () => {
  it("serializes JSON and adds common and Bearer headers", async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse({ id: 3 }))
    vi.stubGlobal("fetch", fetchMock)

    await apiRequest("/users", {
      method: "POST",
      accessToken: "test-token",
      json: { fullName: "Nguyễn An" },
    })

    const [, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    expect(headers.get("Accept")).toBe("application/json")
    expect(headers.get("Content-Type")).toBe("application/json")
    expect(headers.get("Authorization")).toBe("Bearer test-token")
    expect(init?.body).toBe(JSON.stringify({ fullName: "Nguyễn An" }))
  })

  it("leaves multipart Content-Type to fetch for FormData uploads", async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse({ reportId: 1 }))
    vi.stubGlobal("fetch", fetchMock)
    const form = new FormData()
    form.set("Description", "Fixed")

    await apiRequest("/maintenance-requests/1/reports", {
      method: "POST",
      accessToken: "manager-token",
      body: form,
      headers: { "Content-Type": "multipart/form-data" },
    })

    const [, init] = fetchMock.mock.calls[0]
    expect(init?.body).toBe(form)
    expect(new Headers(init?.headers).has("Content-Type")).toBe(false)
  })

  it("returns undefined for 204 without trying to parse a response body", async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(new Response(null, { status: 204 }))
    vi.stubGlobal("fetch", fetchMock)

    await expect(apiRequest<void>("/notifications/1", { method: "PATCH" })).resolves.toBeUndefined()
  })

  it("preserves ProblemDetails status and validation errors and invokes unauthorized handling", async () => {
    const unauthorized = vi.fn()
    setUnauthorizedHandler(unauthorized)
    vi.stubGlobal("fetch", vi.fn<typeof fetch>().mockResolvedValue(jsonResponse({
      title: "Validation failed",
      detail: "Invalid profile",
      errors: {
        FullName: ["Required"],
        Invalid: "not-an-array",
        Mixed: ["Valid", 2],
      },
    }, 401)))

    await expect(apiRequest("/users/me", { accessToken: "expired-token" })).rejects.toMatchObject({
      name: "ApiError",
      message: "Invalid profile",
      status: 401,
      validationErrors: { FullName: ["Required"] },
    })
    expect(unauthorized).toHaveBeenCalledTimes(1)
  })

  it("does not convert aborts into network errors", async () => {
    const controller = new AbortController()
    controller.abort()
    const fetchMock = vi.fn<typeof fetch>().mockRejectedValue(new TypeError("aborted"))
    vi.stubGlobal("fetch", fetchMock)

    await expect(apiRequest("/users/me", { signal: controller.signal })).rejects.toMatchObject({
      name: "TypeError",
      message: "aborted",
    })
  })

  it("normalizes non-abort network errors", async () => {
    vi.stubGlobal("fetch", vi.fn<typeof fetch>().mockRejectedValue(new TypeError("offline")))

    await expect(apiRequest("/users/me")).rejects.toMatchObject({
      name: "ApiError",
      message: "Không thể kết nối đến máy chủ. Vui lòng thử lại.",
    } satisfies Partial<ApiError>)
  })
})
