import { fireEvent, render, screen, waitFor } from "@testing-library/react"
import type { ReactNode } from "react"
import { beforeEach, describe, expect, it, vi } from "vitest"
import ManagementEquipmentPage from "@/pages/ManagementEquipmentPage/ManagementEquipmentPage"

const mocks = vi.hoisted(() => ({
  getEquipments: vi.fn(),
  getEquipment: vi.fn(),
  createEquipment: vi.fn(),
  updateEquipment: vi.fn(),
  deleteEquipment: vi.fn(),
  getProperties: vi.fn(),
  getManagementRooms: vi.fn(),
  user: { id: "9", name: "Admin", email: "admin@example.test", phone: "", role: "ADMIN" as const },
}))

vi.mock("@/auth/AuthContext", () => ({
  useAuth: () => ({ accessToken: "admin-token", user: mocks.user }),
}))

vi.mock("@/utils/managementAuth", () => ({
  getCurrentUser: () => mocks.user,
}))

vi.mock("@/utils/managementScope", () => ({
  getManagementScope: () => ({ equipment: [] }),
}))

vi.mock("@/components/management/ManagementPage", () => ({
  default: ({ title, children }: { title: string; children: ReactNode }) => (
    <main><h1>{title}</h1>{children}</main>
  ),
}))

vi.mock("@/services/equipmentService", () => ({
  getEquipments: mocks.getEquipments,
  getEquipment: mocks.getEquipment,
  createEquipment: mocks.createEquipment,
  updateEquipment: mocks.updateEquipment,
  deleteEquipment: mocks.deleteEquipment,
}))

vi.mock("@/services/propertyService", () => ({
  getProperties: mocks.getProperties,
}))

vi.mock("@/services/roomService", () => ({
  getManagementRooms: mocks.getManagementRooms,
}))

const properties = [{
  propertyId: 1,
  ownerId: 9,
  ownerName: "Admin",
  propertyName: "Sunrise",
  addressText: "Address",
  ward: "Ward",
  district: "District",
  city: "City",
  roomCount: 1,
}]

const rooms = [{
  roomId: 11,
  propertyId: 1,
  propertyName: "Sunrise",
  roomTypeId: 1,
  roomTypeName: "Studio",
  roomTypePrice: 3_000_000,
  roomCode: "A101",
  areaSqm: 20,
  status: "Available",
}]

const equipment = {
  equipmentId: 101,
  roomId: 11,
  roomCode: "A101",
  propertyId: 1,
  propertyName: "Sunrise",
  name: "Máy lạnh API",
  installDate: "2026-01-15",
  warrantyExpiry: "2028-01-15",
  supplier: "Nhà cung cấp API",
  status: "Operational",
}

beforeEach(() => {
  vi.clearAllMocks()
  mocks.getEquipments.mockResolvedValue([equipment])
  mocks.getEquipment.mockResolvedValue(equipment)
  mocks.getProperties.mockResolvedValue(properties)
  mocks.getManagementRooms.mockResolvedValue(rooms)
  mocks.createEquipment.mockResolvedValue(equipment)
  mocks.updateEquipment.mockResolvedValue(equipment)
  mocks.deleteEquipment.mockResolvedValue(undefined)
})

describe("management equipment page", () => {
  it("loads real equipment data and does not render the old mock records", async () => {
    render(<ManagementEquipmentPage />)

    expect(await screen.findByText("Máy lạnh API")).toBeTruthy()
    expect(screen.getByText("A101")).toBeTruthy()
    expect(screen.getByText("Sunrise")).toBeTruthy()
    expect(screen.queryByText("Máy lạnh Daikin")).toBeNull()
    expect(mocks.getEquipments).toHaveBeenCalledWith(
      "admin-token",
      expect.objectContaining({ page: 1, pageSize: 50 }),
      expect.any(AbortSignal),
    )
  })

  it("navigates between equipment pages instead of appending rows", async () => {
    const firstPage = Array.from({ length: 50 }, (_, index) => ({
      ...equipment,
      equipmentId: 200 + index,
      name: index === 0 ? "Thiết bị trang 1" : `Thiết bị trang 1 - ${index}`,
    }))
    const secondPage = [{ ...equipment, equipmentId: 301, name: "Thiết bị trang 2" }]
    mocks.getEquipments.mockReset()
    mocks.getEquipments
      .mockResolvedValueOnce(firstPage)
      .mockResolvedValueOnce(secondPage)

    render(<ManagementEquipmentPage />)

    expect(await screen.findByText("Thiết bị trang 1")).toBeTruthy()
    fireEvent.click(screen.getByRole("button", { name: "Trang sau" }))

    expect(await screen.findByText("Thiết bị trang 2")).toBeTruthy()
    expect(screen.queryByText("Thiết bị trang 1")).toBeNull()
    expect(mocks.getEquipments).toHaveBeenLastCalledWith(
      "admin-token",
      expect.objectContaining({ page: 2, pageSize: 50 }),
      expect.any(AbortSignal),
    )
    expect(screen.getByRole("button", { name: "Trang trước" })).not.toHaveProperty(
      "disabled",
      true,
    )
    expect(screen.getByRole("button", { name: "Trang sau" })).toHaveProperty(
      "disabled",
      true,
    )
  })

  it("creates equipment through the API and sends only backend-supported fields", async () => {
    mocks.getEquipments.mockResolvedValueOnce([])
    render(<ManagementEquipmentPage />)

    fireEvent.click(await screen.findByRole("button", { name: "+ Thêm thiết bị" }))
    fireEvent.change(screen.getByLabelText("Khu trọ"), { target: { value: "1" } })
    fireEvent.change(screen.getByLabelText("Phòng"), { target: { value: "11" } })
    fireEvent.change(screen.getByLabelText("Tên thiết bị"), { target: { value: "Quạt API" } })
    fireEvent.change(screen.getByLabelText("Ngày lắp đặt"), { target: { value: "2026-02-01" } })
    fireEvent.change(screen.getByLabelText("Hạn bảo hành"), { target: { value: "2028-02-01" } })
    fireEvent.change(screen.getByLabelText("Nhà cung cấp"), { target: { value: "Supplier API" } })
    fireEvent.click(screen.getByRole("button", { name: "Lưu thiết bị" }))

    await waitFor(() => expect(mocks.createEquipment).toHaveBeenCalledWith(
      "admin-token",
      {
        roomId: 11,
        name: "Quạt API",
        installDate: "2026-02-01",
        warrantyExpiry: "2028-02-01",
        supplier: "Supplier API",
        status: "Operational",
      },
    ))
  })

  it("uses API-backed detail, update, and delete actions", async () => {
    render(<ManagementEquipmentPage />)

    fireEvent.click(await screen.findByRole("button", { name: "Xem Máy lạnh API" }))
    expect(await screen.findByText("Chi tiết thiết bị")).toBeTruthy()
    expect(screen.getByText("Còn bảo hành")).toBeTruthy()
    fireEvent.click(screen.getByRole("button", { name: "Đóng" }))

    fireEvent.click(screen.getByRole("button", { name: "Sửa Máy lạnh API" }))
    expect(await screen.findByRole("button", { name: "Lưu thiết bị" })).toBeTruthy()
    fireEvent.change(screen.getByLabelText("Nhà cung cấp"), { target: { value: "Supplier mới" } })
    fireEvent.click(screen.getByRole("button", { name: "Lưu thiết bị" }))
    await waitFor(() => expect(mocks.updateEquipment).toHaveBeenCalled())

    vi.spyOn(window, "confirm").mockReturnValue(true)
    fireEvent.click(screen.getByRole("button", { name: "Xóa Máy lạnh API" }))
    await waitFor(() => expect(mocks.deleteEquipment).toHaveBeenCalledWith("admin-token", 101))
  })
})
