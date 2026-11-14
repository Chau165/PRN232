import { fireEvent, render, screen } from "@testing-library/react"
import { beforeEach, describe, expect, it, vi } from "vitest"
import { MemoryRouter, Route, Routes } from "react-router"
import { AuthProvider } from "@/auth/AuthContext"
import { ProtectedRoute } from "@/components/auth/ProtectedRoute"
import ManagementPropertyRoomTypesPage from "@/pages/ManagementPropertyRoomTypesPage/ManagementPropertyRoomTypesPage"

const admin = {
  userId: 7,
  fullName: "Admin Thật",
  email: "admin@example.test",
  phoneNumber: "0900000000",
  roleId: 3,
  roleName: "Admin",
  isActive: true,
}

const property = {
  propertyId: 21,
  ownerId: 7,
  ownerName: admin.fullName,
  propertyName: "Nhà trọ Bình An",
  addressText: "12 Đường số 5",
  ward: "Phường 1",
  district: "Thủ Đức",
  city: "TP. Hồ Chí Minh",
  latitude: 10.85,
  longitude: 106.77,
  primaryImageUrl: null,
  description: "Khu trọ yên tĩnh",
  roomCount: 0,
}

const catalog = [
  { roomTypeId: 4, typeName: "Studio", price: 0 },
  { roomTypeId: 5, typeName: "Deluxe", price: 0 },
]

const configurations = [
  {
    propertyId: property.propertyId,
    roomTypeId: 4,
    roomTypeName: "Studio",
    monthlyRent: 3_000_000,
    isActive: true,
    totalRoomCount: 2,
    availableRoomCount: 1,
  },
]

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  })
}

function adminSession() {
  window.sessionStorage.setItem(
    "troviet-auth-session-v1",
    JSON.stringify({
      accessToken: "admin.backend.jwt",
      expiresAtUtc: new Date(Date.now() + 60_000).toISOString(),
    }),
  )
}

type ApiHandlers = {
  properties?: () => Response | Promise<Response>
  catalog?: () => Response | Promise<Response>
  configurations?: () => Response | Promise<Response>
  createCatalog?: (body: unknown) => Response | Promise<Response>
  createConfiguration?: (body: unknown) => Response | Promise<Response>
}

function stubApi(handlers: ApiHandlers = {}) {
  const calls: Array<{ url: string; init?: RequestInit }> = []

  vi.stubGlobal(
    "fetch",
    vi.fn<typeof fetch>(async (input, init) => {
      const url = String(input)
      const method = init?.method ?? "GET"
      calls.push({ url, init })

      if (url.endsWith("/users/me")) return jsonResponse(admin)
      if (url.endsWith("/properties/21/room-types") && method === "POST") {
        const body = init?.body ? JSON.parse(String(init.body)) : null
        return handlers.createConfiguration
          ? handlers.createConfiguration(body)
          : jsonResponse(
              {
                ...configurations[0],
                roomTypeId: 9,
                roomTypeName: "Phòng Deluxe",
                monthlyRent: 4_500_000,
                totalRoomCount: 0,
                availableRoomCount: 0,
              },
              201,
            )
      }
      if (url.endsWith("/properties/21/room-types")) {
        return handlers.configurations
          ? handlers.configurations()
          : jsonResponse(configurations)
      }
      if (url.endsWith("/room-types") && method === "POST") {
        const body = init?.body ? JSON.parse(String(init.body)) : null
        return handlers.createCatalog
          ? handlers.createCatalog(body)
          : jsonResponse(
              { roomTypeId: 9, typeName: "Phòng Deluxe", price: 0 },
              201,
            )
      }
      if (url.endsWith("/room-types")) {
        return handlers.catalog ? handlers.catalog() : jsonResponse(catalog)
      }
      if (url.endsWith("/properties")) {
        return handlers.properties
          ? handlers.properties()
          : jsonResponse([property])
      }
      return jsonResponse({ title: "Not Found" }, 404)
    }),
  )

  return calls
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={["/management/properties/21/room-types"]}>
      <AuthProvider>
        <Routes>
          <Route
            path="/management/properties/:propertyId/room-types"
            element={
              <ProtectedRoute roles={["ADMIN", "MANAGER"]}>
                <ManagementPropertyRoomTypesPage />
              </ProtectedRoute>
            }
          />
        </Routes>
      </AuthProvider>
    </MemoryRouter>,
  )
}

beforeEach(() => {
  window.sessionStorage.clear()
  vi.restoreAllMocks()
})

describe("management property room types", () => {
  it("keeps property and catalog data when the configurations request fails", async () => {
    adminSession()
    let configurationAttempts = 0
    stubApi({
      configurations: () => {
        configurationAttempts += 1
        return configurationAttempts === 1
          ? jsonResponse({ title: "Cấu hình đang lỗi" }, 500)
          : jsonResponse([])
      },
    })

    renderPage()

    expect(
      await screen.findByText(/Cấu hình riêng cho Nhà trọ Bình An/),
    ).toBeTruthy()
    expect(await screen.findByRole("option", { name: "Deluxe" })).toBeTruthy()
    expect(
      await screen.findByText(
        /Không tải được cấu hình loại phòng: Cấu hình đang lỗi/,
      ),
    ).toBeTruthy()
    expect(
      screen.queryByText("Chưa có loại phòng nào được cấu hình."),
    ).toBeNull()

    fireEvent.click(
      screen.getByRole("button", { name: "Thử tải lại cấu hình" }),
    )

    expect(
      await screen.findByText("Chưa có loại phòng nào được cấu hình."),
    ).toBeTruthy()
  })

  it("keeps configurations when the catalog request fails", async () => {
    adminSession()
    stubApi({
      catalog: () => jsonResponse({ title: "Danh mục đang lỗi" }, 500),
    })

    renderPage()

    expect(
      await screen.findByText(
        /Không tải được danh mục loại phòng: Danh mục đang lỗi/,
      ),
    ).toBeTruthy()
    expect(
      await screen.findByRole("heading", { name: "Studio" }),
    ).toBeTruthy()
    expect(screen.queryByRole("option", { name: "Studio" })).toBeNull()
  })

  it("keeps catalog and configurations when the property request fails", async () => {
    adminSession()
    stubApi({
      properties: () => jsonResponse({ title: "Khu trọ đang lỗi" }, 500),
    })

    renderPage()

    expect(
      await screen.findByText(
        /Không tải được thông tin khu trọ: Khu trọ đang lỗi/,
      ),
    ).toBeTruthy()
    expect(await screen.findByRole("option", { name: "Deluxe" })).toBeTruthy()
    expect(
      await screen.findByRole("heading", { name: "Studio" }),
    ).toBeTruthy()
  })

  it("shows the empty state only after an empty successful configuration response", async () => {
    adminSession()
    stubApi({ configurations: () => jsonResponse([]) })

    renderPage()

    expect(
      await screen.findByText("Chưa có loại phòng nào được cấu hình."),
    ).toBeTruthy()
  })

  it("renders configured property prices", async () => {
    adminSession()
    stubApi()

    renderPage()

    expect(
      await screen.findByRole("heading", { name: "Studio" }),
    ).toBeTruthy()
    expect(screen.getByText(/3\.000\.000/)).toBeTruthy()
  })

  it("creates a catalog type and configures it only for the current property", async () => {
    adminSession()
    const calls = stubApi({
      createCatalog: (body) => {
        expect(body).toEqual({ typeName: "Phòng Deluxe", price: 0 })
        return jsonResponse(
          { roomTypeId: 9, typeName: "Phòng Deluxe", price: 0 },
          201,
        )
      },
      createConfiguration: (body) => {
        expect(body).toEqual({ roomTypeId: 9, monthlyRent: 4_500_000 })
        return jsonResponse(
          {
            propertyId: property.propertyId,
            roomTypeId: 9,
            roomTypeName: "Phòng Deluxe",
            monthlyRent: 4_500_000,
            isActive: true,
            totalRoomCount: 0,
            availableRoomCount: 0,
          },
          201,
        )
      },
    })

    renderPage()

    await screen.findByRole("option", { name: "Deluxe" })
    fireEvent.change(screen.getByLabelText("Tên loại phòng mới"), {
      target: { value: "  Phòng   Deluxe  " },
    })
    fireEvent.change(screen.getByLabelText("Giá thuê loại phòng mới"), {
      target: { value: "4500000" },
    })
    fireEvent.click(screen.getByRole("button", { name: "Tạo và cấu hình" }))

    expect(
      await screen.findByText(
        /Đã tạo loại phòng "Phòng Deluxe" và cấu hình giá cho khu trọ/,
      ),
    ).toBeTruthy()

    const postCalls = calls.filter((call) => call.init?.method === "POST")
    expect(
      postCalls.some((call) => call.url.endsWith("/room-types")),
    ).toBe(true)
    expect(
      postCalls.some((call) =>
        call.url.endsWith("/properties/21/room-types"),
      ),
    ).toBe(true)
  })

  it("reports partial success when the catalog type is created but configuration fails", async () => {
    adminSession()
    stubApi({
      createCatalog: () =>
        jsonResponse(
          { roomTypeId: 9, typeName: "Phòng Deluxe", price: 0 },
          201,
        ),
      createConfiguration: () =>
        jsonResponse(
          {
            title: "Room type already configured",
            detail: "This room type is already configured for the property.",
          },
          409,
        ),
    })

    renderPage()

    await screen.findByRole("option", { name: "Deluxe" })
    fireEvent.change(screen.getByLabelText("Tên loại phòng mới"), {
      target: { value: "Phòng Deluxe" },
    })
    fireEvent.change(screen.getByLabelText("Giá thuê loại phòng mới"), {
      target: { value: "4500000" },
    })
    fireEvent.click(screen.getByRole("button", { name: "Tạo và cấu hình" }))

    expect(
      await screen.findByText(
        /Đã tạo loại phòng "Phòng Deluxe" trong danh mục, nhưng bước gắn giá thuê cho khu trọ thất bại/,
      ),
    ).toBeTruthy()
    expect(
      screen.queryByText(
        /Đã tạo loại phòng "Phòng Deluxe" và cấu hình giá cho khu trọ/,
      ),
    ).toBeNull()
  })

  it("rejects a duplicate catalog name before calling the API", async () => {
    adminSession()
    const calls = stubApi()

    renderPage()

    await screen.findByRole("option", { name: "Deluxe" })
    fireEvent.change(screen.getByLabelText("Tên loại phòng mới"), {
      target: { value: "  studio  " },
    })
    fireEvent.change(screen.getByLabelText("Giá thuê loại phòng mới"), {
      target: { value: "3000000" },
    })
    fireEvent.click(screen.getByRole("button", { name: "Tạo và cấu hình" }))

    expect(
      await screen.findByText(/đã có trong danh mục/),
    ).toBeTruthy()
    expect(
      calls.some(
        (call) => call.init?.method === "POST" && call.url.endsWith("/room-types"),
      ),
    ).toBe(false)
  })

  it("uses the entered property rent instead of the catalog price", async () => {
    adminSession()
    let configurationBody: unknown = null
    stubApi({
      catalog: () =>
        jsonResponse([
          { roomTypeId: 5, typeName: "Deluxe", price: 7_000_000 },
        ]),
      createConfiguration: (body) => {
        configurationBody = body
        return jsonResponse(
          {
            propertyId: property.propertyId,
            roomTypeId: 5,
            roomTypeName: "Deluxe",
            monthlyRent: 3_200_000,
            isActive: true,
            totalRoomCount: 0,
            availableRoomCount: 0,
          },
          201,
        )
      },
    })

    renderPage()

    await screen.findByRole("option", { name: "Deluxe" })
    fireEvent.change(screen.getByLabelText("Danh mục loại phòng"), {
      target: { value: "5" },
    })
    fireEvent.change(screen.getByLabelText("Giá thuê mới"), {
      target: { value: "3200000" },
    })
    fireEvent.click(screen.getByRole("button", { name: "Thêm cấu hình" }))

    expect(
      await screen.findByText("Đã thêm loại phòng cho khu trọ."),
    ).toBeTruthy()
    expect(configurationBody).toEqual({
      roomTypeId: 5,
      monthlyRent: 3_200_000,
    })
  })

  it("shows a duplicate configuration error and does not report success", async () => {
    adminSession()
    stubApi({
      createConfiguration: () =>
        jsonResponse(
          {
            title: "Room type already configured",
            detail: "Loại phòng này đã được cấu hình cho khu trọ.",
          },
          409,
        ),
    })

    renderPage()

    await screen.findByRole("option", { name: "Deluxe" })
    fireEvent.change(screen.getByLabelText("Danh mục loại phòng"), {
      target: { value: "5" },
    })
    fireEvent.change(screen.getByLabelText("Giá thuê mới"), {
      target: { value: "4000000" },
    })
    fireEvent.click(screen.getByRole("button", { name: "Thêm cấu hình" }))

    expect(
      await screen.findByText("Loại phòng này đã được cấu hình cho khu trọ."),
    ).toBeTruthy()
    expect(screen.queryByText("Đã thêm loại phòng cho khu trọ.")).toBeNull()
  })

  it("retries only the property configuration after the catalog type was created", async () => {
    adminSession()
    let configurationAttempts = 0
    const calls = stubApi({
      createCatalog: () =>
        jsonResponse(
          { roomTypeId: 9, typeName: "Phòng VIP", price: 0 },
          201,
        ),
      createConfiguration: () => {
        configurationAttempts += 1
        if (configurationAttempts === 1) {
          return jsonResponse(
            {
              title: "Property configuration failed",
              detail: "Không thể gắn cấu hình cho khu trọ.",
            },
            500,
          )
        }
        return jsonResponse(
          {
            propertyId: property.propertyId,
            roomTypeId: 9,
            roomTypeName: "Phòng VIP",
            monthlyRent: 6_000_000,
            isActive: true,
            totalRoomCount: 0,
            availableRoomCount: 0,
          },
          201,
        )
      },
    })

    renderPage()

    await screen.findByRole("option", { name: "Deluxe" })
    fireEvent.change(screen.getByLabelText("Tên loại phòng mới"), {
      target: { value: "Phòng VIP" },
    })
    fireEvent.change(screen.getByLabelText("Giá thuê loại phòng mới"), {
      target: { value: "6000000" },
    })
    fireEvent.click(screen.getByRole("button", { name: "Tạo và cấu hình" }))

    expect(
      await screen.findByText(
        /bước gắn giá thuê cho khu trọ thất bại: Không thể gắn cấu hình cho khu trọ/,
      ),
    ).toBeTruthy()

    const roomTypeSelect = screen.getByLabelText(
      "Danh mục loại phòng",
    ) as HTMLSelectElement
    const rentInput = screen.getByLabelText("Giá thuê mới") as HTMLInputElement
    expect(roomTypeSelect.value).toBe("9")
    expect(rentInput.value).toBe("6000000")

    fireEvent.click(screen.getByRole("button", { name: "Thêm cấu hình" }))

    expect(
      await screen.findByText("Đã thêm loại phòng cho khu trọ."),
    ).toBeTruthy()
    const catalogCreateCalls = calls.filter(
      (call) =>
        call.init?.method === "POST" &&
        call.url.endsWith("/room-types") &&
        !call.url.includes("/properties/"),
    )
    expect(catalogCreateCalls).toHaveLength(1)
    expect(configurationAttempts).toBe(2)
  })
})
