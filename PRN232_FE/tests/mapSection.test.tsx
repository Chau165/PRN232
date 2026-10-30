import { fireEvent, render, screen, waitFor } from "@testing-library/react"
import { MemoryRouter } from "react-router"
import { describe, expect, it, vi } from "vitest"
import type { PublicPropertyAreaCard } from "@/types/publicBrowsing"
import MapSection from "@/components/home/MapSection"

vi.mock("@/components/home/AreaPropertyMap", async () => {
  const { createElement } = await import("react")
  return {
    default: ({
      properties,
      userLocation,
      selectedPropertyId,
      onSelect,
    }: {
      properties: PublicPropertyAreaCard[]
      userLocation: { latitude: number; longitude: number } | null
      selectedPropertyId: number | null
      onSelect: (propertyId: number) => void
    }) =>
      createElement(
        "div",
        {
          "data-testid": "area-map",
          "data-selected-property": selectedPropertyId ?? "",
          "data-property-count": properties.length,
          "data-has-user-location": userLocation ? "true" : "false",
        },
        ...properties.map((property) =>
          createElement(
            "button",
            {
              key: property.propertyId,
              type: "button",
              "aria-label": `Bản đồ: ${property.propertyName}`,
              onClick: () => onSelect(property.propertyId),
            },
            property.propertyName,
          ),
        ),
      ),
  }
})

function property(
  propertyId: number,
  overrides: Partial<PublicPropertyAreaCard> = {},
): PublicPropertyAreaCard {
  return {
    propertyId,
    propertyName: `Residence ${propertyId} từ API`,
    addressText: `${propertyId} Đường Mẫu`,
    ward: "Phường Sáu",
    district: "Quận Gò Vấp",
    city: "TP. Hồ Chí Minh",
    latitude: 10.8231 + propertyId / 100000,
    longitude: 106.7624,
    primaryImageUrl: null,
    availableRoomCount: 3,
    startingPrice: 2850000,
    ...overrides,
  }
}

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  })
}

function stubGeolocation(getCurrentPosition: Geolocation["getCurrentPosition"]) {
  const navigatorWithGeolocation = Object.create(navigator) as Navigator
  Object.defineProperty(navigatorWithGeolocation, "geolocation", {
    value: { getCurrentPosition },
  })
  vi.stubGlobal("navigator", navigatorWithGeolocation)
}

function grantLocationPermission() {
  stubGeolocation((success) => {
    success({
      coords: { latitude: 10.8231, longitude: 106.7624 },
    } as GeolocationPosition)
  })
}

function renderMapSection() {
  return render(
    <MemoryRouter>
      <MapSection />
    </MemoryRouter>,
  )
}

describe("public area map section", () => {
  it("loads district properties and keeps marker selection synchronized", async () => {
    grantLocationPermission()
    const properties = [property(42), property(43)]
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(
        jsonResponse({
          address: {
            city_district: "Quận Gò Vấp",
            suburb: "Phường Sáu",
            city: "TP. Hồ Chí Minh",
          },
        }),
      )
      .mockResolvedValueOnce(jsonResponse(properties))
    vi.stubGlobal("fetch", fetchMock)

    renderMapSection()
    fireEvent.click(screen.getByRole("button", { name: "Tìm khu trọ quanh tôi" }))

    expect(
      await screen.findByRole("heading", { name: "Khu trọ tại Quận Gò Vấp" }),
    ).toBeTruthy()
    expect((await screen.findByTestId("area-map")).getAttribute("data-property-count")).toBe("2")
    expect(screen.getByTestId("area-map").getAttribute("data-has-user-location")).toBe("true")
    expect(screen.queryByRole("heading", { name: "Danh sách khu trọ" })).toBeNull()
    const areaRequest = new URL(String(fetchMock.mock.calls[1][0]), window.location.origin)
    expect(areaRequest.pathname).toBe("/api/public/properties/by-area")
    expect(areaRequest.searchParams.get("district")).toBe("Quận Gò Vấp")
    expect(areaRequest.searchParams.get("city")).toBe("TP. Hồ Chí Minh")
    expect(areaRequest.searchParams.has("radius")).toBe(false)

    fireEvent.click(screen.getByRole("button", { name: "Bản đồ: Residence 42 từ API" }))
    expect((await screen.findByTestId("area-map")).getAttribute("data-selected-property")).toBe("42")

    fireEvent.click(screen.getByRole("button", { name: "Bản đồ: Residence 43 từ API" }))
    expect((await screen.findByTestId("area-map")).getAttribute("data-selected-property")).toBe("43")
  })

  it("shows a distinct loading state and a permission-denied state", async () => {
    let rejectLocation: PositionErrorCallback | null | undefined
    stubGeolocation((_success, error) => {
      rejectLocation = error
    })
    const fetchMock = vi.fn<typeof fetch>()
    vi.stubGlobal("fetch", fetchMock)

    renderMapSection()
    expect((await screen.findByTestId("area-map")).getAttribute("data-has-user-location")).toBe("false")
    fireEvent.click(screen.getByRole("button", { name: "Tìm khu trọ quanh tôi" }))
    expect(screen.getByRole("status").textContent?.toLowerCase()).toContain("vị trí")

    rejectLocation?.({ code: 1, message: "denied" } as GeolocationPositionError)

    expect((await screen.findByRole("alert")).textContent).toContain("quyền vị trí")
    expect(screen.getByTestId("area-map").getAttribute("data-has-user-location")).toBe("false")
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it("shows reverse-geocoding errors and keeps the location marker visible", async () => {
    grantLocationPermission()
    vi.stubGlobal("fetch", vi.fn<typeof fetch>().mockRejectedValue(new TypeError("Failed to fetch")))

    renderMapSection()
    fireEvent.click(screen.getByRole("button", { name: "Tìm khu trọ quanh tôi" }))

    expect((await screen.findByRole("alert")).textContent?.toLowerCase()).toContain(
      "không thể kết nối dịch vụ xác định khu vực",
    )
    expect((await screen.findByTestId("area-map")).getAttribute("data-has-user-location")).toBe("true")
  })

  it("keeps the map on the user location without requesting city-wide properties when district data is missing", async () => {
    grantLocationPermission()
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(
      jsonResponse({ address: { city: "TP. Hồ Chí Minh" } }),
    )
    vi.stubGlobal("fetch", fetchMock)

    renderMapSection()
    fireEvent.click(screen.getByRole("button", { name: "Tìm khu trọ quanh tôi" }))

    expect((await screen.findByRole("alert")).textContent?.toLowerCase()).toContain(
      "chỉ xác định được thành phố",
    )
    expect(screen.getByTestId("area-map").getAttribute("data-has-user-location")).toBe("true")
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it("shows a no-property state for a successful empty area response", async () => {
    grantLocationPermission()
    vi.stubGlobal(
      "fetch",
      vi.fn<typeof fetch>()
        .mockResolvedValueOnce(jsonResponse({
          address: {
            city_district: "Quận Gò Vấp",
            suburb: "Phường 10",
            city: "TP.HCM",
          },
        }))
        .mockResolvedValueOnce(jsonResponse([])),
    )

    renderMapSection()
    fireEvent.click(screen.getByRole("button", { name: "Tìm khu trọ quanh tôi" }))

    expect(await screen.findByText(/Chưa có khu trọ đang cho thuê tại/)).toBeTruthy()
    expect(screen.getByTestId("area-map").getAttribute("data-property-count")).toBe("0")
    expect(screen.queryByText(/đang hiển thị toàn/)).toBeNull()
  })

  it("shows an API error separately from location and geocoding errors", async () => {
    grantLocationPermission()
    vi.stubGlobal(
      "fetch",
      vi.fn<typeof fetch>()
        .mockResolvedValueOnce(jsonResponse({
          address: {
            city_district: "Quận Gò Vấp",
            city: "TP.HCM",
          },
        }))
        .mockResolvedValueOnce(jsonResponse({ title: "Unavailable" }, 503)),
    )

    renderMapSection()
    fireEvent.click(screen.getByRole("button", { name: "Tìm khu trọ quanh tôi" }))

    await waitFor(() =>
      expect(screen.getByRole("alert").textContent?.toLowerCase()).toContain(
        "không tải được danh sách",
      ),
    )
    expect(screen.getByTestId("area-map")).toBeTruthy()
  })
})
