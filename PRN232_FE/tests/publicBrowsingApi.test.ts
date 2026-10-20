import { describe, expect, it, vi } from "vitest"
import {
  buildPropertyListParams,
  buildRoomListParams,
  createPublicApiClient,
  PublicApiError,
  type PublicPage,
  type PublicPropertyCard,
  type PublicRoomDetail,
} from "@/api/publicBrowsingApi"
import {
  availableRoomsPath,
  parsePositiveRouteId,
  propertyDetailPath,
  routes,
} from "@/constants/routes"

function response(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  })
}

function createFetchMock(body: unknown, status = 200) {
  return vi.fn<typeof fetch>().mockResolvedValue(response(body, status))
}

describe("public browsing query contracts", () => {
  it("encodes text, repeats sorted unique amenity IDs, and keeps decimal prices", () => {
    const query = buildPropertyListParams({
      keyword: "  nhà trọ & phố  ",
      city: " TP.HCM ",
      minPrice: "1250000.75",
      maxPrice: "3000000.5",
      amenityIds: ["8", "3", "8"],
      page: "2",
      pageSize: "25",
    })

    expect(query.get("keyword")).toBe("nhà trọ & phố")
    expect(query.get("city")).toBe("TP.HCM")
    expect(query.get("minPrice")).toBe("1250000.75")
    expect(query.getAll("amenityIds")).toEqual(["3", "8"])
    expect(query.get("page")).toBe("2")
    expect(query.get("pageSize")).toBe("25")
  })

  it("omits blank filters and applies the public API defaults", () => {
    const query = buildPropertyListParams({ keyword: "  ", city: "" })

    expect(query.has("keyword")).toBe(false)
    expect(query.has("city")).toBe(false)
    expect(query.get("sortBy")).toBe("propertyId")
    expect(query.get("page")).toBe("1")
    expect(query.get("pageSize")).toBe("10")
  })

  it("validates price bounds, page limits, sort values, and amenity IDs before fetch", () => {
    expect(() =>
      buildPropertyListParams({ minPrice: "4", maxPrice: "3" }),
    ).toThrow(PublicApiError)
    expect(() => buildPropertyListParams({ pageSize: "51" })).toThrow(
      PublicApiError,
    )
    expect(() =>
      buildPropertyListParams({ sortBy: "nearest" as never }),
    ).toThrow(PublicApiError)
    expect(() => buildPropertyListParams({ amenityIds: ["0"] })).toThrow(
      PublicApiError,
    )
  })

  it("uses only filters supported by property room listing and repeats amenity IDs", () => {
    const query = buildRoomListParams({
      minPrice: "1000000.25",
      roomTypeName: " Studio ",
      amenityIds: ["4", "2"],
      sortBy: "price_asc",
    })

    expect(query.get("minPrice")).toBe("1000000.25")
    expect(query.get("roomTypeName")).toBe("Studio")
    expect(query.getAll("amenityIds")).toEqual(["2", "4"])
    expect(query.has("city")).toBe(false)
    expect(query.get("sortBy")).toBe("price_asc")
  })
})

describe("public browsing API client", () => {
  it("normalizes an API origin and calls the anonymous property listing allow-list", async () => {
    const payload: PublicPage<PublicPropertyCard> = {
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 10,
    }
    const fetchMock = createFetchMock(payload)
    const client = createPublicApiClient("https://api.example.test/", fetchMock)

    await expect(client.listProperties()).resolves.toEqual(payload)

    const [input, init] = fetchMock.mock.calls[0]
    const url = new URL(String(input))
    expect(url.origin).toBe("https://api.example.test")
    expect(url.pathname).toBe("/api/public/properties")
    expect(url.searchParams.get("page")).toBe("1")
    expect(new Headers(init?.headers).has("Authorization")).toBe(false)
  })

  it("does not duplicate an existing /api prefix and forwards abort signals", async () => {
    const fetchMock = createFetchMock({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 10,
    })
    const client = createPublicApiClient(
      "https://api.example.test/api/",
      fetchMock,
    )
    const controller = new AbortController()

    await client.listProperties({}, controller.signal)

    expect(String(fetchMock.mock.calls[0][0])).toContain(
      "https://api.example.test/api/public/properties?",
    )
    expect(fetchMock.mock.calls[0][1]?.signal).toBe(controller.signal)
  })

  it("serializes room filters with repeated amenityIds and the numeric property route ID", async () => {
    const fetchMock = createFetchMock({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 10,
    })
    const client = createPublicApiClient("/api", fetchMock)

    await client.listRooms(42, { amenityIds: ["5", "1"] })

    const url = new URL(String(fetchMock.mock.calls[0][0]), "http://localhost")
    expect(url.pathname).toBe("/api/public/properties/42/rooms")
    expect(url.searchParams.getAll("amenityIds")).toEqual(["1", "5"])
  })

  it("preserves decimal DTO values and empty image, amenity, service, and equipment arrays", async () => {
    const detail: PublicRoomDetail = {
      roomId: 9,
      roomCode: "owner-gated",
      areaSqm: 20.5,
      roomType: { roomTypeId: 4, roomTypeName: "Studio" },
      baseRent: 2850000.75,
      property: {
        propertyId: 42,
        propertyName: "Residence",
        addressText: "Street",
        ward: "Ward",
        district: "District",
        city: "City",
      },
      images: [],
      amenities: [],
      services: [{ serviceId: 2, name: "Internet", price: 175000.25 }],
      equipments: [],
    }
    const fetchMock = createFetchMock(detail)
    const client = createPublicApiClient("/api", fetchMock)

    await expect(client.getRoom(9)).resolves.toEqual(detail)
    expect(detail.baseRent).toBe(2850000.75)
    expect(detail.services[0].price).toBe(175000.25)
    expect(detail.images).toEqual([])
    expect(detail.equipments).toEqual([])
  })

  it("maps 400 validation, generic 404, and server errors without exposing response diagnostics", async () => {
    const validationClient = createPublicApiClient(
      "/api",
      createFetchMock(
        {
          errors: { minPrice: ["Minimum price is invalid"] },
          detail: "secret",
        },
        400,
      ),
    )
    const notFoundClient = createPublicApiClient(
      "/api",
      createFetchMock({ detail: "private room status" }, 404),
    )
    const serverClient = createPublicApiClient(
      "/api",
      createFetchMock({ detail: "database host" }, 503),
    )

    await expect(validationClient.listProperties()).rejects.toMatchObject({
      kind: "validation",
      validationErrors: { minPrice: ["Minimum price is invalid"] },
    })
    await expect(notFoundClient.getProperty(2)).rejects.toMatchObject({
      kind: "not-found",
      message: expect.not.stringContaining("private room status"),
    })
    await expect(serverClient.listProperties()).rejects.toMatchObject({
      kind: "server",
      message: expect.not.stringContaining("database host"),
    })
  })

  it("maps network failures and preserves abort errors", async () => {
    const networkClient = createPublicApiClient(
      "/api",
      vi.fn<typeof fetch>().mockRejectedValue(new TypeError("offline")),
    )
    const abortError = new DOMException("aborted", "AbortError")
    const abortClient = createPublicApiClient(
      "/api",
      vi.fn<typeof fetch>().mockRejectedValue(abortError),
    )

    await expect(networkClient.listProperties()).rejects.toMatchObject({
      kind: "network",
    })
    await expect(abortClient.listProperties()).rejects.toBe(abortError)
  })

  it("does not send a request for invalid IDs or filters", async () => {
    const fetchMock = createFetchMock({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 10,
    })
    const client = createPublicApiClient("/api", fetchMock)

    await expect(client.getProperty(0)).rejects.toMatchObject({
      kind: "validation",
    })
    await expect(
      client.listProperties({ maxPrice: "-1" }),
    ).rejects.toMatchObject({ kind: "validation" })
    expect(fetchMock).not.toHaveBeenCalled()
  })
})

describe("public numeric routes", () => {
  it("builds numeric property and room-list paths and rejects invalid IDs", () => {
    expect(routes.propertyDetail).toBe("/property/:propertyId")
    expect(routes.availableRooms).toBe("/property/:propertyId/rooms")
    expect(propertyDetailPath(42)).toBe("/property/42")
    expect(availableRoomsPath(42)).toBe("/property/42/rooms")
    expect(() => propertyDetailPath(0)).toThrow(RangeError)
    expect(parsePositiveRouteId("12")).toBe(12)
    expect(parsePositiveRouteId(undefined)).toBeNull()
    expect(parsePositiveRouteId("abc")).toBeNull()
    expect(parsePositiveRouteId("0")).toBeNull()
    expect(parsePositiveRouteId("1.5")).toBeNull()
  })
})
