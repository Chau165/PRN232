import type { ReactNode } from "react"
import { fireEvent, render, screen, waitFor } from "@testing-library/react"
import { beforeEach, describe, expect, it, vi } from "vitest"
import { MemoryRouter, Route, Routes, useLocation } from "react-router"
import { AuthProvider } from "@/auth/AuthContext"
import { ProtectedRoute } from "@/components/auth/ProtectedRoute"
import ManagementSidebar from "@/components/management/ManagementSidebar"
import ManagementPropertiesPage from "@/pages/ManagementPropertiesPage/ManagementPropertiesPage"
import ManagementRoomsPage from "@/pages/ManagementRoomsPage/ManagementRoomsPage"
import { routes } from "@/constants/routes"
import type { CurrentUser } from "@/types/user"

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

const room = {
  roomId: 31,
  propertyId: property.propertyId,
  propertyName: property.propertyName,
  roomTypeId: 4,
  roomTypeName: "Studio",
  roomTypePrice: 3_000_000,
  roomCode: "BA-A101",
  areaSqm: 24.5,
  status: "Available",
  primaryImageUrl: null,
}

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

function LocationProbe() {
  const location = useLocation()
  return <output data-testid="location-probe">{location.pathname}</output>
}

function renderManagementPage(
  page: ReactNode,
  initialEntries = ["/management/properties"],
) {
  return render(
    <MemoryRouter initialEntries={initialEntries}>
      <AuthProvider>
        <ProtectedRoute roles={["ADMIN", "MANAGER"]}>{page}</ProtectedRoute>
        <LocationProbe />
      </AuthProvider>
    </MemoryRouter>,
  )
}

function renderScopedRoomsPage() {
  return render(
    <MemoryRouter initialEntries={["/management/properties/21/rooms"]}>
      <AuthProvider>
        <Routes>
          <Route
            path="/management/properties/:propertyId/rooms"
            element={
              <ProtectedRoute roles={["ADMIN", "MANAGER"]}>
                <ManagementRoomsPage />
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

describe("management property and room CRUD", () => {
  it("renders the property's API image and assigned manager name without the manager email", async () => {
    adminSession()
    vi.stubGlobal("fetch", vi.fn<typeof fetch>(async (input) => {
      const url = String(input)
      if (url.endsWith("/users/me")) return jsonResponse(admin)
      if (url.includes("/properties/managers?propertyIds=21")) return jsonResponse([{ propertyId: 21, managers: [{
        propertyId: 21, managerId: 8, managerName: "Nguyễn Hoàng An",
        managerEmail: "manager.private@example.test", managerIsActive: true,
      }] }])
      if (url.endsWith("/properties")) return jsonResponse([{
        ...property, primaryImageUrl: "https://images.example.test/property-21.jpg",
      }])
      return jsonResponse([], 200)
    }))
    renderManagementPage(<ManagementPropertiesPage />)
    expect(await screen.findByText("Nguyễn Hoàng An")).toBeTruthy()
    const image = screen.getByRole("img", { name: `Ảnh ${property.propertyName}` })
    expect(image.getAttribute("src")).toBe("https://images.example.test/property-21.jpg")
    expect(screen.queryByText("manager.private@example.test")).toBeNull()
    fireEvent.error(image)
    expect(screen.queryByRole("img", { name: `Ảnh ${property.propertyName}` })).toBeNull()
    expect(screen.getByText("Chưa có ảnh")).toBeTruthy()
  })

  it("creates a property through the management API and sends no client owner id", async () => {
    adminSession()
    const calls: Array<{ url: string; init?: RequestInit }> = []
    vi.stubGlobal(
      "fetch",
      vi.fn<typeof fetch>(async (input, init) => {
        const url = String(input)
        calls.push({ url, init })
        if (url.endsWith("/users/me")) return jsonResponse(admin)
        if (url.endsWith("/roles"))
          return jsonResponse([{ roleId: 2, roleName: "Manager" }])
        if (url.includes("/users?roleId=2")) return jsonResponse({ items: [], page: 1, pageSize: 100, totalCount: 0 })
        if (url.includes("/properties/managers?propertyIds=21")) return jsonResponse([])
        if (url.endsWith("/properties") && init?.method === "POST")
          return jsonResponse(property, 201)
        if (url.endsWith("/properties")) return jsonResponse([])
        return jsonResponse({ title: "Not Found" }, 404)
      }),
    )

    renderManagementPage(<ManagementPropertiesPage />)
    fireEvent.click(
      await screen.findByRole("button", { name: "+ Thêm khu trọ" }),
    )
    expect(screen.queryByLabelText("Vĩ độ")).toBeNull()
    expect(screen.queryByLabelText("Kinh độ")).toBeNull()
    fireEvent.change(screen.getByLabelText("Tên khu trọ"), {
      target: { value: property.propertyName },
    })
    fireEvent.change(screen.getByLabelText("Địa chỉ"), {
      target: { value: property.addressText },
    })
    const image = new File([new Uint8Array([1, 2, 3])], "property.png", {
      type: "image/png",
    })
    fireEvent.change(screen.getByLabelText("Ảnh đại diện"), {
      target: { files: [image] },
    })
    fireEvent.click(screen.getByRole("button", { name: "Tạo khu trọ" }))

    await waitFor(() =>
      expect(calls.some((call) => call.init?.method === "POST")).toBe(true),
    )
    const createCall = calls.find((call) => call.init?.method === "POST")!
    expect(createCall.url).toMatch(/\/api\/properties$/)
    expect(createCall.init?.body).toBeInstanceOf(FormData)
    const body = createCall.init?.body as FormData
    expect(body.get("ownerId")).toBeNull()
    expect(body.get("latitude")).toBeNull()
    expect(body.get("longitude")).toBeNull()
    expect(body.get("propertyName")).toBe(property.propertyName)
    expect(body.get("primaryImage")).toBeInstanceOf(File)
  })

  it("keeps property manager assignments read-only on the property page", async () => {
    adminSession()
    vi.stubGlobal(
      "fetch",
      vi.fn<typeof fetch>(async (input) => {
        const url = String(input)
        if (url.endsWith("/users/me")) return jsonResponse(admin)
        if (url.endsWith("/roles"))
          return jsonResponse([{ roleId: 2, roleName: "Manager" }])
        if (url.includes("/users?roleId=2"))
          return jsonResponse({ items: [
            {
              userId: 11,
              fullName: "Nguyễn Hoàng An",
              email: "an@example.test",
              roleId: 2,
              roleName: "Manager",
              isActive: true,
            },
            {
              userId: 12,
              fullName: "Trần Gia Bình",
              email: "binh@example.test",
              roleId: 2,
              roleName: "Manager",
              isActive: true,
            },
          ], page: 1, pageSize: 100, totalCount: 2 })
        if (url.includes("/properties/managers?propertyIds=21"))
          return jsonResponse([{
            propertyId: 21,
            managers: [
              {
                propertyId: 21,
                managerId: 11,
                managerName: "Nguyễn Hoàng An",
                managerEmail: "an@example.test",
                managerIsActive: true,
              },
            ],
          }])
        if (url.endsWith("/properties")) return jsonResponse([property])
        return jsonResponse({ title: "Not Found" }, 404)
      }),
    )

    renderManagementPage(<ManagementPropertiesPage />)

    expect(await screen.findByText(property.propertyName)).toBeTruthy()
    expect(screen.getByText("Nguyễn Hoàng An")).toBeTruthy()
    expect(screen.queryByLabelText("Chọn nhiều Manager")).toBeNull()
    expect(screen.queryByRole("button", { name: "Gán Manager" })).toBeNull()
    expect(screen.queryByRole("button", { name: "Thu hồi" })).toBeNull()
  })

  it("uses a room column and opens the selected property's rooms", async () => {
    adminSession()
    const calls: string[] = []
    vi.stubGlobal(
      "fetch",
      vi.fn<typeof fetch>(async (input) => {
        const url = String(input)
        calls.push(url)
        if (url.endsWith("/users/me")) return jsonResponse(admin)
        if (url.includes("/properties/managers?propertyIds=21")) return jsonResponse([])
        if (url.endsWith("/properties")) {
          return jsonResponse([{ ...property, roomCount: 12 }])
        }
        return jsonResponse({ title: "Not Found" }, 404)
      }),
    )

    renderManagementPage(<ManagementPropertiesPage />)

    expect(await screen.findByText("Phòng")).toBeTruthy()
    expect(screen.queryByText("Trạng thái")).toBeNull()
    fireEvent.click(
      await screen.findByRole("button", {
        name: "Xem 12 phòng của Nhà trọ Bình An",
      }),
    )
    expect(screen.getByTestId("location-probe").textContent).toBe(
      "/management/properties/21/rooms",
    )
    expect(calls.filter((url) => url.includes("/managers")).map((url) => url.replace(/^.*\/api/, ""))).toEqual([
      "/properties/managers?propertyIds=21",
    ])
  })

  it("scopes room CRUD to the property in the route", async () => {
    adminSession()
    const calls: Array<{ url: string; init?: RequestInit }> = []
    vi.stubGlobal(
      "fetch",
      vi.fn<typeof fetch>(async (input, init) => {
        const url = String(input)
        calls.push({ url, init })
        if (url.endsWith("/users/me")) return jsonResponse(admin)
        if (url.endsWith("/rooms") && init?.method === "POST")
          return jsonResponse(room, 201)
        if (url.includes("/rooms?propertyId=21")) return jsonResponse([room])
        if (url.endsWith("/properties")) return jsonResponse([property])
        if (url.endsWith("/room-types"))
          return jsonResponse([
            { roomTypeId: 4, typeName: "Studio", price: 3_000_000 },
          ])
        return jsonResponse({ title: "Not Found" }, 404)
      }),
    )

    renderScopedRoomsPage()

    expect(
      await screen.findByText("Quản lý phòng của Nhà trọ Bình An"),
    ).toBeTruthy()
    expect(screen.queryByLabelText("Lọc khu trọ")).toBeNull()
    fireEvent.click(await screen.findByRole("button", { name: "+ Thêm phòng" }))
    const propertySelect = screen.getByLabelText("Khu trọ") as HTMLSelectElement
    expect(propertySelect.value).toBe(String(property.propertyId))
    expect(propertySelect.disabled).toBe(true)
    fireEvent.change(screen.getByLabelText("Loại phòng"), {
      target: { value: "4" },
    })
    fireEvent.change(screen.getByLabelText("Mã phòng"), {
      target: { value: room.roomCode },
    })
    fireEvent.change(screen.getByLabelText("Diện tích (m²)"), {
      target: { value: String(room.areaSqm) },
    })
    fireEvent.click(screen.getByRole("button", { name: "Tạo phòng" }))

    await waitFor(() =>
      expect(
        calls.some(
          (call) => call.init?.method === "POST" && call.url.endsWith("/rooms"),
        ),
      ).toBe(true),
    )
    const createCall = calls.find(
      (call) => call.init?.method === "POST" && call.url.endsWith("/rooms"),
    )!
    expect(JSON.parse(String(createCall.init?.body))).toMatchObject({
      propertyId: property.propertyId,
    })
  })

  it("does not show a standalone Phòng link in the management sidebar", () => {
    const user: CurrentUser = {
      id: String(admin.userId),
      name: admin.fullName,
      email: admin.email,
      phone: admin.phoneNumber,
      role: "ADMIN",
    }
    render(
      <ManagementSidebar
        user={user}
        pathname={routes.managementProperties}
        open={false}
        onClose={() => undefined}
      />,
    )

    expect(screen.queryByRole("link", { name: "Phòng" })).toBeNull()
    expect(screen.getByRole("link", { name: "Khu trọ" })).toBeTruthy()
  })

  it("loads rooms and creates one with property and room type selected from APIs", async () => {
    adminSession()
    const calls: Array<{ url: string; init?: RequestInit }> = []
    vi.stubGlobal(
      "fetch",
      vi.fn<typeof fetch>(async (input, init) => {
        const url = String(input)
        calls.push({ url, init })
        if (url.endsWith("/users/me")) return jsonResponse(admin)
        if (url.endsWith("/rooms") && init?.method === "POST")
          return jsonResponse(room, 201)
        if (url.endsWith("/rooms")) return jsonResponse([])
        if (url.endsWith("/properties")) return jsonResponse([property])
        if (url.endsWith("/room-types"))
          return jsonResponse([
            { roomTypeId: 4, typeName: "Studio", price: 3_000_000 },
          ])
        return jsonResponse({ title: "Not Found" }, 404)
      }),
    )

    renderManagementPage(<ManagementRoomsPage />)
    fireEvent.click(await screen.findByRole("button", { name: "+ Thêm phòng" }))
    fireEvent.change(screen.getByLabelText("Khu trọ"), {
      target: { value: String(property.propertyId) },
    })
    fireEvent.change(screen.getByLabelText("Loại phòng"), {
      target: { value: "4" },
    })
    fireEvent.change(screen.getByLabelText("Mã phòng"), {
      target: { value: room.roomCode },
    })
    fireEvent.change(screen.getByLabelText("Diện tích (m²)"), {
      target: { value: String(room.areaSqm) },
    })
    fireEvent.click(screen.getByRole("button", { name: "Tạo phòng" }))

    await waitFor(() =>
      expect(
        calls.some(
          (call) => call.init?.method === "POST" && call.url.endsWith("/rooms"),
        ),
      ).toBe(true),
    )
    const createCall = calls.find(
      (call) => call.init?.method === "POST" && call.url.endsWith("/rooms"),
    )!
    expect(JSON.parse(String(createCall.init?.body))).toEqual({
      propertyId: property.propertyId,
      roomTypeId: room.roomTypeId,
      roomCode: room.roomCode,
      areaSqm: room.areaSqm,
    })
  })

  it("does not keep removed management area or tenant routes", () => {
    expect("managementAreas" in routes).toBe(false)
    expect("managementTenants" in routes).toBe(false)
  })
})
