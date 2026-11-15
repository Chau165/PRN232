import type { ReactNode } from "react"
import { render, screen, waitFor } from "@testing-library/react"
import { beforeEach, describe, expect, it, vi } from "vitest"
import { MemoryRouter, Route, Routes } from "react-router"
import { AuthProvider } from "@/auth/AuthContext"
import { ProtectedRoute } from "@/components/auth/ProtectedRoute"
import CustomerRoomDrawer from "@/components/customer/CustomerRoomDrawer"
import PropertyDetailDrawer from "@/components/management/PropertyDetailDrawer"
import RoomDetailDrawer from "@/components/management/RoomDetailDrawer"
import PropertyDetailView, {
  type PropertyDetailData,
} from "@/components/property/PropertyDetailView"
import RoomDetailModal from "@/components/room/RoomDetailModal"
import RoomDetailView, {
  type RoomDetailData,
} from "@/components/room/RoomDetailView"
import type { BackendUserDto } from "@/types/auth"
import type { ManagementRoomDto } from "@/types/managementRoom"
import type { Rental } from "@/types/rental"
import type { PropertyDto } from "@/types/user"

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  })
}

function storeSession(accessToken: string) {
  window.sessionStorage.setItem(
    "troviet-auth-session-v1",
    JSON.stringify({
      accessToken,
      expiresAtUtc: new Date(Date.now() + 60_000).toISOString(),
    }),
  )
}

function renderWithAuth(token: string, ui: ReactNode) {
  storeSession(token)
  return render(
    <MemoryRouter initialEntries={["/management/detail"]}>
      <AuthProvider>
        <Routes>
          <Route
            path="/management/detail"
            element={
              <ProtectedRoute roles={["ADMIN", "MANAGER"]}>{ui}</ProtectedRoute>
            }
          />
          <Route path="/login" element={<div>Login</div>} />
        </Routes>
      </AuthProvider>
    </MemoryRouter>,
  )
}

const adminUserDto: BackendUserDto = {
  userId: 1,
  fullName: "Admin Thật",
  email: "admin@troviet.test",
  phoneNumber: "0900000001",
  roleId: 1,
  roleName: "Admin",
  isActive: true,
}

const managerUserDto: BackendUserDto = {
  userId: 2,
  fullName: "Quản Lý Minh",
  email: "manager@troviet.test",
  phoneNumber: "0900000002",
  roleId: 2,
  roleName: "Manager",
  isActive: true,
}

const sampleProperty: PropertyDetailData = {
  propertyId: 21,
  propertyName: "Nhà trọ Bình An",
  address: "12 Đường số 5, Phường 1, TP. Hồ Chí Minh",
  primaryImageUrl: "https://example.test/property.jpg",
  description: "Khu trọ yên tĩnh, an ninh 24/7.",
  startingPrice: 3_500_000,
  availableRoomCount: 3,
  roomCount: 10,
  ownerName: "Chủ Trọ Nội Bộ",
  latitude: 10.85,
  longitude: 106.77,
  services: [
    {
      key: "svc-electric",
      serviceName: "Điện sinh hoạt",
      price: 3_500,
      unitCode: "kwh",
      billingBasis: "usage",
    },
  ],
  equipments: [
    {
      equipmentId: 101,
      name: "Máy lạnh",
      quantity: 2,
      compensationValue: 8_500_000,
      roomCode: "BA-101",
      status: "Operational",
    },
    {
      equipmentId: 102,
      name: "Bàn học",
      quantity: 1,
      compensationValue: 1_200_000,
      roomCode: "BA-102",
      status: "Operational",
    },
  ],
}

const sampleRoom: RoomDetailData = {
  roomId: 31,
  roomCode: "BA-101",
  propertyId: 21,
  propertyName: "Nhà trọ Bình An",
  address: "12 Đường số 5, Phường 1, TP. Hồ Chí Minh",
  roomTypeName: "Studio Ban Công",
  price: 3_800_000,
  areaSqm: 26,
  maxOccupants: 2,
  floor: 0,
  description: "Phòng sáng thoáng, đầy đủ nội thất.",
  status: "Available",
  images: ["https://example.test/room-1.jpg"],
  services: [
    {
      serviceId: 1,
      name: "Điện",
      price: 3_500,
      unitCode: "kwh",
      billingBasis: "usage",
    },
  ],
  equipments: [
    {
      equipmentId: 201,
      name: "Máy lạnh",
      quantity: 2,
      compensationValue: 8_500_000,
      status: "Operational",
      supplier: "Điện Máy Xanh",
      warrantyExpiry: "2027-12-31",
    },
    {
      equipmentId: 202,
      name: "Giường gỗ",
      quantity: 1,
      compensationValue: 2_000_000,
      status: "Operational",
    },
  ],
}

const dtoProperty: PropertyDto = {
  propertyId: 21,
  ownerId: 1,
  ownerName: "Chủ Trọ Nội Bộ",
  propertyName: "Nhà trọ Bình An",
  addressText: "12 Đường số 5",
  ward: "Phường 1",
  district: "Thủ Đức",
  city: "TP. Hồ Chí Minh",
  latitude: 10.85,
  longitude: 106.77,
  primaryImageUrl: "https://example.test/property.jpg",
  description: "Khu trọ yên tĩnh, an ninh 24/7.",
  roomCount: 10,
}

const dtoRoom: ManagementRoomDto = {
  roomId: 31,
  propertyId: 21,
  propertyName: "Nhà trọ Bình An",
  roomTypeId: 4,
  roomTypeName: "Studio Ban Công",
  roomTypePrice: 3_800_000,
  roomCode: "BA-101",
  areaSqm: 26,
  maxOccupants: 2,
  floor: 0,
  description: "Phòng sáng thoáng, đầy đủ nội thất.",
  status: "Available",
  primaryImageUrl: "https://example.test/room-1.jpg",
}

const customerRental: Rental = {
  contractId: 90,
  contractStatus: "Active",
  startDate: "2026-01-01",
  endDate: "2026-12-31",
  monthlyRent: 3_800_000,
  depositAmount: 3_800_000,
  roomId: 31,
  roomCode: "BA-101",
  roomStatus: "Occupied",
  areaSqm: 26,
  roomTypeId: 4,
  roomTypeName: "Studio Ban Công",
  propertyId: 21,
  propertyName: "Nhà trọ Bình An",
  propertyAddress: "12 Đường số 5, Phường 1, TP. Hồ Chí Minh",
  primaryImageUrl: "https://example.test/room-1.jpg",
  images: ["https://example.test/room-1.jpg"],
  equipments: [{ equipmentId: 201, name: "Máy lạnh", status: "Operational" }],
  amenities: [{ amenityId: 1, name: "Wi-Fi" }],
}

describe("Bước 2 (6.2): Component chi tiết khu trọ và phòng dùng chung theo vai trò", () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    window.sessionStorage.clear()
    window.localStorage.clear()
  })

  it("PropertyDetailView hiển thị chung ảnh, địa chỉ, mô tả, giá, dịch vụ, thiết bị và chỉ lộ thông tin nội bộ / giá bồi thường cho Quản lý & Admin", () => {
    const { rerender } = render(
      <PropertyDetailView
        property={sampleProperty}
        viewerRole="guest"
        layout="drawer"
        actions={<button type="button">Xem phòng trống</button>}
      />,
    )

    expect(screen.getByAltText("Ảnh Nhà trọ Bình An")).toBeTruthy()
    expect(screen.getByText(sampleProperty.address)).toBeTruthy()
    expect(screen.getByText(sampleProperty.description!)).toBeTruthy()
    expect(screen.getByText(/3\.500\.000/)).toBeTruthy()
    expect(screen.getByText("Điện")).toBeTruthy()
    expect(screen.getByText("Máy lạnh × 2")).toBeTruthy()
    expect(screen.getByText("Bàn học")).toBeTruthy()
    expect(screen.getByRole("button", { name: "Xem phòng trống" })).toBeTruthy()

    // Thông tin nội bộ (giá bồi thường, chủ sở hữu, tọa độ) phải ẩn với Khách.
    expect(screen.queryByLabelText("Thông tin nội bộ khu trọ")).toBeNull()
    expect(screen.queryByText("Chủ Trọ Nội Bộ")).toBeNull()
    expect(screen.queryByText(/8\.500\.000/)).toBeNull()
    expect(screen.queryByText(/10\.85, 106\.77/)).toBeNull()

    rerender(
      <PropertyDetailView
        property={sampleProperty}
        viewerRole="manager"
        layout="drawer"
        actions={<button type="button">Quản lý phòng</button>}
      />,
    )

    expect(screen.getByAltText("Ảnh Nhà trọ Bình An")).toBeTruthy()
    expect(screen.getByText(sampleProperty.address)).toBeTruthy()
    expect(screen.getByText(sampleProperty.description!)).toBeTruthy()
    expect(screen.getByText("Điện")).toBeTruthy()
    expect(screen.getByLabelText("Thông tin nội bộ khu trọ")).toBeTruthy()
    expect(screen.getByText("Chủ Trọ Nội Bộ")).toBeTruthy()
    expect(screen.getByText("10.85, 106.77")).toBeTruthy()
    expect(screen.getByText("Máy lạnh × 2 — 8.500.000đ/cái")).toBeTruthy()
    expect(screen.getByText("Bàn học × 1 — 1.200.000đ/cái")).toBeTruthy()
  })

  it("RoomDetailView giữ thống nhất nội dung công khai ở mọi vai trò và chỉ hiện giá bồi thường, NCC, bảo hành cho Quản lý & Admin", () => {
    const { rerender } = render(
      <RoomDetailView
        room={sampleRoom}
        viewerRole="guest"
        actions={<button type="button">Đặt cọc giữ phòng</button>}
      />,
    )

    expect(screen.getByAltText("Ảnh Studio Ban Công")).toBeTruthy()
    expect(screen.getByText(/3\.800\.000/)).toBeTruthy()
    expect(screen.getByText("26 m²")).toBeTruthy()
    expect(screen.getByText("Tối đa 2 người")).toBeTruthy()
    expect(screen.getByText("Tầng trệt")).toBeTruthy()
    expect(screen.getByText(sampleRoom.description!)).toBeTruthy()
    expect(screen.getByText(sampleRoom.address!)).toBeTruthy()
    expect(screen.getByText("Điện")).toBeTruthy()
    expect(screen.getByText("Máy lạnh × 2")).toBeTruthy()
    expect(screen.getByText("Giường gỗ")).toBeTruthy()

    // Ẩn thông tin nội bộ và giá bồi thường với Khách.
    expect(screen.queryByLabelText("Thông tin nội bộ phòng")).toBeNull()
    expect(screen.queryByText(/8\.500\.000/)).toBeNull()
    expect(screen.queryByText(/Điện Máy Xanh/)).toBeNull()

    rerender(
      <RoomDetailView
        room={sampleRoom}
        viewerRole="admin"
        actions={<button type="button">Sửa phòng</button>}
      />,
    )

    expect(screen.getByLabelText("Thông tin nội bộ phòng")).toBeTruthy()
    expect(screen.getByText("Phòng BA-101")).toBeTruthy()
    expect(screen.getByText("Studio Ban Công")).toBeTruthy()
    expect(screen.getByText("Trống")).toBeTruthy()
    expect(screen.getByText("Tối đa 2 người")).toBeTruthy()
    expect(screen.getByText("Tầng trệt")).toBeTruthy()
    expect(screen.getByText("Máy lạnh × 2 — 8.500.000đ/cái")).toBeTruthy()
    expect(screen.getByText("Giường gỗ × 1 — 2.000.000đ/cái")).toBeTruthy()
    expect(screen.getByText(/NCC: Điện Máy Xanh/)).toBeTruthy()
  })

  it("PropertyDetailDrawer hiển thị đầy đủ nút thêm/sửa/xóa cho Admin và chỉ thao tác quản lý cho Manager", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async (input: RequestInfo | URL) => {
        const url = String(input)
        if (url.endsWith("/api/users/me")) {
          return jsonResponse(adminUserDto)
        }
        if (url.includes("/api/properties/21/room-types")) {
          return jsonResponse([
            {
              roomTypeId: 4,
              roomTypeName: "Studio Ban Công",
              monthlyRent: 3_800_000,
              availableRoomCount: 2,
              totalRoomCount: 5,
              isActive: true,
            },
          ])
        }
        if (url.includes("/api/equipments")) {
          return jsonResponse([
            {
              equipmentId: 101,
              roomId: null,
              roomCode: null,
              propertyId: 21,
              propertyName: "Nhà trọ Bình An",
              name: "Máy lạnh",
              quantity: 2,
              compensationValue: 8_500_000,
              status: "Operational",
              installDate: "2026-01-01",
              warrantyExpiry: "2027-01-01",
              supplier: "Điện Máy Xanh",
            },
          ])
        }
        if (url.includes("/api/properties/21/service-rates")) {
          return jsonResponse([])
        }
        if (url.includes("/api/services")) {
          return jsonResponse([])
        }
        return jsonResponse({ title: "Not Found" }, 404)
      }),
    )

    const { unmount } = renderWithAuth(
      "admin.jwt",
      <PropertyDetailDrawer
        property={dtoProperty}
        onClose={() => {}}
        onEdit={() => {}}
        onDelete={() => {}}
      />,
    )

    expect(
      await screen.findByText("Máy lạnh × 2 — 8.500.000đ/cái"),
    ).toBeTruthy()
    expect(screen.getByText("Chủ Trọ Nội Bộ")).toBeTruthy()
    expect(screen.getByRole("button", { name: "Quản lý phòng" })).toBeTruthy()
    expect(
      screen.getByRole("button", { name: "Loại phòng & giá" }),
    ).toBeTruthy()
    expect(screen.getByRole("button", { name: "Sửa khu trọ" })).toBeTruthy()
    expect(screen.getByRole("button", { name: "Xóa khu trọ" })).toBeTruthy()
    unmount()

    vi.stubGlobal(
      "fetch",
      vi.fn(async (input: RequestInfo | URL) => {
        const url = String(input)
        if (url.endsWith("/api/users/me")) {
          return jsonResponse(managerUserDto)
        }
        if (url.includes("/api/properties/21/room-types")) {
          return jsonResponse([])
        }
        if (url.includes("/api/equipments")) {
          return jsonResponse([
            {
              equipmentId: 101,
              roomId: null,
              roomCode: null,
              propertyId: 21,
              propertyName: "Nhà trọ Bình An",
              name: "Máy lạnh",
              quantity: 2,
              compensationValue: 8_500_000,
              status: "Operational",
              installDate: "2026-01-01",
              warrantyExpiry: "2027-01-01",
              supplier: "Điện Máy Xanh",
            },
          ])
        }
        if (url.includes("/api/properties/21/service-rates")) {
          return jsonResponse([])
        }
        if (url.includes("/api/services")) {
          return jsonResponse([])
        }
        return jsonResponse({ title: "Not Found" }, 404)
      }),
    )

    renderWithAuth(
      "manager.jwt",
      <PropertyDetailDrawer
        property={dtoProperty}
        onClose={() => {}}
        onEdit={() => {}}
        onDelete={() => {}}
      />,
    )

    expect(
      await screen.findByText("Máy lạnh × 2 — 8.500.000đ/cái"),
    ).toBeTruthy()
    expect(screen.getByRole("button", { name: "Quản lý phòng" })).toBeTruthy()
    expect(screen.queryByRole("button", { name: "Loại phòng & giá" })).toBeNull()
    expect(screen.queryByRole("button", { name: "Sửa khu trọ" })).toBeNull()
    expect(screen.queryByRole("button", { name: "Xóa khu trọ" })).toBeNull()
  })

  it("RoomDetailModal (Khách), CustomerRoomDrawer (Khách thuê), RoomDetailDrawer (Manager/Admin) dùng chung RoomDetailView với nút thao tác đúng vai trò", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async (input: RequestInfo | URL) => {
        const url = String(input)
        if (url.includes("/api/public/rooms/31")) {
          return jsonResponse({
            roomId: 31,
            roomCode: "BA-101",
            areaSqm: 26,
            maxOccupants: 2,
            floor: 0,
            description: "Phòng sáng thoáng, đầy đủ nội thất.",
            baseRent: 3_800_000,
            property: {
              propertyId: 21,
              propertyName: "Nhà trọ Bình An",
              addressText: "12 Đường số 5",
              ward: "Phường 1",
              city: "TP. Hồ Chí Minh",
            },
            roomType: {
              roomTypeId: 4,
              roomTypeName: "Studio Ban Công",
            },
            images: ["https://example.test/room-1.jpg"],
            amenities: [{ amenityId: 1, name: "Wi-Fi" }],
            services: [
              { serviceId: 1, name: "Điện", billingBasis: "usage", unitCode: "kwh" },
            ],
            equipments: [
              { equipmentId: 201, name: "Máy lạnh", quantity: 2 },
              { equipmentId: 202, name: "Giường gỗ", quantity: 1 },
            ],
          })
        }
        return jsonResponse({ title: "Not Found" }, 404)
      }),
    )

    const guestView = render(
      <MemoryRouter>
        <RoomDetailModal
          roomId={31}
          propertyServiceRates={[
            {
              serviceId: 1,
              serviceName: "Điện",
              unitCode: "kwh",
              billingBasis: "usage",
              price: 3_500,
              effectiveFrom: "2026-01-01",
            },
          ]}
          onClose={() => {}}
          onRefresh={() => {}}
        />
      </MemoryRouter>,
    )

    expect(await screen.findByText("Máy lạnh × 2")).toBeTruthy()
    expect(screen.getByText("Giường gỗ")).toBeTruthy()
    expect(screen.queryByText(/8\.500\.000/)).toBeNull()
    expect(
      screen.getByRole("link", { name: "Đặt lịch xem phòng" }),
    ).toBeTruthy()
    expect(
      screen.getByRole("link", { name: "Đặt cọc giữ phòng" }),
    ).toBeTruthy()
    guestView.unmount()

    const customerView = render(
      <MemoryRouter>
        <CustomerRoomDrawer
          room={customerRental}
          onClose={() => {}}
          onReportDamage={() => {}}
        />
      </MemoryRouter>,
    )

    expect(screen.getByText("Máy lạnh")).toBeTruthy()
    expect(screen.queryByText(/8\.500\.000/)).toBeNull()
    expect(screen.getByRole("button", { name: "Báo hư hỏng" })).toBeTruthy()
    customerView.unmount()

    vi.stubGlobal(
      "fetch",
      vi.fn(async (input: RequestInfo | URL) => {
        const url = String(input)
        if (url.endsWith("/api/users/me")) {
          return jsonResponse(managerUserDto)
        }
        if (url.includes("/api/equipments")) {
          return jsonResponse([
            {
              equipmentId: 201,
              roomId: 31,
              roomCode: "BA-101",
              propertyId: 21,
              propertyName: "Nhà trọ Bình An",
              name: "Máy lạnh",
              quantity: 2,
              compensationValue: 8_500_000,
              status: "Operational",
              installDate: "2026-01-01",
              warrantyExpiry: "2027-12-31",
              supplier: "Điện Máy Xanh",
            },
          ])
        }
        if (url.includes("/api/properties/21/service-rates")) {
          return jsonResponse([
            {
              rateId: 1,
              propertyId: 21,
              serviceId: 1,
              serviceName: "Điện",
              unitCode: "kwh",
              billingBasis: "usage",
              price: 3_500,
              effectiveFrom: "2025-01-01",
              effectiveTo: null,
            },
          ])
        }
        if (url.includes("/api/rooms/31/images")) {
          return jsonResponse([])
        }
        return jsonResponse({ title: "Not Found" }, 404)
      }),
    )

    const managerView = renderWithAuth(
      "manager.jwt",
      <RoomDetailDrawer
        room={dtoRoom}
        property={dtoProperty}
        onClose={() => {}}
        accessToken="manager.jwt"
        onImagesChanged={() => {}}
        onEdit={() => {}}
        onDelete={() => {}}
      />,
    )

    expect(
      await screen.findByText("Máy lạnh × 2 — 8.500.000đ/cái"),
    ).toBeTruthy()
    expect(screen.getByText(/3\.500/)).toBeTruthy()
    expect(screen.getByRole("button", { name: "Quản lý thiết bị" })).toBeTruthy()
    expect(screen.queryByRole("button", { name: "Sửa phòng" })).toBeNull()
    expect(screen.queryByRole("button", { name: "Xóa phòng" })).toBeNull()
    managerView.unmount()

    vi.stubGlobal(
      "fetch",
      vi.fn(async (input: RequestInfo | URL) => {
        const url = String(input)
        if (url.endsWith("/api/users/me")) {
          return jsonResponse(adminUserDto)
        }
        if (url.includes("/api/equipments")) {
          return jsonResponse([
            {
              equipmentId: 201,
              roomId: 31,
              roomCode: "BA-101",
              propertyId: 21,
              propertyName: "Nhà trọ Bình An",
              name: "Máy lạnh",
              quantity: 2,
              compensationValue: 8_500_000,
              status: "Operational",
              installDate: "2026-01-01",
              warrantyExpiry: "2027-12-31",
              supplier: "Điện Máy Xanh",
            },
          ])
        }
        if (url.includes("/api/properties/21/service-rates")) {
          return jsonResponse([])
        }
        if (url.includes("/api/rooms/31/images")) {
          return jsonResponse([])
        }
        return jsonResponse({ title: "Not Found" }, 404)
      }),
    )

    renderWithAuth(
      "admin.jwt",
      <RoomDetailDrawer
        room={dtoRoom}
        property={dtoProperty}
        onClose={() => {}}
        accessToken="admin.jwt"
        onImagesChanged={() => {}}
        onEdit={() => {}}
        onDelete={() => {}}
      />,
    )

    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: "Thêm / Quản lý thiết bị" }),
      ).toBeTruthy()
    })
    expect(screen.getByRole("button", { name: "Sửa phòng" })).toBeTruthy()
    expect(screen.getByRole("button", { name: "Xóa phòng" })).toBeTruthy()
  })
})
