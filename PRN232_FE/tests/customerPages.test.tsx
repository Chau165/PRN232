import { act, fireEvent, render, renderHook, screen, waitFor, within } from "@testing-library/react"
import { MemoryRouter, useLocation } from "react-router"
import { beforeEach, describe, expect, it, vi } from "vitest"
import MyRoomPage from "@/pages/MyRoomPage/MyRoomPage"
import MyContractsPage from "@/pages/MyContractsPage/MyContractsPage"
import NotificationsPage from "@/pages/NotificationsPage/NotificationsPage"
import Navbar from "@/components/common/Navbar"
import type { Rental } from "@/types/rental"
import type { MaintenanceRequestDto } from "@/types/maintenance"
import type { NotificationDto } from "@/types/notification"
import type { ContractBillingTimelineDto, ContractDto } from "@/types/contract"
import { useCustomerNotifications } from "@/hooks/useCustomerNotifications"
import { localToday } from "@/utils/managementContracts"

const mocks = vi.hoisted(() => ({
  getCurrentRentals: vi.fn(),
  getRentalHistory: vi.fn(),
  getContracts: vi.fn(),
  getContractBillingTimeline: vi.fn(),
  getMaintenanceRequests: vi.fn(),
  getMaintenanceRequest: vi.fn(),
  getMaintenanceLogs: vi.fn(),
  createMaintenanceRequest: vi.fn(),
  getNotifications: vi.fn(),
  markNotificationRead: vi.fn(),
  notificationHubUrl: vi.fn(() => "/hubs/notifications"),
  refreshNotifications: vi.fn(),
  markRead: vi.fn(),
  notifications: [] as NotificationDto[],
  lastMarkRead: true,
  navAuth: null as null | { status: "authenticated"; user: { id: string; name: string; role: "CUSTOMER" }; logout: () => void },
}))

vi.mock("@/auth/AuthContext", () => ({
  useAuth: () => ({ accessToken: "customer-token" }),
  useOptionalAuth: () => mocks.navAuth,
}))
vi.mock("@/auth/CustomerNotificationsContext", () => ({
  useCustomerNotificationsState: () => ({
    notifications: mocks.notifications, unreadCount: mocks.notifications.filter((item) => !item.isRead).length, loading: false, error: null, connected: true,
    refresh: mocks.refreshNotifications, markRead: (...args: unknown[]) => { mocks.lastMarkRead = true; return mocks.markRead(...args) },
  }),
}))
vi.mock("@/services/rentalService", () => ({
  getCurrentRentals: mocks.getCurrentRentals,
  getRentalHistory: mocks.getRentalHistory,
}))
vi.mock("@/services/contractService", () => ({
  getContracts: mocks.getContracts,
  getContractBillingTimeline: mocks.getContractBillingTimeline,
}))
vi.mock("@/services/maintenanceService", () => ({
  getMaintenanceRequests: mocks.getMaintenanceRequests,
  getMaintenanceRequest: mocks.getMaintenanceRequest,
  getMaintenanceLogs: mocks.getMaintenanceLogs,
  createMaintenanceRequest: mocks.createMaintenanceRequest,
}))
vi.mock("@/services/notificationService", () => ({
  getNotifications: mocks.getNotifications,
  markNotificationRead: mocks.markNotificationRead,
  notificationHubUrl: mocks.notificationHubUrl,
}))
vi.mock("@microsoft/signalr", () => ({
  HubConnectionBuilder: class {
    withUrl() { return this }
    withAutomaticReconnect() { return this }
    build() {
      return {
        state: "Connected",
        on() {}, off() {}, onreconnecting() {}, onreconnected() {}, onclose() {},
        async start() {}, async stop() {},
      }
    }
  },
  HubConnectionState: { Connected: "Connected", Disconnected: "Disconnected" },
}))
vi.mock("@/services/notificationNavigation", () => ({ notificationPath: () => "/notifications" }))

const rental = (overrides: Partial<Rental> = {}): Rental => ({
  contractId: 123, contractStatus: "Active", startDate: "2026-01-01", endDate: "2027-01-01",
  monthlyRent: 3_800_000, depositAmount: 7_600_000, roomId: 45, roomCode: "A203",
  roomStatus: "Occupied", areaSqm: 24, roomTypeId: 2, roomTypeName: "Studio",
  propertyId: 8, propertyName: "Khu trọ thật", propertyAddress: "Địa chỉ thật",
  primaryImageUrl: null, images: [], equipments: [{ equipmentId: 11, name: "Máy lạnh", status: "Working" }], amenities: [],
  ...overrides,
})

const contract = (overrides: Partial<ContractDto> = {}): ContractDto => ({
  contractId: 123,
  bookingId: 9,
  roomId: 45,
  roomCode: "A203",
  propertyId: 8,
  propertyName: "Khu trọ thật",
  customerId: 10,
  customerName: "Customer",
  customerPhone: "0900000000",
  startDate: "2026-11-01",
  endDate: "2027-10-31",
  monthlyRent: 3_000_000,
  depositAmount: 3_000_000,
  status: "Active",
  processedByStaffName: "Manager A",
  prepaidRentAmount: 3_000_000,
  prepaidRentApplied: 0,
  remainingPrepaidCredit: 3_000_000,
  propertyAddress: "Địa chỉ thật",
  lessorUserId: 7,
  lessorName: "Admin Owner",
  lessorAddress: "12 Example Street",
  displayStatus: "Upcoming",
  ...overrides,
})

const billingTimeline = (contractDto: ContractDto): ContractBillingTimelineDto => ({
  contract: contractDto,
  issuedInvoices: [],
  plannedBillingPeriods: [{
    invoiceMonth: `${localToday().slice(0, 7)}-01`,
    monthlyRent: contractDto.monthlyRent,
    displayStatus: "NotIssued",
    grossTotal: null,
    prepaidRentApplied: null,
    netPayable: null,
    utilitiesStatus: "Chưa có dữ liệu",
    note: null,
  }],
  hasMorePlannedPeriods: false,
})

const request = (overrides: Partial<MaintenanceRequestDto> = {}): MaintenanceRequestDto => ({
  requestId: 42, description: "Máy lạnh bị hỏng", status: "Pending", contractId: 123,
  customerId: 10, customerName: "Customer", roomId: 45, roomCode: "A203", equipmentId: 11,
  equipmentName: "Máy lạnh", equipmentStatus: "NeedsRepair", assignedTo: null, assignedStaffName: null,
  ...overrides,
})

function renderPage(page: React.ReactNode) {
  return render(<MemoryRouter initialEntries={["/my-room"]}>{page}<CurrentPath /></MemoryRouter>)
}

function CurrentPath() {
  return <output data-testid="current-path">{useLocation().pathname}</output>
}

beforeEach(() => {
  vi.clearAllMocks()
  mocks.getCurrentRentals.mockResolvedValue([rental()])
  mocks.getRentalHistory.mockResolvedValue([])
  mocks.getContracts.mockResolvedValue([contract()])
  mocks.getContractBillingTimeline.mockImplementation(async (_token, contractId) =>
    billingTimeline(contract({ contractId })),
  )
  mocks.getMaintenanceRequests.mockResolvedValue([])
  mocks.getMaintenanceRequest.mockImplementation(async (_token, id) => request({ requestId: id, status: "Approved" }))
  mocks.getMaintenanceLogs.mockResolvedValue([{ logId: 1, requestId: 42, oldStatus: null, newStatus: "Pending", changedBy: 10, changedByName: "Customer", changedAt: "2026-02-01T10:00:00Z" }])
  mocks.createMaintenanceRequest.mockResolvedValue(request({ requestId: 43 }))
  mocks.getNotifications.mockResolvedValue([])
  mocks.markNotificationRead.mockResolvedValue(undefined)
  mocks.refreshNotifications.mockResolvedValue(undefined)
  mocks.markRead.mockResolvedValue(undefined)
  mocks.notifications = []
  mocks.lastMarkRead = true
  mocks.navAuth = null
})

describe("Customer room and notification pages", () => {
  it("renders multiple live rentals and reports against the selected contract and equipment", async () => {
    mocks.getCurrentRentals.mockResolvedValue([
      rental(),
      rental({ contractId: 456, roomId: 78, roomCode: "B204", propertyName: "Khu khác", equipments: [{ equipmentId: 22, name: "Bình nóng lạnh", status: "Working" }] }),
    ])
    renderPage(<MyRoomPage />)

    expect(await screen.findByText("Khu trọ thật")).toBeTruthy()
    expect(screen.getByText("Khu khác")).toBeTruthy()
    fireEvent.click(screen.getAllByRole("button", { name: "Báo hư hỏng" })[1]!)
    fireEvent.change(screen.getByLabelText(/Thiết bị liên quan/), { target: { value: "22" } })
    fireEvent.change(screen.getByLabelText("Mô tả chi tiết"), { target: { value: "Bình nước không nóng" } })
    fireEvent.click(screen.getByRole("button", { name: "Gửi báo cáo" }))

    await waitFor(() => expect(mocks.createMaintenanceRequest).toHaveBeenCalledWith("customer-token", {
      contractId: 456, equipmentId: 22, description: "Bình nước không nóng",
    }))
    expect(await screen.findByText("Đã gửi yêu cầu sửa chữa thành công")).toBeTruthy()
    expect(mocks.refreshNotifications).toHaveBeenCalled()
  })

  it("loads history and request detail/logs from APIs without fabricated timeline data", async () => {
    mocks.getRentalHistory.mockResolvedValue([rental({ contractId: 33, roomCode: "C305", endDate: "2025-12-31" })])
    mocks.getMaintenanceRequests.mockResolvedValue([request()])
    renderPage(<MyRoomPage />)

    fireEvent.click(screen.getByRole("button", { name: "Lịch sử thuê" }))
    expect(await screen.findByText("Phòng C305")).toBeTruthy()
    fireEvent.click(screen.getByRole("button", { name: "Yêu cầu sửa chữa" }))
    const requestButton = await screen.findByRole("button", { name: /#42 · Phòng A203/ })
    fireEvent.click(requestButton)
    expect(await screen.findByText("Đã được duyệt · Đã phân công")).toBeTruthy()
    expect(await screen.findByText(/17:00:00 1\/2\/2026/)).toBeTruthy()
    expect(mocks.getMaintenanceRequest).toHaveBeenCalledWith("customer-token", 42, expect.any(AbortSignal))
    expect(mocks.getMaintenanceLogs).toHaveBeenCalledWith("customer-token", 42, expect.any(AbortSignal))
  })

  it("shows real empty/error states and does not create optimistic requests on failure", async () => {
    mocks.getCurrentRentals.mockResolvedValue([])
    mocks.getMaintenanceRequests.mockRejectedValue(new Error("API unavailable"))
    renderPage(<MyRoomPage />)
    expect(await screen.findByText("Bạn chưa có phòng đang thuê.")).toBeTruthy()
    fireEvent.click(screen.getByRole("button", { name: "Yêu cầu sửa chữa" }))
    expect(await screen.findByText("API unavailable")).toBeTruthy()
  })

  it("shows every contract status and only labels planned periods as not issued", async () => {
    mocks.getContracts.mockResolvedValue([
      contract({ contractId: 123, displayStatus: "Upcoming" }),
      contract({ contractId: 88, roomCode: "D406", displayStatus: "Active" }),
      contract({ contractId: 99, roomCode: "E407", displayStatus: "Ended" }),
      contract({ contractId: 77, roomCode: "F408", displayStatus: "ExpiredPendingCheckout" }),
      contract({ contractId: 66, roomCode: "G409", status: "Terminated", displayStatus: "Terminated" }),
    ])
    renderPage(<MyContractsPage />)
    expect(await screen.findByText("Hợp đồng #123")).toBeTruthy()
    expect(screen.getByText("Hợp đồng #88")).toBeTruthy()
    expect(screen.getByText("Hợp đồng #99")).toBeTruthy()
    expect(screen.getByText("Sắp có hiệu lực")).toBeTruthy()
    expect(screen.getByText("Đang thuê")).toBeTruthy()
    expect(screen.getByText("Đã kết thúc")).toBeTruthy()
    expect(screen.getByText("Hết hạn — Chờ trả phòng")).toBeTruthy()
    expect(screen.getByText("Đã kết thúc / Đã trả phòng")).toBeTruthy()
    expect(screen.queryByText("Ngọc Anh")).toBeNull()

    fireEvent.click(screen.getAllByRole("button", { name: "Xem chi tiết và lịch thanh toán" })[0]!)
    expect(await screen.findByText(/Kỳ dự kiến tháng/)).toBeTruthy()
    expect(screen.getByText("Chưa phát hành")).toBeTruthy()
    expect(screen.queryByRole("link", { name: "Thanh toán" })).toBeNull()
    expect(mocks.getContractBillingTimeline).toHaveBeenCalledWith("customer-token", 123)
  })

  it("marks a real notification read through the shared API state", async () => {
    mocks.notifications = [{ notificationId: 9, title: "Yêu cầu đã được duyệt", type: "Maintenance", isRead: false }]
    mocks.markRead.mockImplementation(async () => {})
    renderPage(<NotificationsPage />)
    expect(await screen.findByText("Yêu cầu đã được duyệt")).toBeTruthy()
    const card = screen.getByRole("button", { name: /Yêu cầu đã được duyệt/ })
    expect(within(card).getByText("Chưa đọc")).toBeTruthy()
    fireEvent.click(card)
    await waitFor(() => expect(mocks.markRead).toHaveBeenCalledWith(9))
  })

  it("renders Pending, Approved, and Completed from the API without fabricated in-progress state", async () => {
    mocks.getMaintenanceRequests.mockResolvedValue([
      request({ requestId: 45, status: "Pending" }),
      request({ requestId: 46, status: "Approved" }),
      request({ requestId: 47, status: "Completed" }),
    ])
    renderPage(<MyRoomPage />)
    fireEvent.click(screen.getByRole("button", { name: "Yêu cầu sửa chữa" }))
    expect(await screen.findByText("HOÀN THÀNH")).toBeTruthy()
    expect(screen.getByText("ĐÃ DUYỆT · ĐÃ PHÂN CÔNG")).toBeTruthy()
    expect(screen.getAllByText("CHỜ DUYỆT")).toHaveLength(1)
    expect(screen.queryByText("ĐANG XỬ LÝ")).toBeNull()
  })

  it("keeps API error visible instead of showing a success toast when submission fails", async () => {
    mocks.createMaintenanceRequest.mockRejectedValue(new Error("Không được phép tạo yêu cầu"))
    renderPage(<MyRoomPage />)
    fireEvent.click(await screen.findByRole("button", { name: "Báo hư hỏng" }))
    fireEvent.change(screen.getByLabelText("Mô tả chi tiết"), { target: { value: "Vòi nước rò" } })
    fireEvent.click(screen.getByRole("button", { name: "Gửi báo cáo" }))
    expect(await screen.findByText("Không được phép tạo yêu cầu")).toBeTruthy()
    expect(screen.queryByText("Đã gửi yêu cầu sửa chữa thành công")).toBeNull()
    expect(mocks.createMaintenanceRequest).toHaveBeenCalledTimes(1)
  })

  it("counts unread API notifications and updates the shared count after a confirmed read", async () => {
    mocks.getNotifications.mockResolvedValue([
      { notificationId: 1, title: "one", type: "Maintenance", isRead: false },
      { notificationId: 2, title: "two", type: "Maintenance", isRead: true },
      { notificationId: 3, title: "three", type: "Maintenance", isRead: false },
    ])
    const { result } = renderHook(() => useCustomerNotifications("customer-token", true))
    await waitFor(() => expect(result.current.unreadCount).toBe(2))
    await act(async () => { await result.current.markRead(1) })
    expect(result.current.unreadCount).toBe(1)
    expect(mocks.markNotificationRead).toHaveBeenCalledWith("customer-token", 1)
  })

  it("does not expose cached notifications while switching Customer accounts", async () => {
    mocks.getNotifications.mockResolvedValue([{ notificationId: 1, title: "private", type: "Maintenance", isRead: false }])
    const { result, rerender } = renderHook(({ token }) => useCustomerNotifications(token, Boolean(token)), {
      initialProps: { token: "customer-one-token" as string | null },
    })
    await waitFor(() => expect(result.current.notifications).toHaveLength(1))
    rerender({ token: "customer-two-token" })
    expect(result.current.notifications).toEqual([])
    expect(result.current.unreadCount).toBe(0)
  })

  it("keeps the current page when marking a notification read fails", async () => {
    mocks.notifications = [{ notificationId: 9, title: "Viewing approved", type: "Viewing", isRead: false }]
    mocks.markRead.mockRejectedValue(new Error("read update failed"))
    renderPage(<NotificationsPage />)

    fireEvent.click(screen.getByText("Viewing approved"))

    expect(await screen.findByText("read update failed")).toBeTruthy()
    expect(screen.getByTestId("current-path").textContent).toBe("/my-room")
  })

  it("shows the API unread count in the Navbar dropdown", async () => {
    mocks.navAuth = { status: "authenticated", user: { id: "10", name: "Customer", role: "CUSTOMER" }, logout: vi.fn() }
    mocks.notifications = [
      { notificationId: 1, title: "Unread one", type: "Maintenance", isRead: false },
      { notificationId: 2, title: "Read", type: "Maintenance", isRead: true },
      { notificationId: 3, title: "Unread two", type: "Maintenance", isRead: false },
    ]
    renderPage(<Navbar />)
    expect(screen.getByText("2")).toBeTruthy()
    fireEvent.click(screen.getByRole("button", { name: "Thông báo" }))
    expect(screen.getByText("Unread one")).toBeTruthy()
    expect(screen.getByText("Unread two")).toBeTruthy()
  })
})
