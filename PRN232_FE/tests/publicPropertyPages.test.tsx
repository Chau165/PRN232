import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react"
import { MemoryRouter, Route, Routes } from "react-router"
import { describe, expect, it, vi } from "vitest"
import HomePage from "@/pages/HomePage/HomePage"
import AvailableRoomsPage from "@/pages/AvailableRoomsPage/AvailableRoomsPage"
import PropertyDetailPage from "@/pages/PropertyDetailPage/PropertyDetailPage"
import type {
  PublicPage,
  PublicPropertyCard,
  PublicPropertyDetail,
  PublicPropertyRoomCard,
  PublicPropertyServiceRate,
  PublicRoomDetail,
} from "@/api/publicBrowsingApi"

function property(
  propertyId: number,
  overrides: Partial<PublicPropertyDetail> = {},
): PublicPropertyDetail {
  return {
    propertyId,
    propertyName: `Property ${propertyId}`,
    addressText: `${propertyId} Đường API`,
    ward: "Phường Một",
    district: "Quận Hai",
    city: "TP.HCM",
    primaryImageUrl: null,
    availableRoomCount: 3,
    startingPrice: 1250000.5,
    roomAmenities: [],
    ...overrides,
  }
}

function room(
  overrides: Partial<PublicPropertyRoomCard> = {},
): PublicPropertyRoomCard {
  return {
    roomId: 501,
    roomCode: "R-501",
    roomTypeId: 2,
    roomTypeName: "Studio",
    baseRent: 1250000.5,
    areaSqm: 20,
    primaryImageUrl: null,
    amenitiesSummary: [{ amenityId: 3, name: "Wi-Fi" }],
    ...overrides,
  }
}

function roomDetail(roomId: number): PublicRoomDetail {
  return {
    roomId,
    roomCode: `R-${roomId}`,
    areaSqm: 20.5,
    roomType: { roomTypeId: 2, roomTypeName: "Studio" },
    baseRent: 1350000,
    property: {
      propertyId: 17,
      propertyName: "Property 17",
      addressText: "17 Đường API",
      ward: "Phường Một",
      district: "Quận Hai",
      city: "TP.HCM",
    },
    images: [],
    amenities: [{ amenityId: 3, name: "Wi-Fi" }],
    services: [
      { serviceId: 8, name: "Điện", price: 4000.5 },
      { serviceId: 9, name: "Nước", price: 25000 },
    ],
    equipments: [],
  }
}

function page<T>(
  items: T[],
  overrides: Partial<PublicPage<T>> = {},
): PublicPage<T> {
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

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/property/:propertyId" element={<PropertyDetailPage />} />
        <Route
          path="/property/:propertyId/rooms"
          element={<AvailableRoomsPage />}
        />
      </Routes>
    </MemoryRouter>,
  )
}

function requestUrl(
  fetchMock: ReturnType<typeof vi.fn<typeof fetch>>,
  index: number,
) {
  return new URL(
    String(fetchMock.mock.calls[index]?.[0]),
    window.location.origin,
  )
}

describe("public property detail and room listing", () => {
  it("loads the exact Property ID and links to its scoped room list", async () => {
    const fetchMock = vi.fn<typeof fetch>().mockImplementation((input) => {
      const url = new URL(String(input), window.location.origin)
      const id = Number(url.pathname.split("/").pop())
      return Promise.resolve(jsonResponse(property(id)))
    })
    vi.stubGlobal("fetch", fetchMock)

    const first = renderAt("/property/7")
    expect(
      await screen.findByRole("heading", { name: "Property 7" }),
    ).toBeTruthy()
    expect(
      screen
        .getByRole("link", { name: "Xem phòng trống" })
        .getAttribute("href"),
    ).toBe("/property/7/rooms")
    expect(screen.getByText("Chưa có thông tin tiện ích.")).toBeTruthy()
    expect(requestUrl(fetchMock, 0).pathname).toBe("/api/public/properties/7")
    first.unmount()

    renderAt("/property/28")
    expect(
      await screen.findByRole("heading", { name: "Property 28" }),
    ).toBeTruthy()
    expect(requestUrl(fetchMock, 1).pathname).toBe("/api/public/properties/28")
    expect(screen.queryByText("Sunrise Residence")).toBeNull()
  })

  it("shows only API amenities for each Property and leaves missing content explicit", async () => {
    const details = new Map([
      [
        1,
        property(1, {
          description: null,
          commonAmenities: [],
          livingCosts: [],
          roomAmenities: [
            { amenityId: 1, name: "Wi-Fi", rentableRoomCount: 1 },
            { amenityId: 2, name: "Air conditioner", rentableRoomCount: 1 },
          ],
        }),
      ],
      [
        2,
        property(2, {
          description: "   ",
          roomAmenities: [
            { amenityId: 3, name: "Balcony", rentableRoomCount: 2 },
          ],
        }),
      ],
    ])
    const fetchMock = vi.fn<typeof fetch>().mockImplementation((input) => {
      const url = new URL(String(input), window.location.origin)
      const propertyId = Number(url.pathname.split("/").pop())
      return Promise.resolve(jsonResponse(details.get(propertyId)))
    })
    vi.stubGlobal("fetch", fetchMock)

    const first = renderAt("/property/1")
    await screen.findByRole("heading", { name: "Property 1" })
    expect(screen.getByRole("heading", { name: "Giới thiệu" })).toBeTruthy()
    expect(screen.getByRole("heading", { name: "Tiện ích" })).toBeTruthy()
    expect(screen.getByRole("heading", { name: "Chi phí sinh hoạt" })).toBeTruthy()
    expect(screen.getByText("Wi-Fi")).toBeTruthy()
    expect(screen.getAllByText(/1 trên 3 phòng hiện có thể thuê/)).toHaveLength(2)
    expect(screen.queryByText("Balcony")).toBeNull()
    expect(screen.getByText("Thông tin giới thiệu đang được cập nhật.")).toBeTruthy()
    expect(screen.queryByText(/Chưa có thông tin tiện ích/)).toBeNull()
    expect(screen.getByText("Chi phí sinh hoạt đang được cập nhật.")).toBeTruthy()
    expect(screen.queryByText(/Sunrise Residence|3\.500|100\.000/)).toBeNull()
    first.unmount()

    renderAt("/property/2")
    await screen.findByRole("heading", { name: "Property 2" })
    expect(screen.getByText("Balcony")).toBeTruthy()
    expect(screen.queryByText("Wi-Fi")).toBeNull()
    expect(screen.getByText(/2 trên 3 phòng hiện có thể thuê/)).toBeTruthy()
    expect(screen.getByText("Thông tin giới thiệu đang được cập nhật.")).toBeTruthy()
    expect(screen.queryByText(/Chưa có thông tin tiện ích/)).toBeNull()
    expect(screen.getByText("Chi phí sinh hoạt đang được cập nhật.")).toBeTruthy()
  })

  it("renders API description, common amenities, and unit-specific living costs", async () => {
    const serviceRate = (
      serviceId: number,
      serviceName: string,
      price: number,
      unitCode: string,
      billingBasis: string,
    ): PublicPropertyServiceRate => ({
      serviceId,
      serviceName,
      price,
      unitCode,
      billingBasis,
      effectiveFrom: "2026-10-01",
      effectiveTo: null,
    })
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(
      jsonResponse(
        property(6, {
          description: "Khu trọ yên tĩnh.\nGần trạm xe buýt.",
          commonAmenities: [{ amenityId: 10, name: "Camera" }],
          roomAmenities: [
            { amenityId: 1, name: "Wi-Fi", rentableRoomCount: 2 },
          ],
          livingCosts: [
            serviceRate(11, "Điện", 3500, "kwh", "usage"),
            serviceRate(12, "Nước", 20000, "m3", "usage"),
            serviceRate(13, "Internet", 100000, "room_month", "fixed_room"),
            serviceRate(14, "Rác", 30000, "room_month", "fixed_room"),
            serviceRate(16, "Phí người", 50000, "person_month", "occupant"),
            serviceRate(17, "Phí xe", 70000, "vehicle_month", "vehicle"),
            serviceRate(15, "Dịch vụ khác", 999000, "custom", "custom"),
          ],
        }),
      ),
    )
    vi.stubGlobal("fetch", fetchMock)

    renderAt("/property/6")

    await screen.findByRole("heading", { name: "Property 6" })
    expect(screen.getByText(/Khu trọ yên tĩnh\./).textContent).toContain(
      "Gần trạm xe buýt.",
    )
    expect(screen.getByText("Camera")).toBeTruthy()
    expect(screen.getByText("Wi-Fi")).toBeTruthy()
    expect(screen.getByText(/3\.500/)).toBeTruthy()
    expect(screen.getByText(/\/kWh/)).toBeTruthy()
    expect(screen.getByText(/20\.000/)).toBeTruthy()
    expect(screen.getByText(/\/m³/)).toBeTruthy()
    expect(screen.getByText(/100\.000/)).toBeTruthy()
    expect(screen.getAllByText(/\/phòng\/tháng/)).toHaveLength(2)
    expect(screen.getByText(/\/người\/tháng/)).toBeTruthy()
    expect(screen.getByText(/\/xe\/tháng/)).toBeTruthy()
    expect(screen.getByText("Dịch vụ khác")).toBeTruthy()
    expect(screen.getByText("Đơn vị tính đang được cập nhật.")).toBeTruthy()
    expect(screen.queryByText(/999\.000/)).toBeNull()
    expect(screen.queryByText("Tổng chi phí")).toBeNull()
  })

  it("combines common and room amenities and maps known service costs to localized icons", async () => {
    const serviceRate = (
      serviceId: number,
      serviceName: string,
      price: number,
      unitCode: string,
      billingBasis: string,
    ): PublicPropertyServiceRate => ({
      serviceId,
      serviceName,
      price,
      unitCode,
      billingBasis,
      effectiveFrom: "2026-10-01",
      effectiveTo: null,
    })
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(
      jsonResponse(
        property(8, {
          commonAmenities: [
            { amenityId: 1, name: "Wi-Fi" },
            { amenityId: 5, name: "Camera" },
          ],
          roomAmenities: [
            { amenityId: 1, name: "Wi-Fi", rentableRoomCount: 3 },
            { amenityId: 2, name: "Balcony", rentableRoomCount: 1 },
          ],
          livingCosts: [
            serviceRate(21, "Electricity - per kWh", 3500, "kwh", "usage"),
            serviceRate(22, "Internet - monthly", 100000, "room_month", "fixed_room"),
            serviceRate(23, "Trash - monthly", 30000, "room_month", "fixed_room"),
            serviceRate(24, "Water - per m3", 20000, "m3", "usage"),
          ],
        }),
      ),
    )
    vi.stubGlobal("fetch", fetchMock)

    renderAt("/property/8")

    await screen.findByRole("heading", { name: "Property 8" })
    expect(screen.getAllByText("Wi-Fi")).toHaveLength(1)
    expect(screen.getByText("Camera")).toBeTruthy()
    expect(screen.getByText("Balcony")).toBeTruthy()
    expect(screen.getByText("Có tại khu trọ và tất cả phòng hiện có thể thuê")).toBeTruthy()
    expect(screen.getByText("Tiện ích chung của khu trọ")).toBeTruthy()
    expect(screen.queryByRole("heading", { name: "Tiện ích chung" })).toBeNull()
    expect(screen.queryByRole("heading", { name: "Tiện ích trong phòng" })).toBeNull()

    for (const label of ["Điện", "Internet", "Rác", "Nước"]) {
      expect(screen.getByText(label)).toBeTruthy()
      expect(screen.getByText(label).closest("li")?.querySelector("svg")).toBeTruthy()
    }
    expect(screen.queryByText("Electricity - per kWh")).toBeNull()
    expect(screen.queryByText("Internet - monthly")).toBeNull()
    expect(screen.queryByText("Trash - monthly")).toBeNull()
    expect(screen.queryByText("Water - per m3")).toBeNull()
    expect(screen.getByText(/3\.500/)).toBeTruthy()
    expect(screen.getByText(/100\.000/)).toBeTruthy()
    expect(screen.getByText(/30\.000/)).toBeTruthy()
    expect(screen.getByText(/20\.000/)).toBeTruthy()
  })

  it("navigates from Home through property and room detail using the selected Property ID", async () => {
    const propertyCard: PublicPropertyCard = {
      propertyId: 17,
      propertyName: "Property 17",
      addressText: "17 Đường API",
      ward: "Phường Một",
      district: "Quận Hai",
      city: "TP.HCM",
      primaryImageUrl: null,
      amenitiesSummary: [],
      availableRoomCount: 1,
      startingPrice: 1350000,
    }
    const fetchMock = vi.fn<typeof fetch>().mockImplementation((input) => {
      const url = new URL(String(input), window.location.origin)
      if (url.pathname === "/api/public/properties") {
        return Promise.resolve(jsonResponse(page([propertyCard])))
      }
      if (url.pathname === "/api/public/properties/17") {
        return Promise.resolve(
          jsonResponse(
            property(17, {
              description: "Live API description",
              livingCosts: [
                {
                  serviceId: 8,
                  serviceName: "Điện",
                  price: 3500,
                  unitCode: "kwh",
                  billingBasis: "usage",
                  effectiveFrom: "2026-10-01",
                  effectiveTo: null,
                },
              ],
            }),
          ),
        )
      }
      if (url.pathname === "/api/public/properties/17/rooms") {
        return Promise.resolve(jsonResponse(page([room()])))
      }
      return Promise.resolve(
        jsonResponse(roomDetail(Number(url.pathname.split("/").pop()))),
      )
    })
    vi.stubGlobal("fetch", fetchMock)

    renderAt("/")
    fireEvent.click(await screen.findByRole("link", { name: "Xem chi tiết" }))
    expect(await screen.findByText("Live API description")).toBeTruthy()
    fireEvent.click(screen.getByRole("link", { name: "Xem phòng trống" }))
    await screen.findByText("Studio")
    fireEvent.click(screen.getByRole("button", { name: "Xem chi tiết" }))

    const dialog = await screen.findByRole("dialog")
    expect(within(dialog).getByText(/3\.500/)).toBeTruthy()
    expect(within(dialog).queryByText(/4\.000,5|25\.000/)).toBeNull()
    expect(within(dialog).getByText(/biểu phí/i)).toBeTruthy()
    expect(
      fetchMock.mock.calls.map((call) =>
        new URL(String(call[0]), window.location.origin).pathname,
      ),
    ).toEqual([
      "/api/public/properties",
      "/api/public/properties/17",
      "/api/public/properties/17",
      "/api/public/properties/17/rooms",
      "/api/public/rooms/501",
    ])
  })

  it("renders the supported amenity labels from the Property API", async () => {
    const amenityNames = [
      "WiFi",
      "Máy lạnh",
      "Bãi xe",
      "Camera",
      "WC riêng",
      "Washing machine",
      "Laundry",
      "Máy giặt",
      "Thang máy",
      "Khóa cửa",
      "Balcony",
    ]
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(
      jsonResponse(
        property(4, {
          roomAmenities: amenityNames.map((name, index) => ({
            amenityId: index + 1,
            name,
            rentableRoomCount: 1,
          })),
        }),
      ),
    )
    vi.stubGlobal("fetch", fetchMock)

    renderAt("/property/4")

    await screen.findByRole("heading", { name: "Property 4" })
    for (const name of amenityNames) {
      expect(screen.getByText(name)).toBeTruthy()
    }
  })

  it("shows one generic unavailable message for a property 404", async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValue(jsonResponse({ title: "Not Found" }, 404))
    vi.stubGlobal("fetch", fetchMock)

    renderAt("/property/999")

    expect(await screen.findByText("Khu trọ hiện không khả dụng")).toBeTruthy()
    expect(screen.queryByText(/không tồn tại|đã hết phòng/i)).toBeNull()
  })

  it("requests rooms for the route Property ID with repeatable filters and server paging", async () => {
    const listedRooms = [
      room({
        roomId: 501,
        roomCode: "R-501",
        roomTypeId: 2,
        baseRent: 1250000.5,
      }),
      room({
        roomId: 502,
        roomCode: "R-502",
        roomTypeId: 8,
        baseRent: 1800000,
      }),
    ]
    const fetchMock = vi.fn<typeof fetch>().mockImplementation((input) => {
      const url = new URL(String(input), window.location.origin)
      return Promise.resolve(
        url.pathname.endsWith("/rooms")
          ? jsonResponse(
              page(listedRooms, { totalCount: 12, page: 2, pageSize: 5 }),
            )
          : jsonResponse(property(17)),
      )
    })
    vi.stubGlobal("fetch", fetchMock)

    renderAt(
      "/property/17/rooms?minPrice=700000&maxPrice=2000000&roomTypeName=Studio&amenityIds=3&amenityIds=9&sortBy=price_asc&page=2&pageSize=5",
    )

    expect(await screen.findByText(/12 phòng phù hợp/)).toBeTruthy()
    expect(screen.getAllByText("Studio")).toHaveLength(2)
    expect(screen.getByText(/1\.250\.000,5/)).toBeTruthy()
    expect(screen.getByText(/1\.800\.000/)).toBeTruthy()
    expect(screen.queryByText("R-501")).toBeNull()
    expect(screen.queryByText("R-502")).toBeNull()

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2))
    const propertyRequest = fetchMock.mock.calls
      .map((_, index) => requestUrl(fetchMock, index))
      .find((url) => url.pathname === "/api/public/properties/17")
    const roomsRequest = fetchMock.mock.calls
      .map((_, index) => requestUrl(fetchMock, index))
      .find((url) => url.pathname === "/api/public/properties/17/rooms")
    expect(propertyRequest).toBeTruthy()
    expect(roomsRequest?.searchParams.get("minPrice")).toBe("700000")
    expect(roomsRequest?.searchParams.get("maxPrice")).toBe("2000000")
    expect(roomsRequest?.searchParams.get("roomTypeName")).toBe("Studio")
    expect(roomsRequest?.searchParams.getAll("amenityIds")).toEqual(["3", "9"])
    expect(roomsRequest?.searchParams.get("sortBy")).toBe("price_asc")
    expect(roomsRequest?.searchParams.get("page")).toBe("2")
    expect(roomsRequest?.searchParams.get("pageSize")).toBe("5")
  })

  it("distinguishes a filtered empty list from a property-level 404", async () => {
    const fetchMock = vi.fn<typeof fetch>().mockImplementation((input) => {
      const url = new URL(String(input), window.location.origin)
      return Promise.resolve(
        url.pathname.endsWith("/rooms")
          ? jsonResponse(page([], { totalCount: 0 }))
          : jsonResponse(property(17)),
      )
    })
    vi.stubGlobal("fetch", fetchMock)

    renderAt("/property/17/rooms")

    expect(
      await screen.findByText("Không có phòng phù hợp với bộ lọc hiện tại"),
    ).toBeTruthy()
    expect(screen.queryByText("Khu trọ hiện không khả dụng")).toBeNull()
  })

  it("resets room pagination when filters change and sends unique repeated amenity IDs", async () => {
    const fetchMock = vi.fn<typeof fetch>().mockImplementation((input) => {
      const url = new URL(String(input), window.location.origin)
      return Promise.resolve(
        url.pathname.endsWith("/rooms")
          ? jsonResponse(
              page([room()], { totalCount: 21, page: 3, pageSize: 10 }),
            )
          : jsonResponse(property(17)),
      )
    })
    vi.stubGlobal("fetch", fetchMock)

    renderAt("/property/17/rooms?page=3&pageSize=10")
    expect(await screen.findByText("Studio")).toBeTruthy()
    fireEvent.change(screen.getByLabelText("Giá tối thiểu"), {
      target: { value: "800000.25" },
    })
    fireEvent.change(screen.getByLabelText("Giá tối đa"), {
      target: { value: "2000000" },
    })
    fireEvent.change(screen.getByLabelText("Loại phòng"), {
      target: { value: "Studio" },
    })
    fireEvent.change(screen.getByLabelText("Mã tiện ích"), {
      target: { value: "9, 3, 9" },
    })
    fireEvent.change(screen.getByLabelText("Sắp xếp"), {
      target: { value: "price_asc" },
    })
    fireEvent.click(screen.getByRole("button", { name: "Áp dụng bộ lọc" }))

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(3))
    const filteredRequest = requestUrl(fetchMock, 2)
    expect(filteredRequest.searchParams.get("minPrice")).toBe("800000.25")
    expect(filteredRequest.searchParams.get("maxPrice")).toBe("2000000")
    expect(filteredRequest.searchParams.get("roomTypeName")).toBe("Studio")
    expect(filteredRequest.searchParams.getAll("amenityIds")).toEqual([
      "3",
      "9",
    ])
    expect(filteredRequest.searchParams.get("sortBy")).toBe("price_asc")
    expect(filteredRequest.searchParams.get("page")).toBe("1")
  })

  it("blocks an invalid room price range before sending a request", async () => {
    const fetchMock = vi.fn<typeof fetch>().mockImplementation((input) => {
      const url = new URL(String(input), window.location.origin)
      return Promise.resolve(
        url.pathname.endsWith("/rooms")
          ? jsonResponse(page([room()]))
          : jsonResponse(property(17)),
      )
    })
    vi.stubGlobal("fetch", fetchMock)

    renderAt("/property/17/rooms")
    expect(await screen.findByText("Studio")).toBeTruthy()
    fireEvent.change(screen.getByLabelText("Giá tối thiểu"), {
      target: { value: "3000000" },
    })
    fireEvent.change(screen.getByLabelText("Giá tối đa"), {
      target: { value: "1000000" },
    })
    fireEvent.click(screen.getByRole("button", { name: "Áp dụng bộ lọc" }))

    expect(
      await screen.findByText(
        /Giá tối đa phải lớn hơn hoặc bằng giá tối thiểu/,
      ),
    ).toBeTruthy()
    expect(fetchMock).toHaveBeenCalledTimes(2)
  })

  it("fetches fresh room details by RoomId and links viewing and deposit by RoomId", async () => {
    const fetchMock = vi.fn<typeof fetch>().mockImplementation((input) => {
      const url = new URL(String(input), window.location.origin)
      if (url.pathname === "/api/public/properties/17")
        return Promise.resolve(
          jsonResponse(
            property(17, {
              livingCosts: [
                {
                  serviceId: 8,
                  serviceName: "Điện",
                  price: 3500,
                  unitCode: "kwh",
                  billingBasis: "usage",
                  effectiveFrom: "2026-10-01",
                  effectiveTo: null,
                },
              ],
            }),
          ),
        )
      if (url.pathname === "/api/public/properties/17/rooms")
        return Promise.resolve(jsonResponse(page([room()])))
      return Promise.resolve(
        jsonResponse(roomDetail(Number(url.pathname.split("/").pop()))),
      )
    })
    vi.stubGlobal("fetch", fetchMock)

    renderAt("/property/17/rooms")
    await screen.findByText("Studio")
    const trigger = screen.getByRole("button", { name: "Xem chi tiết" })
    trigger.focus()
    fireEvent.click(trigger)

    const dialog = await screen.findByRole("dialog")
    expect(within(dialog).getByText("Wi-Fi")).toBeTruthy()
    expect(within(dialog).getByText("20.5 m²")).toBeTruthy()
    expect(within(dialog).getByText(/Điện/)).toBeTruthy()
    expect(within(dialog).getByText(/3\.500/)).toBeTruthy()
    expect(within(dialog).queryByText(/4\.000,5|25\.000/)).toBeNull()
    expect(within(dialog).getByText(/biểu phí/i)).toBeTruthy()
    expect(
      within(dialog).getByText("Chưa có thông tin thiết bị công khai."),
    ).toBeTruthy()
    expect(within(dialog).queryByText("R-501")).toBeNull()
    expect(
      within(dialog)
        .getByRole("link", { name: "Đặt cọc giữ phòng" })
        .getAttribute("href"),
    ).toBe("/deposit?roomId=501")
    expect(
      within(dialog)
        .getByRole("link", { name: "Đặt lịch xem phòng" })
        .getAttribute("href"),
    ).toBe("/viewing-request?roomId=501")
    expect(dialog.querySelector("img")).toBeNull()
    expect(requestUrl(fetchMock, 2).pathname).toBe("/api/public/rooms/501")

    fireEvent.keyDown(document, { key: "Escape" })
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull())
    expect(document.activeElement).toBe(trigger)
  })

  it("keeps room modal focus contained and renders available media and equipment", async () => {
    const fetchMock = vi.fn<typeof fetch>().mockImplementation((input) => {
      const url = new URL(String(input), window.location.origin)
      if (url.pathname === "/api/public/properties/17")
        return Promise.resolve(jsonResponse(property(17)))
      if (url.pathname === "/api/public/properties/17/rooms")
        return Promise.resolve(jsonResponse(page([room()])))
      return Promise.resolve(
        jsonResponse({
          ...roomDetail(501),
          images: ["https://images.example.test/room.jpg"],
          amenities: [],
          services: [],
          equipments: [{ equipmentId: 4, name: "Bàn làm việc" }],
        }),
      )
    })
    vi.stubGlobal("fetch", fetchMock)

    renderAt("/property/17/rooms")
    await screen.findByText("Studio")
    fireEvent.click(screen.getByRole("button", { name: "Xem chi tiết" }))

    const dialog = await screen.findByRole("dialog")
    const closeButton = within(dialog).getByRole("button", {
      name: "Đóng chi tiết phòng",
    })
    expect(within(dialog).getByRole("img", { name: "Ảnh Studio" })).toBeTruthy()
    expect(within(dialog).getByText("Bàn làm việc")).toBeTruthy()
    expect(
      within(dialog).getByText("Chưa có thông tin tiện ích công khai."),
    ).toBeTruthy()
    expect(
      within(dialog).getByText("Chưa có thông tin dịch vụ công khai."),
    ).toBeTruthy()

    const depositLink = within(dialog).getByRole("link", {
      name: "Đặt cọc giữ phòng",
    })
    closeButton.focus()
    fireEvent.keyDown(dialog, { key: "Tab", shiftKey: true })
    expect(document.activeElement).toBe(depositLink)
    fireEvent.keyDown(dialog, { key: "Tab" })
    expect(document.activeElement).toBe(closeButton)

    const backdrop = dialog.parentElement
    if (!backdrop) throw new Error("Room detail backdrop is missing")
    fireEvent.mouseDown(backdrop)
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull())
  })

  it("requests a new detail for each room and refreshes the list after a stale 404", async () => {
    const fetchMock = vi.fn<typeof fetch>().mockImplementation((input) => {
      const url = new URL(String(input), window.location.origin)
      if (url.pathname === "/api/public/properties/17")
        return Promise.resolve(jsonResponse(property(17)))
      if (url.pathname === "/api/public/properties/17/rooms")
        return Promise.resolve(
          jsonResponse(
            page([room(), room({ roomId: 502, roomCode: "R-502" })]),
          ),
        )
      if (url.pathname === "/api/public/rooms/502")
        return Promise.resolve(jsonResponse({ title: "Not Found" }, 404))
      return Promise.resolve(
        jsonResponse(roomDetail(Number(url.pathname.split("/").pop()))),
      )
    })
    vi.stubGlobal("fetch", fetchMock)

    renderAt("/property/17/rooms")
    await screen.findAllByText("Studio")
    fireEvent.click(screen.getAllByRole("button", { name: "Xem chi tiết" })[0])
    await screen.findByRole("dialog")
    fireEvent.click(screen.getByRole("button", { name: "Đóng chi tiết phòng" }))
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull())
    fireEvent.click(screen.getAllByRole("button", { name: "Xem chi tiết" })[1])

    const dialog = await screen.findByRole("dialog")
    expect(
      await within(dialog).findByText("Phòng hiện không khả dụng"),
    ).toBeTruthy()
    expect(requestUrl(fetchMock, 3).pathname).toBe("/api/public/rooms/502")
    fireEvent.click(
      within(dialog).getByRole("button", { name: "Tải lại danh sách phòng" }),
    )
    await waitFor(() =>
      expect(
        fetchMock.mock.calls.filter((call) =>
          String(call[0]).includes("/api/public/properties/17/rooms"),
        ).length,
      ).toBe(2),
    )
    expect(screen.queryByRole("dialog")).toBeNull()
  })
})
