import { fireEvent, render, screen, waitFor } from "@testing-library/react"
import { MemoryRouter, Route, Routes, useNavigate } from "react-router"
import { describe, expect, it, vi } from "vitest"
import HomePage from "@/pages/HomePage/HomePage"
import type { PublicPage, PublicPropertyCard } from "@/types/publicBrowsing"

vi.mock("@/services/publicBrowsingService", async (importOriginal) => {
  const actual =
    await importOriginal<typeof import("@/services/publicBrowsingService")>()

  return {
    ...actual,
    publicApi: { ...actual.publicApi, listAmenities: vi.fn().mockResolvedValue([
        { amenityId: 4, name: "Wi-Fi" },
        { amenityId: 9, name: "Máy lạnh" },
      ]) },
  }
})

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
  it("restores applied amenity chips during Back and Forward navigation", async () => {
    vi.stubGlobal("fetch", vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(page([property()]))))
    function NavigationHarness() {
      const navigate = useNavigate()
      return <>
        <HomePage />
        <button onClick={() => navigate(-1)}>Quay lại bộ lọc</button>
        <button onClick={() => navigate(1)}>Tiến tới bộ lọc</button>
      </>
    }
    render(<MemoryRouter initialEntries={["/?amenityIds=4", "/?amenityIds=9"]} initialIndex={1}><NavigationHarness /></MemoryRouter>)
    expect(await screen.findByRole("button", { name: "Bỏ tiện ích Máy lạnh" })).toBeTruthy()
    fireEvent.click(screen.getByRole("button", { name: "Quay lại bộ lọc" }))
    expect(await screen.findByRole("button", { name: "Bỏ tiện ích Wi-Fi" })).toBeTruthy()
    expect(screen.queryByRole("button", { name: "Bỏ tiện ích Máy lạnh" })).toBeNull()
    fireEvent.click(screen.getByRole("button", { name: "Tiến tới bộ lọc" }))
    expect(await screen.findByRole("button", { name: "Bỏ tiện ích Máy lạnh" })).toBeTruthy()
    expect(screen.queryByRole("button", { name: "Bỏ tiện ích Wi-Fi" })).toBeNull()
  })

  it("hydrates URL chips, edits only the draft, and clears applied filters", async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(page([property()])))
    vi.stubGlobal("fetch", fetchMock)
    renderHome("/?city=HCM&amenityIds=4")
    expect(await screen.findByRole("button", { name: "Bỏ tiện ích Wi-Fi" })).toBeTruthy()
    fireEvent.click(screen.getByRole("combobox", { name: "Tiện ích mong muốn" }))
    fireEvent.click(screen.getByRole("checkbox", { name: "Máy lạnh" }))
    expect(fetchMock).toHaveBeenCalledTimes(1)
    fireEvent.click(screen.getByRole("button", { name: "Hoàn tất" }))
    fireEvent.click(screen.getByRole("button", { name: "Xóa bộ lọc" }))
    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2))
    expect(requestUrl(fetchMock).searchParams.has("city")).toBe(false)
    expect(requestUrl(fetchMock).searchParams.has("amenityIds")).toBe(false)
    expect(screen.queryByRole("button", { name: "Bỏ tiện ích Wi-Fi" })).toBeNull()
  })

  it("warns about stale URL IDs and only submits IDs confirmed by the API", async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(page([property()])))
    vi.stubGlobal("fetch", fetchMock)
    renderHome("/?amenityIds=4&amenityIds=999")
    await screen.findByText(/1 tiện ích đã chọn không còn tồn tại/)
    fireEvent.click(screen.getByRole("button", { name: "Tìm phòng phù hợp" }))
    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2))
    expect(requestUrl(fetchMock).searchParams.getAll("amenityIds")).toEqual(["4"])
  })

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

    expect(screen.getAllByRole("status").some((element) => element.textContent?.includes("Đang tải")),
    ).toBe(true)
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
    expect(
      screen.getByRole("button", { name: "Tìm trên bản đồ" }),
    ).toBeTruthy()
    expect(screen.queryByText(/Bản đồ chưa khả dụng/)).toBeNull()

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

  it("submits location and multiple named amenities without removed filters", async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValue(jsonResponse(page([property()])))
    vi.stubGlobal("fetch", fetchMock)

    renderHome()
    await screen.findByText("Residence từ API")

    expect(screen.queryByLabelText("Từ khóa")).toBeNull()

    expect(screen.queryByLabelText("Giá tối thiểu")).toBeNull()

    expect(screen.queryByLabelText("Giá tối đa")).toBeNull()

    expect(screen.queryByLabelText("Loại phòng")).toBeNull()
    fireEvent.change(screen.getByLabelText("Thành phố"), {
      target: { value: "TP.HCM" },
    })
    fireEvent.change(screen.getByLabelText("Quận/Huyện"), {
      target: { value: "Quận Hai" },
    })
    fireEvent.change(screen.getByLabelText("Phường/Xã"), {
      target: { value: "Phường Một" },
    })
    fireEvent.click(screen.getByRole("combobox", { name: "Tiện ích mong muốn" }))
    fireEvent.click(await screen.findByRole("checkbox", { name: "Wi-Fi" }))

    fireEvent.click(screen.getByRole("checkbox", { name: "Máy lạnh" }))
    fireEvent.change(screen.getByLabelText("Sắp xếp"), {
      target: { value: "price_asc" },
    })
    fireEvent.click(screen.getByRole("button", { name: "Tìm phòng phù hợp" }))

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2))
    const url = requestUrl(fetchMock)
    expect(url.searchParams.has("keyword")).toBe(false)
    expect(url.searchParams.get("city")).toBe("TP.HCM")
    expect(url.searchParams.get("district")).toBe("Quận Hai")
    expect(url.searchParams.get("ward")).toBe("Phường Một")
    expect(url.searchParams.has("minPrice")).toBe(false)
    expect(url.searchParams.has("maxPrice")).toBe(false)
    expect(url.searchParams.has("roomTypeName")).toBe(false)
    expect(url.searchParams.getAll("amenityIds")).toEqual(["4", "9"])
    expect(url.searchParams.get("sortBy")).toBe("price_asc")
    expect(url.searchParams.get("page")).toBe("1")
  })

  it("resets pagination when location filters change", async () => {
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

    fireEvent.change(screen.getByLabelText("Thành phố"), {
      target: { value: "Hà Nội" },
    })
    fireEvent.click(screen.getByRole("button", { name: "Tìm phòng phù hợp" }))

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(3))

    expect(requestUrl(fetchMock).searchParams.get("page")).toBe("1")

    expect(requestUrl(fetchMock).searchParams.get("city")).toBe("Hà Nội")
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
    fireEvent.change(screen.getByLabelText("Thành phố"), {
      target: { value: "new search" },
    })
    fireEvent.click(screen.getByRole("button", { name: "Tìm phòng phù hợp" }))
    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2))

    pending[1](jsonResponse(page([property({ propertyName: "Kết quả mới" })])))
    expect(await screen.findByText("Kết quả mới")).toBeTruthy()
    pending[0](jsonResponse(page([property({ propertyName: "Kết quả cũ" })])))

    expect(screen.queryByText("Kết quả cũ")).toBeNull()
    expect(fetchMock.mock.calls[0][1]?.signal?.aborted).toBe(true)
  })
})
