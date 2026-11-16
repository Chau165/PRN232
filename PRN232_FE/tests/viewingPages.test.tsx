import { fireEvent, render, screen } from "@testing-library/react"
import { MemoryRouter, Route, Routes } from "react-router"
import { beforeEach, describe, expect, it, vi } from "vitest"
import ViewingRequestPage from "@/pages/ViewingRequestPage/ViewingRequestPage"
import ManagementViewingsPage from "@/pages/ManagementViewingsPage/ManagementViewingsPage"
import PublicRoomTypeCards from "@/components/property/PublicRoomTypeCards"
import { ApiError } from "@/api/apiClient"
import type { ViewingScheduleDto } from "@/types/viewingSchedule"
import type { PublicRoomDetail } from "@/types/publicBrowsing"

const mocks = vi.hoisted(() => ({
  getViewingSchedules: vi.fn(),
  createViewingSchedule: vi.fn(),
  approveViewingSchedule: vi.fn(),
  rejectViewingSchedule: vi.fn(),
  getAvailableViewingRooms: vi.fn(),
  selectViewingRoom: vi.fn(),
  getRoom: vi.fn(),
  listRoomTypes: vi.fn(),
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
  getAvailableViewingRooms: mocks.getAvailableViewingRooms,
  selectViewingRoom: mocks.selectViewingRoom,
}))
vi.mock("@/services/publicBrowsingService", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/services/publicBrowsingService")>()),
  publicApi: { getRoom: mocks.getRoom, listRoomTypes: mocks.listRoomTypes },
}))

const futureIso = (days: number) => new Date(Date.now() + days * 86_400_000).toISOString()
const futureLocal = "2099-05-20T09:30"

const viewing = (overrides: Partial<ViewingScheduleDto> = {}): ViewingScheduleDto => ({
  viewingId: 3, roomId: 501, roomCode: "A101", propertyId: 1, propertyName: "Sunrise",
  roomTypeId: 1, roomTypeName: "Studio", currentMonthlyRent: 3_000_000,
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
  mocks.createViewingSchedule.mockReset()
  mocks.rejectViewingSchedule.mockReset()
  mocks.getAvailableViewingRooms.mockReset()
  mocks.selectViewingRoom.mockReset()
  mocks.user = { id: "10", name: "Customer A", phone: "0900", role: "CUSTOMER" }
  mocks.getRoom.mockResolvedValue(room)
  mocks.listRoomTypes.mockResolvedValue([{
    propertyId: 1,
    propertyName: "Sunrise",
    roomTypeId: 3,
    roomTypeName: "Premium",
    monthlyRent: 3_000_000,
    availableRoomCount: 2,
    totalRoomCount: 4,
    minimumAreaSqm: 18,
    maximumAreaSqm: 22,
    referenceImageUrl: null,
  }])
  mocks.getViewingSchedules.mockResolvedValue([])
})

describe("ViewingRequestPage", () => {
  it("opens from a room type card and submits the selected property and room type", async () => {
    mocks.createViewingSchedule.mockResolvedValue(viewing({ roomId: null, roomCode: null }))
    render(
      <MemoryRouter initialEntries={["/property/1/rooms"]}>
        <Routes>
          <Route path="/property/:propertyId/rooms" element={<PublicRoomTypeCards propertyId={1} />} />
          <Route path="/viewing-request" element={<ViewingRequestPage />} />
        </Routes>
      </MemoryRouter>,
    )

    fireEvent.click(await screen.findByRole("link", { name: "Đặt lịch xem" }))
    expect(await screen.findByLabelText("Ngày giờ xem phòng")).toBeTruthy()
    fireEvent.click(screen.getByLabelText("Để Manager khu trọ sắp xếp thời gian"))
    fireEvent.click(screen.getByRole("button", { name: "Gửi yêu cầu xem loại phòng" }))

    expect(await screen.findByText(/Chưa có phòng cụ thể được giữ/)).toBeTruthy()
    expect(screen.getByText(/Manager của khu trọ sẽ xem xét và xác nhận lịch/)).toBeTruthy()
    expect(mocks.createViewingSchedule).toHaveBeenCalledWith("token", {
      propertyId: 1,
      roomTypeId: 3,
      scheduledAt: null,
    })
  })

  it("creates a viewing by property and room type without sending a RoomId", async () => {
    mocks.createViewingSchedule.mockResolvedValue(viewing({ roomId: null, roomCode: null }))
    render(
      <MemoryRouter initialEntries={["/viewing-request?propertyId=1&roomTypeId=3"]}>
        <Routes><Route path="/viewing-request" element={<ViewingRequestPage />} /></Routes>
      </MemoryRouter>,
    )

    await screen.findByText("Premium")
    fireEvent.click(screen.getByLabelText("Để Manager khu trọ sắp xếp thời gian"))
    fireEvent.click(screen.getByRole("button", { name: "Gửi yêu cầu xem loại phòng" }))

    expect(await screen.findByText(/Chưa có phòng cụ thể được giữ/)).toBeTruthy()
    expect(screen.getByText(/Manager của khu trọ sẽ xem xét và xác nhận lịch/)).toBeTruthy()
    expect(mocks.createViewingSchedule).toHaveBeenCalledWith("token", {
      propertyId: 1,
      roomTypeId: 3,
      scheduledAt: null,
    })
  })

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
    expect(screen.getByText(/Manager của khu trọ sẽ xem xét và xác nhận lịch/)).toBeTruthy()
    expect(mocks.createViewingSchedule).toHaveBeenCalledWith("token", { roomId: 501, scheduledAt: new Date(futureLocal).toISOString() })
  })

  it("lets the owner decide the time and explains a conflict", async () => {
    mocks.createViewingSchedule.mockRejectedValue(new ApiError("Duplicate", { status: 409 }))
    renderRequestPage()
    fireEvent.click(await screen.findByLabelText("Để Manager khu trọ sắp xếp thời gian"))

    expect(screen.queryByLabelText("Ngày giờ xem phòng")).toBeNull()
    fireEvent.click(screen.getByRole("button", { name: "Gửi yêu cầu xem phòng" }))

    expect((await screen.findByRole("alert")).textContent).toBe("Phòng không còn trống hoặc bạn đã có lịch xem đang chờ cho phòng này.")
    expect(mocks.createViewingSchedule).toHaveBeenCalledWith("token", { roomId: 501, scheduledAt: null })
  })
})

describe("ManagementViewingsPage", () => {
  it("lets a Manager directly approve a pending request with a confirmed time and note", async () => {
    mocks.user = { id: "7", name: "Manager A", phone: "0911", role: "MANAGER" }
    mocks.getViewingSchedules.mockResolvedValue([viewing({ scheduledAt: null })])
    mocks.approveViewingSchedule.mockResolvedValue(viewing({ status: "Approved", assignedTo: 7, assignedManagerName: "Manager A" }))
    render(<MemoryRouter><ManagementViewingsPage /></MemoryRouter>)

    await screen.findByText("Nguyễn An")
    expect(screen.getByText("Chờ Manager khu trọ sắp xếp thời gian")).toBeTruthy()
    fireEvent.click(screen.getByRole("button", { name: "Chấp thuận" }))
    expect(await screen.findByRole("heading", { name: "Chấp thuận lịch xem" })).toBeTruthy()
    expect(screen.queryByLabelText("Manager phụ trách")).toBeNull()
    fireEvent.change(screen.getByLabelText("Thời gian xác nhận"), { target: { value: futureLocal } })
    fireEvent.change(screen.getByLabelText("Ghi chú cho khách (không bắt buộc)"), { target: { value: "Use the side entrance." } })
    fireEvent.click(screen.getByRole("button", { name: "Chấp thuận & thông báo" }))

    await screen.findByText("Đã chấp thuận lịch xem Studio.")
    expect(mocks.approveViewingSchedule).toHaveBeenCalledWith("token", 3, {
      scheduledAt: new Date(futureLocal).toISOString(),
      managerNote: "Use the side entrance.",
    })
  })

  it("keeps Admin read-only while showing pending requests for supervision", async () => {
    mocks.user = { id: "9", name: "Admin", phone: "0900", role: "ADMIN" }
    mocks.getViewingSchedules.mockResolvedValue([viewing()])
    render(<MemoryRouter><ManagementViewingsPage /></MemoryRouter>)

    await screen.findByText("Nguyễn An")
    expect(screen.queryByRole("button", { name: "Chấp thuận" })).toBeNull()
    expect(screen.queryByRole("button", { name: "Từ chối" })).toBeNull()
    expect(screen.queryByRole("button", { name: "Phân công & duyệt" })).toBeNull()
  })

  it("lets the assigned Manager select only a currently available room", async () => {
    mocks.user = { id: "7", name: "Manager A", phone: "0911", role: "MANAGER" }
    mocks.getViewingSchedules.mockResolvedValue([viewing({
      status: "Approved",
      assignedTo: 7,
      assignedManagerName: "Manager A",
      roomId: null,
      roomCode: null,
    })])
    mocks.getAvailableViewingRooms.mockResolvedValue([
      { roomId: 502, roomCode: "A-102", areaSqm: 21, status: "Available" },
    ])
    mocks.selectViewingRoom.mockResolvedValue(viewing({
      status: "Approved",
      assignedTo: 7,
      roomId: 502,
      roomCode: "A-102",
    }))
    render(<MemoryRouter><ManagementViewingsPage /></MemoryRouter>)

    fireEvent.click(await screen.findByRole("button", { name: "Chọn phòng dẫn khách xem" }))
    await screen.findByRole("option", { name: "A-102 · 21 m² · Available" })
    expect(mocks.getAvailableViewingRooms).toHaveBeenCalledWith("token", 3)
    fireEvent.change(screen.getByLabelText("Phòng đủ điều kiện"), { target: { value: "502" } })
    fireEvent.click(screen.getByRole("button", { name: "Xác nhận phòng" }))

    await screen.findByText("Đã xác nhận phòng dẫn khách xem.")
    expect(mocks.selectViewingRoom).toHaveBeenCalledWith("token", 3, 502)
  })

  it("does not let a second Manager select a room approved by the first Manager", async () => {
    mocks.user = { id: "8", name: "Manager B", phone: "0912", role: "MANAGER" }
    mocks.getViewingSchedules.mockResolvedValue([viewing({
      status: "Approved",
      assignedTo: 7,
      assignedManagerName: "Manager A",
      roomId: null,
      roomCode: null,
    })])
    render(<MemoryRouter><ManagementViewingsPage /></MemoryRouter>)

    expect(await screen.findByText("Manager Manager A phụ trách chọn phòng.")).toBeTruthy()
    expect(screen.queryByRole("button", { name: "Chọn phòng dẫn khách xem" })).toBeNull()
  })

  it("requires a rejection reason before notifying the customer", async () => {
    mocks.user = { id: "7", name: "Manager A", phone: "0911", role: "MANAGER" }
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

  it("explains 403 and 409 decision errors and refreshes stale rows", async () => {
    mocks.user = { id: "7", name: "Manager A", phone: "0911", role: "MANAGER" }
    mocks.getViewingSchedules.mockResolvedValue([viewing()])
    mocks.approveViewingSchedule.mockRejectedValueOnce(new ApiError("Forbidden", { status: 403 }))
    render(<MemoryRouter><ManagementViewingsPage /></MemoryRouter>)

    fireEvent.click(await screen.findByRole("button", { name: "Chấp thuận" }))
    fireEvent.change(screen.getByLabelText("Thời gian xác nhận"), { target: { value: futureLocal } })
    fireEvent.click(screen.getByRole("button", { name: "Chấp thuận & thông báo" }))
    expect(await screen.findByText("Bạn không có quyền xử lý lịch này hoặc không còn được phân công quản lý khu trọ.")).toBeTruthy()

    mocks.approveViewingSchedule.mockRejectedValueOnce(new ApiError("Conflict", { status: 409 }))
    fireEvent.click(await screen.findByRole("button", { name: "Chấp thuận" }))
    fireEvent.change(screen.getByLabelText("Thời gian xác nhận"), { target: { value: futureLocal } })
    fireEvent.click(screen.getByRole("button", { name: "Chấp thuận & thông báo" }))
    expect(await screen.findByText("Lịch xem vừa được xử lý hoặc đã thay đổi. Danh sách đã được tải lại.")).toBeTruthy()
  })
})
