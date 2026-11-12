import { fireEvent, render, screen } from "@testing-library/react"
import { MemoryRouter, Route, Routes } from "react-router"
import { beforeEach, describe, expect, it, vi } from "vitest"
import ViewingRequestPage from "@/pages/ViewingRequestPage/ViewingRequestPage"
import ManagementViewingsPage from "@/pages/ManagementViewingsPage/ManagementViewingsPage"
import { ApiError } from "@/api/apiClient"
import type { ViewingScheduleDto } from "@/types/viewingSchedule"
import type { PublicRoomDetail } from "@/types/publicBrowsing"

const mocks = vi.hoisted(() => ({
  getViewingSchedules: vi.fn(),
  createViewingSchedule: vi.fn(),
  approveViewingSchedule: vi.fn(),
  rejectViewingSchedule: vi.fn(),
  getRoom: vi.fn(),
  getRoles: vi.fn(),
  getUsers: vi.fn(),
  getPropertyManagerAssignments: vi.fn(),
  user: { id: "10", name: "Customer A", phone: "0900", role: "CUSTOMER" as string },
}))

vi.mock("@/auth/AuthContext", () => ({
  useAuth: () => ({ accessToken: "token", user: mocks.user }),
  useOptionalAuth: () => null,
}))
vi.mock("@/auth/CustomerNotificationsContext", () => ({
  useCustomerNotificationsState: () => ({
    notifications: [], unreadCount: 0, loading: false, error: null, refresh: vi.fn(), markRead: vi.fn(),
  }),
}))
vi.mock("@/components/management/ManagementPage", () => ({
  default: ({ children }: { children: React.ReactNode }) => <main>{children}</main>,
}))
vi.mock("@/services/viewingScheduleService", () => ({
  getViewingSchedules: mocks.getViewingSchedules,
  createViewingSchedule: mocks.createViewingSchedule,
  approveViewingSchedule: mocks.approveViewingSchedule,
  rejectViewingSchedule: mocks.rejectViewingSchedule,
}))
vi.mock("@/services/userService", () => ({ getRoles: mocks.getRoles, getUsers: mocks.getUsers }))
vi.mock("@/services/propertyManagerAssignmentService", () => ({ getPropertyManagerAssignments: mocks.getPropertyManagerAssignments }))
vi.mock("@/services/publicBrowsingService", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/services/publicBrowsingService")>()),
  publicApi: { getRoom: mocks.getRoom },
}))

const futureIso = (days: number) => new Date(Date.now() + days * 86_400_000).toISOString()
const futureLocal = "2099-05-20T09:30"

const viewing = (overrides: Partial<ViewingScheduleDto> = {}): ViewingScheduleDto => ({
  viewingId: 3, roomId: 501, roomCode: "A101", propertyId: 1, propertyName: "Sunrise",
  customerId: 10, customerName: "Nguyễn An", customerPhone: "0908123456",
  scheduledAt: futureIso(1), status: "Pending",
  assignedTo: null, assignedManagerName: null, assignedManagerPhone: null,
  managerNote: null, respondedBy: null, respondedAt: null,
  ...overrides,
})

const room: PublicRoomDetail = {
  roomId: 501, roomCode: "A101", areaSqm: 20, roomType: { roomTypeId: 1, roomTypeName: "Studio" },
  baseRent: 3_000_000,
  property: { propertyId: 1, propertyName: "Sunrise", addressText: "", ward: "", district: "", city: "" },
  images: [], amenities: [], services: [], equipments: [],
}

function renderRequestPage() {
  return render(
    <MemoryRouter initialEntries={["/viewing-request?roomId=501"]}>
      <Routes><Route path="/viewing-request" element={<ViewingRequestPage />} /></Routes>
    </MemoryRouter>,
  )
}

beforeEach(() => {
  mocks.approveViewingSchedule.mockReset()
  mocks.rejectViewingSchedule.mockReset()
  mocks.getRoles.mockReset()
  mocks.getUsers.mockReset()
  mocks.getPropertyManagerAssignments.mockReset()
  mocks.getPropertyManagerAssignments.mockResolvedValue([{ propertyId: 1, managerId: 7, managerIsActive: true }])
  mocks.user = { id: "10", name: "Customer A", phone: "0900", role: "CUSTOMER" }
  mocks.getRoom.mockResolvedValue(room)
  mocks.getViewingSchedules.mockResolvedValue([])
  mocks.getRoles.mockResolvedValue([{ roleId: 2, roleName: "Manager" }])
  mocks.getUsers.mockResolvedValue([{ userId: 7, fullName: "Manager A", phoneNumber: "0911", roleName: "Manager", isActive: true }])
})

describe("ViewingRequestPage", () => {
  it("requires a future time and sends the chosen local time as UTC", async () => {
    mocks.createViewingSchedule.mockResolvedValue(viewing({ scheduledAt: new Date(futureLocal).toISOString() }))
    renderRequestPage()
    await screen.findByText(/A101 · /)

    fireEvent.click(screen.getByRole("button", { name: "Gửi yêu cầu xem phòng" }))
    expect(screen.getByRole("alert").textContent).toBe("Vui lòng chọn thời điểm xem trong tương lai.")
    expect(mocks.createViewingSchedule).not.toHaveBeenCalled()

    fireEvent.change(screen.getByLabelText("Ngày giờ xem phòng"), { target: { value: futureLocal } })
    fireEvent.click(screen.getByRole("button", { name: "Gửi yêu cầu xem phòng" }))
    expect(await screen.findByText(/Đã gửi yêu cầu xem phòng A101/)).toBeTruthy()
    expect(mocks.createViewingSchedule).toHaveBeenCalledWith("token", { roomId: 501, scheduledAt: new Date(futureLocal).toISOString() })
  })

  it("lets the owner decide the time and explains a conflict", async () => {
    mocks.createViewingSchedule.mockRejectedValue(new ApiError("Duplicate", { status: 409 }))
    renderRequestPage()
    fireEvent.click(await screen.findByLabelText("Để Admin phân công lịch"))

    expect(screen.queryByLabelText("Ngày giờ xem phòng")).toBeNull()
    fireEvent.click(screen.getByRole("button", { name: "Gửi yêu cầu xem phòng" }))

    expect((await screen.findByRole("alert")).textContent).toBe("Phòng không còn trống hoặc bạn đã có lịch xem đang chờ cho phòng này.")
    expect(mocks.createViewingSchedule).toHaveBeenCalledWith("token", { roomId: 501, scheduledAt: null })
  })
})

describe("ManagementViewingsPage", () => {
  it("only lists active Managers assigned to the viewing property", async () => {
    mocks.user.role = "ADMIN"
    mocks.getViewingSchedules.mockResolvedValue([viewing()])
    mocks.getUsers.mockResolvedValue([
      { userId: 7, fullName: "Manager A", roleName: "Manager", isActive: true },
      { userId: 8, fullName: "Other property Manager", roleName: "Manager", isActive: true },
      { userId: 9, fullName: "Inactive Manager", roleName: "Manager", isActive: false },
    ])
    mocks.getPropertyManagerAssignments.mockResolvedValue([
      { propertyId: 1, managerId: 7, managerIsActive: true },
      { propertyId: 1, managerId: 9, managerIsActive: false },
    ])
    render(<MemoryRouter><ManagementViewingsPage /></MemoryRouter>)

    fireEvent.click(await screen.findByRole("button", { name: "Phân công & duyệt" }))
    await screen.findByRole("option", { name: "Manager A" })
    expect(mocks.getPropertyManagerAssignments).toHaveBeenCalledWith("token", 1)
    expect(screen.queryByRole("option", { name: "Other property Manager" })).toBeNull()
    expect(screen.queryByRole("option", { name: "Inactive Manager" })).toBeNull()
  })

  it("prevents approval when the property has no eligible Manager", async () => {
    mocks.user.role = "ADMIN"
    mocks.getViewingSchedules.mockResolvedValue([viewing()])
    mocks.getPropertyManagerAssignments.mockResolvedValue([])
    render(<MemoryRouter><ManagementViewingsPage /></MemoryRouter>)

    fireEvent.click(await screen.findByRole("button", { name: "Phân công & duyệt" }))
    await screen.findByText("Khu trọ này chưa có Manager đang hoạt động được phân quyền.")
    expect(screen.queryByRole("option", { name: /Manager A/ })).toBeNull()
    expect((screen.getByRole("button", { name: "Xác nhận & thông báo" }) as HTMLButtonElement).disabled).toBe(true)
    expect(mocks.approveViewingSchedule).not.toHaveBeenCalled()
  })

  it("reloads eligible Managers when opening a viewing for another property", async () => {
    mocks.user.role = "ADMIN"
    mocks.getViewingSchedules.mockResolvedValue([
      viewing(), viewing({ viewingId: 4, propertyId: 2, propertyName: "Other property" }),
    ])
    mocks.getPropertyManagerAssignments
      .mockResolvedValueOnce([{ propertyId: 1, managerId: 7, managerIsActive: true }])
      .mockResolvedValueOnce([])
    render(<MemoryRouter><ManagementViewingsPage /></MemoryRouter>)

    fireEvent.click((await screen.findAllByRole("button", { name: "Phân công & duyệt" }))[0])
    await screen.findByRole("option", { name: "Manager A · 0911" })
    fireEvent.click(screen.getByRole("button", { name: "Huỷ" }))
    fireEvent.click(screen.getAllByRole("button", { name: "Phân công & duyệt" })[1])

    await screen.findByText("Khu trọ này chưa có Manager đang hoạt động được phân quyền.")
    expect(mocks.getPropertyManagerAssignments).toHaveBeenLastCalledWith("token", 2)
    expect(screen.queryByRole("option", { name: /Manager A/ })).toBeNull()
  })

  it("blocks assignment if property permissions cannot be loaded", async () => {
    mocks.user.role = "ADMIN"
    mocks.getViewingSchedules.mockResolvedValue([viewing()])
    mocks.getPropertyManagerAssignments.mockRejectedValue(new Error("Không thể tải phân quyền khu trọ."))
    render(<MemoryRouter><ManagementViewingsPage /></MemoryRouter>)

    fireEvent.click(await screen.findByRole("button", { name: "Phân công & duyệt" }))
    expect((await screen.findByRole("alert")).textContent).toBe("Không thể tải phân quyền khu trọ.")
    expect(screen.queryByRole("option", { name: /Manager A/ })).toBeNull()
    expect((screen.getByRole("button", { name: "Xác nhận & thông báo" }) as HTMLButtonElement).disabled).toBe(true)
  })

  it("lets only Admin assign an active Manager while confirming a future time and note", async () => {
    mocks.user = { id: "9", name: "Admin", phone: "0900", role: "ADMIN" }
    mocks.getViewingSchedules.mockResolvedValue([
      viewing({ scheduledAt: null }),
    ])
    mocks.approveViewingSchedule.mockResolvedValue(viewing({ status: "Approved", assignedTo: 7, assignedManagerName: "Manager A" }))
    render(<MemoryRouter><ManagementViewingsPage /></MemoryRouter>)

    await screen.findByText("Nguyễn An")
    expect(screen.getByText("Chờ Admin phân lịch")).toBeTruthy()
    fireEvent.click(screen.getByRole("button", { name: "Phân công & duyệt" }))
    expect(await screen.findByRole("option", { name: "Manager A · 0911" })).toBeTruthy()

    fireEvent.change(screen.getByLabelText("Manager phụ trách"), { target: { value: "7" } })
    fireEvent.change(screen.getByLabelText("Thời gian xác nhận"), { target: { value: futureLocal } })
    fireEvent.change(screen.getByLabelText("Ghi chú cho Manager / khách (không bắt buộc)"), { target: { value: "Use the side entrance." } })
    fireEvent.click(screen.getByRole("button", { name: "Xác nhận & thông báo" }))

    await screen.findByText(/Đã phân công Manager/)
    expect(mocks.approveViewingSchedule).toHaveBeenCalledWith("token", 3, {
      scheduledAt: new Date(futureLocal).toISOString(),
      assignedTo: 7,
      managerNote: "Use the side entrance.",
    })
  })

  it("keeps Manager view read-only and scopes decision actions to Admin", async () => {
    mocks.user = { id: "7", name: "Manager A", phone: "0911", role: "MANAGER" }
    mocks.getViewingSchedules.mockResolvedValue([viewing({ status: "Approved", assignedTo: 7, assignedManagerName: "Manager A" })])
    render(<MemoryRouter><ManagementViewingsPage /></MemoryRouter>)

    await screen.findByText("Nguyễn An")
    expect(screen.queryByRole("button", { name: "Phân công & duyệt" })).toBeNull()
    expect(screen.queryByRole("button", { name: "Từ chối" })).toBeNull()
  })

  it("requires a rejection reason before notifying the customer", async () => {
    mocks.user = { id: "9", name: "Admin", phone: "0900", role: "ADMIN" }
    mocks.getViewingSchedules.mockResolvedValue([viewing()])
    mocks.rejectViewingSchedule.mockResolvedValue(viewing({ status: "Rejected" }))
    render(<MemoryRouter><ManagementViewingsPage /></MemoryRouter>)

    fireEvent.click(await screen.findByRole("button", { name: "Từ chối" }))
    fireEvent.click(screen.getByRole("button", { name: "Từ chối & thông báo" }))
    expect(screen.getByRole("alert").textContent).toBe("Vui lòng nhập lý do từ chối.")
    expect(mocks.rejectViewingSchedule).not.toHaveBeenCalled()
    fireEvent.change(screen.getByLabelText("Lý do từ chối"), { target: { value: "No available staff." } })
    fireEvent.click(screen.getByRole("button", { name: "Từ chối & thông báo" }))

    await screen.findByText("Đã từ chối")
    expect(mocks.rejectViewingSchedule).toHaveBeenCalledWith("token", 3, "No available staff.")
  })
})
