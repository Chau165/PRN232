import { fireEvent, render, screen, waitFor } from "@testing-library/react"
import { MemoryRouter, Route, Routes } from "react-router"
import type { ReactNode } from "react"
import { beforeEach, describe, expect, it, vi } from "vitest"
import { ApiError } from "@/api/apiClient"
import ManagementRoomEquipmentPage from "@/pages/ManagementRoomEquipmentPage/ManagementRoomEquipmentPage"
import { managementRoomEquipmentPath, routes } from "@/constants/routes"

const mocks = vi.hoisted(() => ({
  getManagementRoom: vi.fn(),
  getEquipments: vi.fn(),
  getEquipment: vi.fn(),
  createEquipment: vi.fn(),
  updateEquipment: vi.fn(),
  deleteEquipment: vi.fn(),
}))

vi.mock("@/auth/AuthContext", () => ({
  useAuth: () => ({
    accessToken: "admin-token",
    user: { id: "7", name: "Admin", role: "ADMIN" },
  }),
}))

vi.mock("@/components/management/ManagementPage", () => ({
  default: ({
    title,
    children,
  }: { title: string; children: ReactNode }) => (
    <main>
      <h1>{title}</h1>
      {children}
    </main>
  ),
}))

vi.mock("@/services/roomService", () => ({
  getManagementRoom: mocks.getManagementRoom,
}))

vi.mock("@/services/equipmentService", () => ({
  getEquipments: mocks.getEquipments,
  getEquipment: mocks.getEquipment,
  createEquipment: mocks.createEquipment,
  updateEquipment: mocks.updateEquipment,
  deleteEquipment: mocks.deleteEquipment,
}))

const room = {
  roomId: 31,
  propertyId: 21,
  propertyName: "Nhà trọ Bình An",
  roomTypeId: 4,
  roomTypeName: "Studio",
  roomTypePrice: 3_000_000,
  roomCode: "BA-A101",
  areaSqm: 24.5,
  status: "Available",
}

const equipment = {
  equipmentId: 101,
  roomId: 31,
  roomCode: "BA-A101",
  propertyId: 21,
  propertyName: "Nhà trọ Bình An",
  name: "Máy lạnh API",
  installDate: "2026-01-15",
  warrantyExpiry: "2028-01-15",
  supplier: "Nhà cung cấp API",
  status: "Operational",
}

function renderPage(path = "/management/properties/21/rooms/31/equipment") {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route
          path={routes.managementRoomEquipment}
          element={<ManagementRoomEquipmentPage />}
        />
      </Routes>
    </MemoryRouter>,
  )
}

beforeEach(() => {
  vi.clearAllMocks()
  mocks.getManagementRoom.mockResolvedValue(room)
  mocks.getEquipments.mockResolvedValue([equipment])
  mocks.getEquipment.mockResolvedValue(equipment)
  mocks.createEquipment.mockResolvedValue(equipment)
  mocks.updateEquipment.mockResolvedValue(equipment)
  mocks.deleteEquipment.mockResolvedValue(undefined)
})

describe("room-scoped equipment management", () => {
  it("validates IDs when building the room equipment URL", () => {
    expect(managementRoomEquipmentPath(21, 31)).toBe(
      "/management/properties/21/rooms/31/equipment",
    )
    expect(() => managementRoomEquipmentPath(0, 31)).toThrow(RangeError)
    expect(() => managementRoomEquipmentPath(21, -1)).toThrow(RangeError)
  })

  it("validates room context before loading its equipment", async () => {
    renderPage()

    expect(
      await screen.findByRole("heading", {
        name: "Thiết bị — Phòng BA-A101",
      }),
    ).toBeTruthy()
    expect(screen.getAllByText("Nhà trọ Bình An")).toHaveLength(2)
    expect(mocks.getManagementRoom).toHaveBeenCalledWith(
      "admin-token",
      31,
      expect.any(AbortSignal),
    )
    expect(mocks.getEquipments).toHaveBeenCalledWith(
      "admin-token",
      {
        roomId: 31,
        status: undefined,
        search: "",
        page: 1,
        pageSize: 50,
      },
      expect.any(AbortSignal),
    )
    expect(mocks.getManagementRoom.mock.invocationCallOrder[0]).toBeLessThan(
      mocks.getEquipments.mock.invocationCallOrder[0],
    )
  })

  it("does not load equipment when the URL property and room do not match", async () => {
    mocks.getManagementRoom.mockResolvedValue({
      ...room,
      propertyId: 99,
    })

    renderPage()

    expect(
      await screen.findByText("Phòng không thuộc khu trọ trong đường dẫn."),
    ).toBeTruthy()
    expect(mocks.getEquipments).not.toHaveBeenCalled()
  })

  it("never renders an equipment row returned for a different room", async () => {
    mocks.getEquipments.mockResolvedValue([
      { ...equipment, roomId: 32, name: "Thiết bị phòng khác" },
    ])
    renderPage()

    expect(
      await screen.findByText(
        "Dữ liệu thiết bị trả về không thuộc phòng đang mở.",
      ),
    ).toBeTruthy()
    expect(screen.queryByText("Thiết bị phòng khác")).toBeNull()
  })

  it("rejects invalid route IDs before calling room or equipment APIs", async () => {
    renderPage("/management/properties/0/rooms/31/equipment")

    expect(
      await screen.findByText("Đường dẫn khu trọ hoặc phòng không hợp lệ."),
    ).toBeTruthy()
    expect(mocks.getManagementRoom).not.toHaveBeenCalled()
    expect(mocks.getEquipments).not.toHaveBeenCalled()
  })

  it("creates equipment with the room ID from the validated route", async () => {
    mocks.getEquipments.mockResolvedValue([])
    renderPage()

    fireEvent.click(
      await screen.findByRole("button", { name: "+ Thêm thiết bị" }),
    )
    expect(
      screen.getByRole("heading", {
        name: "Thêm thiết bị cho phòng BA-A101",
      }),
    ).toBeTruthy()
    expect(screen.getByLabelText("Phòng")).toHaveProperty("readOnly", true)
    expect(screen.queryByLabelText("Khu trọ")).toBeNull()

    fireEvent.change(screen.getByLabelText("Tên thiết bị"), {
      target: { value: "Quạt API" },
    })
    fireEvent.change(screen.getByLabelText("Ngày lắp đặt"), {
      target: { value: "2026-02-01" },
    })
    fireEvent.change(screen.getByLabelText("Hạn bảo hành"), {
      target: { value: "2028-02-01" },
    })
    fireEvent.change(screen.getByLabelText("Nhà cung cấp"), {
      target: { value: "Supplier API" },
    })
    fireEvent.click(screen.getByRole("button", { name: "Lưu thiết bị" }))

    await waitFor(() =>
      expect(mocks.createEquipment).toHaveBeenCalledWith("admin-token", {
        roomId: 31,
        name: "Quạt API",
        installDate: "2026-02-01",
        warrantyExpiry: "2028-02-01",
        supplier: "Supplier API",
        status: "Operational",
      }),
    )
  })

  it("blocks a detail response belonging to another room", async () => {
    mocks.getEquipment.mockResolvedValue({
      ...equipment,
      roomId: 32,
    })
    renderPage()

    fireEvent.click(
      await screen.findByRole("button", { name: "Xem Máy lạnh API" }),
    )

    expect(
      await screen.findByText("Thiết bị này không thuộc phòng đang mở."),
    ).toBeTruthy()
    expect(screen.queryByText("Chi tiết thiết bị")).toBeNull()
  })

  it("updates the selected equipment without sending a room ID", async () => {
    renderPage()

    fireEvent.click(
      await screen.findByRole("button", { name: "Sửa Máy lạnh API" }),
    )
    fireEvent.change(await screen.findByLabelText("Nhà cung cấp"), {
      target: { value: "Supplier mới" },
    })
    fireEvent.click(screen.getByRole("button", { name: "Lưu thiết bị" }))

    await waitFor(() =>
      expect(mocks.updateEquipment).toHaveBeenCalledWith("admin-token", 101, {
        name: "Máy lạnh API",
        installDate: "2026-01-15",
        warrantyExpiry: "2028-01-15",
        supplier: "Supplier mới",
        status: "Operational",
      }),
    )
  })

  it("shows the maintenance-history message for HTTP 409", async () => {
    mocks.deleteEquipment.mockRejectedValue(
      new ApiError("Conflict", { status: 409 }),
    )
    vi.spyOn(window, "confirm").mockReturnValue(true)
    renderPage()

    fireEvent.click(
      await screen.findByRole("button", { name: "Xóa Máy lạnh API" }),
    )

    expect(
      await screen.findByText(
        "Không thể xóa thiết bị vì đã có lịch sử bảo trì liên quan.",
      ),
    ).toBeTruthy()
    expect(mocks.deleteEquipment).toHaveBeenCalledWith("admin-token", 101)
  })

  it("returns to the previous page after deleting its last record", async () => {
    const firstPage = Array.from({ length: 50 }, (_, index) => ({
      ...equipment,
      equipmentId: 200 + index,
      name: "Trang một " + index,
    }))
    mocks.getEquipments
      .mockReset()
      .mockResolvedValueOnce(firstPage)
      .mockResolvedValueOnce([equipment])
      .mockResolvedValueOnce(firstPage)
    vi.spyOn(window, "confirm").mockReturnValue(true)

    renderPage()
    expect(await screen.findByText("Trang một 0")).toBeTruthy()
    fireEvent.click(screen.getByRole("button", { name: "Trang sau" }))
    expect(await screen.findByText("Máy lạnh API")).toBeTruthy()
    fireEvent.click(screen.getByRole("button", { name: "Xóa Máy lạnh API" }))

    expect(await screen.findByText("Trang một 0")).toBeTruthy()
    expect(
      await screen.findByText("Đã xóa thiết bị Máy lạnh API."),
    ).toBeTruthy()
    expect(mocks.getEquipments).toHaveBeenLastCalledWith(
      "admin-token",
      expect.objectContaining({ roomId: 31, page: 1, pageSize: 50 }),
      expect.any(AbortSignal),
    )
  })

  it("shows an empty state and retries after a list error", async () => {
    mocks.getEquipments
      .mockRejectedValueOnce(new ApiError("Network failure", { status: 500 }))
      .mockResolvedValueOnce([])
    renderPage()

    expect(await screen.findByRole("alert")).toBeTruthy()
    fireEvent.click(screen.getByRole("button", { name: "Tải lại" }))

    expect(
      await screen.findByText("Phòng này chưa có thiết bị nào."),
    ).toBeTruthy()
  })

  it("resets search and status changes to page one", async () => {
    const firstPage = Array.from({ length: 50 }, (_, index) => ({
      ...equipment,
      equipmentId: 200 + index,
      name: "Trang một " + index,
    }))
    mocks.getEquipments
      .mockReset()
      .mockResolvedValueOnce(firstPage)
      .mockResolvedValueOnce([{ ...equipment, name: "Trang hai" }])
      .mockResolvedValueOnce([{ ...equipment, name: "Kết quả tìm kiếm" }])
      .mockResolvedValueOnce([{ ...equipment, name: "Theo trạng thái" }])

    renderPage()
    expect(await screen.findByText("Trang một 0")).toBeTruthy()
    fireEvent.click(screen.getByRole("button", { name: "Trang sau" }))
    expect(await screen.findByText("Trang hai")).toBeTruthy()

    fireEvent.change(screen.getByLabelText("Tìm thiết bị"), {
      target: { value: "máy lạnh" },
    })
    expect(await screen.findByText("Kết quả tìm kiếm")).toBeTruthy()
    fireEvent.change(screen.getByLabelText("Lọc trạng thái"), {
      target: { value: "Operational" },
    })
    expect(await screen.findByText("Theo trạng thái")).toBeTruthy()

    expect(mocks.getEquipments).toHaveBeenLastCalledWith(
      "admin-token",
      {
        roomId: 31,
        status: "Operational",
        search: "máy lạnh",
        page: 1,
        pageSize: 50,
      },
      expect.any(AbortSignal),
    )
  })
})
