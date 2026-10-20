import { fireEvent, render, screen, waitFor } from "@testing-library/react"
import { MemoryRouter, Route, Routes } from "react-router"
import { describe, expect, it, vi } from "vitest"
import HomePage from "@/pages/HomePage/HomePage"
import type { PublicPage, PublicPropertyCard } from "@/api/publicBrowsingApi"

function property(
  overrides: Partial<PublicPropertyCard> = {},
): PublicPropertyCard {
  return {
    propertyId: 42,
    propertyName: "Residence từ API",
    addressText: "12 Đường Mẫu",
    ward: "Phường Một",
    district: "Quận Hai",
    city: "TP.HCM",
    primaryImageUrl: null,
    amenitiesSummary: [{ amenityId: 4, name: "Wi-Fi" }],
    availableRoomCount: 7,
    startingPrice: 2850000.5,
    ...overrides,
  }
}

function page(
  items: PublicPropertyCard[],
  overrides: Partial<PublicPage<PublicPropertyCard>> = {},
): PublicPage<PublicPropertyCard> {
  return {
    items,
    totalCount: items.length,
    page: 1,
    pageSize: 10,
    ...overrides,
  }
}

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  })
}

function renderHome(initialEntry = "/") {
  return render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <Routes>
        <Route path="*" element={<HomePage />} />
      </Routes>
    </MemoryRouter>,
  )
}

function requestUrl(
  fetchMock: ReturnType<typeof vi.fn<typeof fetch>>,
  index = -1,
): URL {
  const call = fetchMock.mock.calls.at(index)
  return new URL(String(call?.[0]), window.location.origin)
}

describe("public Home browsing", () => {
  it("shows loading then API fields without fake photos, map pins, distance, or monthly claims", async () => {
    let resolveResponse: ((response: Response) => void) | undefined
    const fetchMock = vi.fn<typeof fetch>().mockImplementation(
      () =>
        new Promise((resolve) => {
          resolveResponse = resolve
        }),
    )
    vi.stubGlobal("fetch", fetchMock)

    renderHome()

    expect(screen.getByRole("status").textContent).toContain("Đang tải")
    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1))
    resolveResponse?.(jsonResponse(page([property()])))

    expect(await screen.findByText("Residence từ API")).toBeTruthy()
    expect(screen.getByText("7 phòng phù hợp")).toBeTruthy()
    expect(
      screen.getByText("12 Đường Mẫu, Phường Một, Quận Hai, TP.HCM"),
    ).toBeTruthy()
    expect(screen.getByText(/2\.850\.000,5/)).toBeTruthy()
    expect(screen.getByText(/Tiện ích phòng đại diện/)).toBeTruthy()
    expect(
      screen.getByRole("link", { name: "Xem chi tiết" }).getAttribute("href"),
    ).toBe("/property/42")
    expect(screen.queryByText(/1,2 km|Cách bạn|\/ tháng/)).toBeNull()
    expect(screen.queryByText("Sunrise Residence")).toBeNull()
    expect(screen.getByText(/Bản đồ chưa khả dụng/)).toBeTruthy()

    const url = requestUrl(fetchMock)
    expect(url.pathname).toBe("/api/public/properties")
    expect(
      new Headers(fetchMock.mock.calls[0][1]?.headers).has("Authorization"),
    ).toBe(false)
    expect(fetchMock.mock.calls[0][1]?.credentials).toBe("omit")
  })

  it("distinguishes a successful empty result from a failed request", async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValue(jsonResponse(page([], { totalCount: 0 })))
    vi.stubGlobal("fetch", fetchMock)

    renderHome()

    expect(
      await screen.findByText("Không tìm thấy khu trọ phù hợp"),
    ).toBeTruthy()
    expect(screen.queryByText(/Không thể kết nối/)).toBeNull()
  })

  it("submits keyword, location, decimal price, type, amenity, and sort filters to the API", async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValue(jsonResponse(page([property()])))
    vi.stubGlobal("fetch", fetchMock)

    renderHome()
    await screen.findByText("Residence từ API")

    fireEvent.change(screen.getByLabelText("Từ khóa"), {
      target: { value: "  khu trọ & phố  " },
    })
    fireEvent.change(screen.getByLabelText("Thành phố"), {
      target: { value: "TP.HCM" },
    })
    fireEvent.change(screen.getByLabelText("Quận/Huyện"), {
      target: { value: "Quận Hai" },
    })
    fireEvent.change(screen.getByLabelText("Phường/Xã"), {
      target: { value: "Phường Một" },
    })
    fireEvent.change(screen.getByLabelText("Giá tối thiểu"), {
      target: { value: "1250000.75" },
    })
    fireEvent.change(screen.getByLabelText("Giá tối đa"), {
      target: { value: "3500000.5" },
    })
    fireEvent.change(screen.getByLabelText("Loại phòng"), {
      target: { value: "Studio" },
    })
    fireEvent.change(screen.getByLabelText("Mã tiện ích"), {
      target: { value: "4, 9, 4" },
    })
    fireEvent.change(screen.getByLabelText("Sắp xếp"), {
      target: { value: "price_asc" },
    })
    fireEvent.click(screen.getByRole("button", { name: "Tìm phòng" }))

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2))
    const url = requestUrl(fetchMock)
    expect(url.searchParams.get("keyword")).toBe("khu trọ & phố")
    expect(url.searchParams.get("city")).toBe("TP.HCM")
    expect(url.searchParams.get("district")).toBe("Quận Hai")
    expect(url.searchParams.get("ward")).toBe("Phường Một")
    expect(url.searchParams.get("minPrice")).toBe("1250000.75")
    expect(url.searchParams.get("maxPrice")).toBe("3500000.5")
    expect(url.searchParams.get("roomTypeName")).toBe("Studio")
    expect(url.searchParams.getAll("amenityIds")).toEqual(["4", "9"])
    expect(url.searchParams.get("sortBy")).toBe("price_asc")
    expect(url.searchParams.get("page")).toBe("1")
  })

  it("blocks invalid price ranges locally and resets pagination when filters change", async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValue(
        jsonResponse(
          page([property()], { totalCount: 21, page: 1, pageSize: 10 }),
        ),
      )
    vi.stubGlobal("fetch", fetchMock)

    renderHome()
    await screen.findByText("Residence từ API")
    fireEvent.click(screen.getByRole("button", { name: "Trang tiếp" }))
    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2))
    expect(requestUrl(fetchMock).searchParams.get("page")).toBe("2")

    fireEvent.change(screen.getByLabelText("Giá tối thiểu"), {
      target: { value: "5" },
    })
    fireEvent.change(screen.getByLabelText("Giá tối đa"), {
      target: { value: "3" },
    })
    fireEvent.click(screen.getByRole("button", { name: "Tìm phòng" }))

    expect(
      await screen.findByText(
        /Giá tối đa phải lớn hơn hoặc bằng giá tối thiểu/,
      ),
    ).toBeTruthy()
    expect(fetchMock).toHaveBeenCalledTimes(2)
  })

  it("shows validation errors and offers a retry after a network failure", async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(
        jsonResponse({ errors: { keyword: ["Từ khóa không hợp lệ"] } }, 400),
      )
      .mockRejectedValueOnce(new TypeError("offline"))
      .mockResolvedValueOnce(jsonResponse(page([property()])))
    vi.stubGlobal("fetch", fetchMock)

    renderHome("/?keyword=bad")

    expect(await screen.findByText("Từ khóa không hợp lệ")).toBeTruthy()
    fireEvent.click(screen.getByRole("button", { name: "Thử lại" }))
    expect(await screen.findByText(/Không thể kết nối/)).toBeTruthy()
    fireEvent.click(screen.getByRole("button", { name: "Thử lại" }))
    expect(await screen.findByText("Residence từ API")).toBeTruthy()
    expect(fetchMock).toHaveBeenCalledTimes(3)
  })

  it("ignores a slower response from an earlier search", async () => {
    const pending: Array<(response: Response) => void> = []
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockImplementation(() => new Promise((resolve) => pending.push(resolve)))
    vi.stubGlobal("fetch", fetchMock)

    renderHome()
    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1))
    fireEvent.change(screen.getByLabelText("Từ khóa"), {
      target: { value: "new search" },
    })
    fireEvent.click(screen.getByRole("button", { name: "Tìm phòng" }))
    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2))

    pending[1](jsonResponse(page([property({ propertyName: "Kết quả mới" })])))
    expect(await screen.findByText("Kết quả mới")).toBeTruthy()
    pending[0](jsonResponse(page([property({ propertyName: "Kết quả cũ" })])))

    expect(screen.queryByText("Kết quả cũ")).toBeNull()
    expect(fetchMock.mock.calls[0][1]?.signal?.aborted).toBe(true)
  })
})
