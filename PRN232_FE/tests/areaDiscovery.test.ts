import { describe, expect, it, vi } from "vitest"
import {
  AreaDiscoveryError,
  buildGoogleMapsDirectionsUrl,
  discoverCurrentArea,
} from "@/utils/areaDiscovery"
import type {
  PublicPropertyAreaCard,
  PublicPropertyAreaQuery,
} from "@/api/publicBrowsingApi"

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  })
}

function locationPermission(
  errorCode?: number,
): Geolocation {
  return {
    getCurrentPosition: vi.fn((success, error) => {
      if (errorCode !== undefined) {
        error?.({ code: errorCode, message: "location unavailable" } as GeolocationPositionError)
        return
      }

      success({
        coords: { latitude: 10.8231, longitude: 106.7624 },
      } as GeolocationPosition)
    }),
  } as unknown as Geolocation
}

function property(overrides: Partial<PublicPropertyAreaCard> = {}): PublicPropertyAreaCard {
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

describe("current-area property discovery", () => {
  it("uses Photon reverse geocoding and resolves its district and city fields", async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(
      jsonResponse({
        features: [{ properties: { district: "Gò Vấp", city: "Ho Chi Minh City" } }],
      }),
    )
    const listPropertiesByArea = vi
      .fn<(query: PublicPropertyAreaQuery) => Promise<PublicPropertyAreaCard[]>>()
      .mockResolvedValue([property()])

    const result = await discoverCurrentArea({
      geolocation: locationPermission(),
      fetchImplementation: fetchMock,
      listPropertiesByArea,
    })

    expect(result.area).toEqual({ district: "Gò Vấp", city: "Ho Chi Minh City" })
    expect(listPropertiesByArea).toHaveBeenCalledWith({
      district: "Gò Vấp",
      city: "Ho Chi Minh City",
    }, undefined)
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

    expect(onLocation).toHaveBeenCalledWith({ latitude: 10.8231, longitude: 106.7624 })
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
      .fn<(query: PublicPropertyAreaQuery) => Promise<PublicPropertyAreaCard[]>>()
      .mockResolvedValue(properties)

    const result = await discoverCurrentArea({
      geolocation: locationPermission(),
      fetchImplementation: fetchMock,
      listPropertiesByArea,
      onProgress: (step) => steps.push(step),
    })

    expect(steps).toEqual(["location", "reverse-geocode", "properties"])
    expect(result.coordinates).toEqual({ latitude: 10.8231, longitude: 106.7624 })
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
    }, undefined)
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
      .fn<(query: PublicPropertyAreaQuery) => Promise<PublicPropertyAreaCard[]>>()
      .mockResolvedValueOnce([])

    const result = await discoverCurrentArea({
      geolocation: locationPermission(),
      fetchImplementation: fetchMock,
      listPropertiesByArea,
    })

    expect(listPropertiesByArea).toHaveBeenNthCalledWith(1, {
      district: "Quận Gò Vấp",
      city: "Thành phố Hồ Chí Minh",
    }, undefined)
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
    ).rejects.toMatchObject<Partial<AreaDiscoveryError>>({ kind: "area-not-specific" })
    expect(listPropertiesByArea).not.toHaveBeenCalled()
  })

  it("uses the ward when the geocoder cannot resolve a district", async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(
      jsonResponse({ address: { suburb: "Phường Sáu", city: "TP.HCM" } }),
    )
    const listPropertiesByArea = vi
      .fn<(query: PublicPropertyAreaQuery) => Promise<PublicPropertyAreaCard[]>>()
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
    }, undefined)
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
    ).rejects.toMatchObject<Partial<AreaDiscoveryError>>({ kind: "permission-denied" })
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
