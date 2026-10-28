import { fireEvent, render, screen } from "@testing-library/react"
import { MemoryRouter, Route, Routes } from "react-router"
import { beforeEach, describe, expect, it, vi } from "vitest"
import ViewingRequestPage from "@/pages/ViewingRequestPage/ViewingRequestPage"
import ManagementViewingsPage from "@/pages/ManagementViewingsPage/ManagementViewingsPage"
import { ApiError } from "@/api/apiClient"
import type { ViewingScheduleDto } from "@/api/viewingSchedulesApi"
import type { PublicRoomDetail } from "@/api/publicBrowsingApi"

const mocks = vi.hoisted(() => ({
  getViewingSchedules: vi.fn(),
  createViewingSchedule: vi.fn(),
  approveViewingSchedule: vi.fn(),
  rejectViewingSchedule: vi.fn(),
  getRoom: vi.fn(),
}))

vi.mock("@/auth/AuthContext", () => ({
  useAuth: () => ({ accessToken: "token", user: { id: "10", name: "Customer A", phone: "0900", role: "CUSTOMER" } }),
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
vi.mock("@/api/viewingSchedulesApi", () => ({
  getViewingSchedules: mocks.getViewingSchedules,
  createViewingSchedule: mocks.createViewingSchedule,
  approveViewingSchedule: mocks.approveViewingSchedule,
  rejectViewingSchedule: mocks.rejectViewingSchedule,
}))
vi.mock("@/api/publicBrowsingApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/api/publicBrowsingApi")>()),
  publicApi: { getRoom: mocks.getRoom },
}))

const futureIso = (days: number) => new Date(Date.now() + days * 86_400_000).toISOString()
const futureLocal = "2099-05-20T09:30"

const viewing = (overrides: Partial<ViewingScheduleDto> = {}): ViewingScheduleDto => ({
  viewingId: 3, roomId: 501, roomCode: "A101", propertyId: 1, propertyName: "Sunrise",
  customerId: 10, customerName: "Nguyễn An", customerPhone: "0908123456",
  scheduledAt: futureIso(1), status: "Pending",
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
  mocks.getRoom.mockResolvedValue(room)
  mocks.getViewingSchedules.mockResolvedValue([])
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
    fireEvent.click(await screen.findByLabelText("Để chủ trọ sắp xếp"))

    expect(screen.queryByLabelText("Ngày giờ xem phòng")).toBeNull()
    fireEvent.click(screen.getByRole("button", { name: "Gửi yêu cầu xem phòng" }))

    expect((await screen.findByRole("alert")).textContent).toBe("Phòng không còn trống hoặc bạn đã có lịch xem đang chờ cho phòng này.")
    expect(mocks.createViewingSchedule).toHaveBeenCalledWith("token", { roomId: 501, scheduledAt: null })
  })
})

describe("ManagementViewingsPage", () => {
  it("approves a requested time directly and needs a picked time when the owner decides", async () => {
    mocks.getViewingSchedules.mockResolvedValue([
      viewing(),
      viewing({ viewingId: 4, customerName: "Trần Khang", roomCode: "B102", scheduledAt: null }),
    ])
    mocks.approveViewingSchedule
      .mockResolvedValueOnce(viewing({ status: "Approved" }))
      .mockResolvedValueOnce(viewing({
        viewingId: 4, customerName: "Trần Khang", roomCode: "B102", status: "Approved",
        scheduledAt: new Date(futureLocal).toISOString(),
      }))
    render(<MemoryRouter><ManagementViewingsPage /></MemoryRouter>)

    await screen.findByText("Nguyễn An")
    expect(screen.getByText("Chủ trọ sắp xếp")).toBeTruthy()
    expect(screen.getAllByRole("button", { name: "Chấp thuận" })).toHaveLength(1)

    fireEvent.click(screen.getByRole("button", { name: "Chấp thuận" }))
    await screen.findByText("Đã chấp thuận lịch xem phòng của Nguyễn An.")
    expect(mocks.approveViewingSchedule).toHaveBeenLastCalledWith("token", 3, null)

    fireEvent.click(screen.getByRole("button", { name: "Chọn lịch" }))
    fireEvent.change(screen.getByLabelText("Ngày giờ xem phòng"), { target: { value: futureLocal } })
    fireEvent.click(screen.getByRole("button", { name: "Lưu lịch" }))
    await screen.findByText("Đã hẹn Trần Khang xem phòng B102.")
    expect(mocks.approveViewingSchedule).toHaveBeenLastCalledWith("token", 4, new Date(futureLocal).toISOString())
    expect(screen.getAllByText("Đã chấp thuận")).toHaveLength(2)
    expect(screen.queryByRole("dialog")).toBeNull()
  })

  it("rejects only after confirmation", async () => {
    mocks.getViewingSchedules.mockResolvedValue([viewing()])
    mocks.rejectViewingSchedule.mockResolvedValue(viewing({ status: "Rejected" }))
    vi.spyOn(window, "confirm").mockReturnValueOnce(false).mockReturnValueOnce(true)
    render(<MemoryRouter><ManagementViewingsPage /></MemoryRouter>)

    fireEvent.click(await screen.findByRole("button", { name: "Từ chối" }))
    expect(mocks.rejectViewingSchedule).not.toHaveBeenCalled()
    fireEvent.click(screen.getByRole("button", { name: "Từ chối" }))

    await screen.findByText("Đã từ chối")
    expect(mocks.rejectViewingSchedule).toHaveBeenCalledWith("token", 3)
  })
})
