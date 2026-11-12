import { describe, expect, it, vi } from "vitest"
import {
  AreaDiscoveryError,
  buildGoogleMapsDirectionsUrl,
  discoverCurrentArea,
  discoverEnteredArea,
} from "@/utils/areaDiscovery"
import type {
  PublicPropertyAreaCard,
  PublicPropertyAreaQuery,
} from "@/types/publicBrowsing"

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  })
}

function locationPermission(
  errorCode?: number): Geolocation {
  return {
    getCurrentPosition: vi.fn((success, error) => {
      if (errorCode !== undefined) {
        error?.({ code: errorCode, message: "location unavailable",
        } as GeolocationPositionError)
        return
      }

      success({
        coords: { latitude: 10.8231, longitude: 106.7624 },
      } as GeolocationPosition)
    }),
  } as unknown as Geolocation
}

function property(overrides: Partial<PublicPropertyAreaCard> = {},
): PublicPropertyAreaCard {
  return {
    propertyId: 42,
    propertyName: "Residence từ API",
    addressText: "12 Đường Mẫu",
    ward: "Phường Sáu",
    district: "Quận Gò Vấp",
    city: "TP. Hồ Chí Minh",
    latitude: 10.8231,
    longitude: 106.7624,
    primaryImageUrl: null,
    availableRoomCount: 3,
    startingPrice: 2850000,
    ...overrides,
  }
}

describe("typed-area property discovery", () => {
  it("finds Quận 7 properties even when Photon resolves the input to another province", async () => {
    // Captured Photon response for q=Quận 7: a street in Gia Lai, not the requested district.
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse({
      features: [{
        geometry: { coordinates: [109.1479799, 13.7874638] },
        properties: {
          name: "Lac Long Quan road", type: "street", osm_value: "trunk",
          district: "North Quy Nhon", city: "Diêu Trì", state: "Gia Lai province",
        },
      }],
    }))
    const expectedProperty = property({ district: "Khu vực Quận 7 cũ", latitude: 10.738, longitude: 106.728 })
    const listPropertiesByArea = vi.fn(async (query: PublicPropertyAreaQuery) =>
      query.district === "Quận 7" ? [expectedProperty] : [],
    )

    const result = await discoverEnteredArea("Quận 7", {
      fetchImplementation: fetchMock, listPropertiesByArea,
    })

    expect(result.properties).toEqual([expectedProperty])
    expect(result.area.district).toBe("Quận 7")
    expect(result.coordinates).toEqual({ latitude: 10.738, longitude: 106.728 })
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it.each([
    ["Quận Gò Vấp", { district: "Quận Gò Vấp" }],
    ["quan 7", { district: "quan 7" }],
    ["Phường Tân Thuận", { ward: "Phường Tân Thuận" }],
    ["Thành phố Hồ Chí Minh", { city: "Thành phố Hồ Chí Minh" }],
    ["Quận 7, TP.HCM", { district: "Quận 7", city: "TP.HCM" }],
    ["Phường Tân Thuận, Quận 7, TP.HCM", { ward: "Phường Tân Thuận", district: "Quận 7", city: "TP.HCM" }],
  ])("looks up explicit administrative input %s without external geocoding", async (input, query) => {
    const fetchMock = vi.fn<typeof fetch>().mockRejectedValue(new Error("offline"))
    const listPropertiesByArea = vi.fn().mockResolvedValue([property()])
    const onLocation = vi.fn()
    const controller = new AbortController()
    const result = await discoverEnteredArea(input, {
      fetchImplementation: fetchMock, listPropertiesByArea, onLocation, signal: controller.signal,
    })
    expect(listPropertiesByArea).toHaveBeenCalledWith(query, controller.signal)
    expect(result.properties).toEqual([property()])
    expect(onLocation).toHaveBeenCalledWith(result.coordinates)
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it("keeps an explicit district filter when no matching properties exist", async () => {
    const listPropertiesByArea = vi.fn().mockResolvedValue([])
    const result = await discoverEnteredArea("Quận 7", {
      fetchImplementation: vi.fn().mockResolvedValue(jsonResponse({
        features: [{ geometry: { coordinates: [109.14, 13.78] },
          properties: { district: "North Quy Nhon", city: "Diêu Trì" } }],
      })),
      listPropertiesByArea,
    })
    expect(result.properties).toEqual([])
    expect(result.area).toEqual({ district: "Quận 7" })
    expect(listPropertiesByArea).toHaveBeenCalledExactlyOnceWith({ district: "Quận 7" }, undefined)
  })

  it("preserves unlocated properties without inventing property coordinates", async () => {
    const unlocated = property({ latitude: 0, longitude: 0 })
    const result = await discoverEnteredArea("Quận 7", {
      fetchImplementation: vi.fn().mockResolvedValue(jsonResponse({
        features: [{ geometry: { coordinates: [106.72, 10.73] }, properties: { district: "Quận 7" } }],
      })),
      listPropertiesByArea: vi.fn().mockResolvedValue([unlocated]),
    })
    expect(result.properties).toEqual([unlocated])
    expect(result.coordinates).toEqual({ latitude: 10.73, longitude: 106.72 })
  })

  it("reports local API failures instead of silently switching to an unrelated geocoded area", async () => {
    const fetchMock = vi.fn<typeof fetch>()
    await expect(discoverEnteredArea("Quận 7", {
      fetchImplementation: fetchMock,
      listPropertiesByArea: vi.fn().mockRejectedValue(new Error("unavailable")),
    })).rejects.toMatchObject({ kind: "area-api" })
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it("does not publish local results after the request is aborted", async () => {
    const controller = new AbortController()
    const onLocation = vi.fn()
    await expect(discoverEnteredArea("Quận 7", {
      fetchImplementation: vi.fn(), signal: controller.signal, onLocation,
      listPropertiesByArea: vi.fn(async () => {
        controller.abort()
        return [property()]
      }),
    })).rejects.toMatchObject({ name: "AbortError" })
    expect(onLocation).not.toHaveBeenCalled()
  })

  it("supports city searches and encodes Vietnamese input", async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(
      jsonResponse({
        features: [
          {
            geometry: { coordinates: [105.85, 21.03] },

            properties: { name: "Hà Nội", osm_value: "city" },
          },
        ],
      }),
    )

    const listPropertiesByArea = vi.fn().mockResolvedValue([])

    const result = await discoverEnteredArea("  Hà Nội  ", {
      fetchImplementation: fetchMock,
      listPropertiesByArea,
    })

    expect(result.matchedLevel).toBe("city")

    expect(result.coordinates).toEqual({ latitude: 21.03, longitude: 105.85 })

    expect(listPropertiesByArea.mock.calls[0][0]).toEqual({ city: "Hà Nội" })

    const url = new URL(String(fetchMock.mock.calls[0][0]))

    expect(url.searchParams.get("q")).toBe("Hà Nội")

    expect(url.searchParams.get("countrycode")).toBe("VN")
  })

  it("does not request properties when a place cannot be found", async () => {
    const listPropertiesByArea = vi.fn()

    await expect(
      discoverEnteredArea("unknown", {
        fetchImplementation: vi
          .fn()
          .mockResolvedValue(jsonResponse({ features: [] })),

        listPropertiesByArea,
      }),
    ).rejects.toMatchObject({ kind: "geocode" })

    expect(listPropertiesByArea).not.toHaveBeenCalled()
  })

  it("rejects invalid coordinates and failed geocoding requests", async () => {
    for (const response of [
      jsonResponse({ features: [{ geometry: { coordinates: [200, 100] } }] }),
      jsonResponse({}, 503),
    ]) {
      const listPropertiesByArea = vi.fn()

      await expect(
        discoverEnteredArea("Hà Nội", {
          fetchImplementation: vi.fn().mockResolvedValue(response),
          listPropertiesByArea,
        }),
      ).rejects.toMatchObject({ kind: "geocode" })

      expect(listPropertiesByArea).not.toHaveBeenCalled()
    }
  })
})

describe("current-area property discovery", () => {
  it("uses Photon reverse geocoding and resolves its district and city fields", async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(
      jsonResponse({
        features: [{ properties: { district: "Gò Vấp", city: "Ho Chi Minh City" } },
        ],
      }),
    )
    const listPropertiesByArea = vi
      .fn<(query: PublicPropertyAreaQuery,
      ) => Promise<PublicPropertyAreaCard[]>>()
      .mockResolvedValue([property()])

    const result = await discoverCurrentArea({
      geolocation: locationPermission(),
      fetchImplementation: fetchMock,
      listPropertiesByArea,
    })

    expect(result.area).toEqual({ district: "Gò Vấp", city: "Ho Chi Minh City",
    })
    expect(listPropertiesByArea).toHaveBeenCalledWith({
      district: "Gò Vấp",
      city: "Ho Chi Minh City",
    }, undefined,
    )
    const reverseUrl = new URL(String(fetchMock.mock.calls[0][0]))
    expect(reverseUrl.hostname).toBe("photon.komoot.io")
    expect(reverseUrl.searchParams.has("format")).toBe(false)
  })

  it("keeps the user location visible on the map when reverse geocoding fails", async () => {
    const onLocation = vi.fn()
    await expect(
      discoverCurrentArea({
        geolocation: locationPermission(),
        fetchImplementation: vi.fn<typeof fetch>().mockRejectedValue(new TypeError("Failed to fetch")),
        listPropertiesByArea: vi.fn(),
        onLocation,
      }),
    ).rejects.toMatchObject({ kind: "reverse-geocode" })

    expect(onLocation).toHaveBeenCalledWith({ latitude: 10.8231, longitude: 106.7624,
    })
  })

  it("requests location, reverse-geocodes it, then loads the whole district", async () => {
    const steps: string[] = []
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(
      jsonResponse({
        address: {
          city_district: "Quận Gò Vấp",
          suburb: "Phường Sáu",
          city: "TP. Hồ Chí Minh",
        },
      }),
    )
    const properties = [property()]
    const listPropertiesByArea = vi
      .fn<(query: PublicPropertyAreaQuery,
      ) => Promise<PublicPropertyAreaCard[]>>()
      .mockResolvedValue(properties)

    const result = await discoverCurrentArea({
      geolocation: locationPermission(),
      fetchImplementation: fetchMock,
      listPropertiesByArea,
      onProgress: (step) => steps.push(step),
    })

    expect(steps).toEqual(["location", "reverse-geocode", "properties"])
    expect(result.coordinates).toEqual({ latitude: 10.8231, longitude: 106.7624,
    })
    expect(result.area).toEqual({
      district: "Quận Gò Vấp",
      ward: "Phường Sáu",
      city: "TP. Hồ Chí Minh",
    })
    expect(result.matchedLevel).toBe("district")
    expect(result.properties).toEqual(properties)
    expect(listPropertiesByArea).toHaveBeenCalledWith({
      district: "Quận Gò Vấp",
      city: "TP. Hồ Chí Minh",
    }, undefined,
    )
    const reverseUrl = new URL(String(fetchMock.mock.calls[0][0]))
    expect(reverseUrl.hostname).toBe("photon.komoot.io")
    expect(reverseUrl.searchParams.get("lat")).toBe("10.8231")
    expect(reverseUrl.searchParams.get("lon")).toBe("106.7624")
    expect(reverseUrl.searchParams.has("format")).toBe(false)
  })

  it("does not broaden an empty district result to the whole city", async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(
      jsonResponse({
        address: {
          city_district: "Quận Gò Vấp",
          suburb: "Phường 10",
          city: "Thành phố Hồ Chí Minh",
        },
      }),
    )
    const listPropertiesByArea = vi
      .fn<(query: PublicPropertyAreaQuery,
      ) => Promise<PublicPropertyAreaCard[]>>()
      .mockResolvedValueOnce([])

    const result = await discoverCurrentArea({
      geolocation: locationPermission(),
      fetchImplementation: fetchMock,
      listPropertiesByArea,
    })

    expect(listPropertiesByArea).toHaveBeenNthCalledWith(1, {
      district: "Quận Gò Vấp",
      city: "Thành phố Hồ Chí Minh",
    }, undefined,
    )
    expect(listPropertiesByArea).toHaveBeenCalledTimes(1)
    expect(result.matchedLevel).toBe("district")
    expect(result.properties).toEqual([])
  })

  it("requires ward or district detail instead of silently loading the whole city", async () => {
    const listPropertiesByArea = vi.fn()

    await expect(
      discoverCurrentArea({
        geolocation: locationPermission(),
        fetchImplementation: vi.fn<typeof fetch>().mockResolvedValue(
          jsonResponse({ address: { city: "TP. Hồ Chí Minh" } }),
        ),
        listPropertiesByArea,
      }),
    ).rejects.toMatchObject<Partial<AreaDiscoveryError>>({ kind: "area-not-specific",
    })
    expect(listPropertiesByArea).not.toHaveBeenCalled()
  })

  it("uses the ward when the geocoder cannot resolve a district", async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(
      jsonResponse({ address: { suburb: "Phường Sáu", city: "TP.HCM" } }),
    )
    const listPropertiesByArea = vi
      .fn<(query: PublicPropertyAreaQuery,
      ) => Promise<PublicPropertyAreaCard[]>>()
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce([property()])

    const result = await discoverCurrentArea({
      geolocation: locationPermission(),
      fetchImplementation: fetchMock,
      listPropertiesByArea,
    })

    expect(listPropertiesByArea).toHaveBeenNthCalledWith(1, {
      ward: "Phường Sáu",
      city: "TP.HCM",
    }, undefined,
    )
    expect(listPropertiesByArea).toHaveBeenCalledTimes(1)
    expect(result.matchedLevel).toBe("ward")
    expect(result.properties).toEqual([])
  })

  it("reports denied location permission separately", async () => {
    await expect(
      discoverCurrentArea({
        geolocation: locationPermission(1),
        fetchImplementation: vi.fn<typeof fetch>(),
        listPropertiesByArea: vi.fn(),
      }),
    ).rejects.toMatchObject<Partial<AreaDiscoveryError>>({ kind: "permission-denied",
    })
  })

  it("reports missing location and reverse-geocoding failures", async () => {
    await expect(
      discoverCurrentArea({
        geolocation: null,
        fetchImplementation: vi.fn<typeof fetch>(),
        listPropertiesByArea: vi.fn(),
      }),
    ).rejects.toMatchObject({ kind: "location-unavailable" })

    await expect(
      discoverCurrentArea({
        geolocation: locationPermission(),
        fetchImplementation: vi.fn<typeof fetch>().mockResolvedValue(
          jsonResponse({ error: "service unavailable" }, 503),
        ),
        listPropertiesByArea: vi.fn(),
      }),
    ).rejects.toMatchObject({ kind: "reverse-geocode" })
  })

  it("reports API failures and rejects locations without an area name", async () => {
    const cityFetch = vi.fn<typeof fetch>().mockResolvedValue(
      jsonResponse({
        address: { city_district: "Quận Gò Vấp", city: "TP.HCM" },
      }),
    )
    await expect(
      discoverCurrentArea({
        geolocation: locationPermission(),
        fetchImplementation: cityFetch,
        listPropertiesByArea: vi.fn().mockRejectedValue(new Error("API unavailable")),
      }),
    ).rejects.toMatchObject({ kind: "area-api" })

    await expect(
      discoverCurrentArea({
        geolocation: locationPermission(),
        fetchImplementation: vi.fn<typeof fetch>().mockResolvedValue(
          jsonResponse({ address: { road: "Unknown Road" } }),
        ),
        listPropertiesByArea: vi.fn(),
      }),
    ).rejects.toMatchObject({ kind: "reverse-geocode" })
  })
})

describe("Google Maps directions", () => {
  it("prefers valid coordinates and falls back to the complete address", () => {
    expect(buildGoogleMapsDirectionsUrl(property())).toBe(
      "https://www.google.com/maps/dir/?api=1&destination=10.8231%2C106.7624",
    )
    expect(
      buildGoogleMapsDirectionsUrl(
        property({ latitude: Number.NaN, longitude: Number.NaN }),
      ),
    ).toBe(
      "https://www.google.com/maps/dir/?api=1&destination=12+%C4%90%C6%B0%E1%BB%9Dng+M%E1%BA%ABu%2C+Ph%C6%B0%E1%BB%9Dng+S%C3%A1u%2C+Qu%E1%BA%ADn+G%C3%B2+V%E1%BA%A5p%2C+TP.+H%E1%BB%93+Ch%C3%AD+Minh",
    )
  })
})
