import { fireEvent, render, screen, waitFor } from "@testing-library/react"
import { MemoryRouter } from "react-router"
import { beforeEach, describe, expect, it, vi } from "vitest"
import ManagementMaintenancePage from "@/pages/ManagementMaintenancePage/ManagementMaintenancePage"
import type { MaintenanceRequestDto } from "@/types/maintenance"
import { ApiError } from "@/api/apiClient"

const mocks = vi.hoisted(() => ({
  getMaintenanceRequests: vi.fn(),
  getMaintenanceRequest: vi.fn(),
  getMaintenanceLogs: vi.fn(),
  getMaintenanceReport: vi.fn(),
  getMaintenanceRooms: vi.fn(),
  assignMaintenanceRequest: vi.fn(),
  submitMaintenanceReport: vi.fn(),
  getRoles: vi.fn(),
  getUsers: vi.fn(),
  role: "ADMIN" as "ADMIN" | "MANAGER",
}))

vi.mock("@/auth/AuthContext", () => ({
  useAuth: () => ({ accessToken: "test-token", user: { id: "9", role: mocks.role } }),
}))
vi.mock("@/utils/managementAuth", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/utils/managementAuth")>()),
  getCurrentUser: () => ({ id: "9", name: "Test User", role: mocks.role }),
}))
vi.mock("@/utils/managementScope", () => ({
  getManagementScope: () => ({ maintenance: [], properties: [] }),
}))
vi.mock("@/components/management/ManagementPage", () => ({
  default: ({ children }: { children: React.ReactNode }) => <main>{children}</main>,
}))
vi.mock("@/services/maintenanceService", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/services/maintenanceService")>()),
  getMaintenanceRequests: mocks.getMaintenanceRequests,
  getMaintenanceRequest: mocks.getMaintenanceRequest,
  getMaintenanceLogs: mocks.getMaintenanceLogs,
  getMaintenanceReport: mocks.getMaintenanceReport,
  assignMaintenanceRequest: mocks.assignMaintenanceRequest,
  submitMaintenanceReport: mocks.submitMaintenanceReport,
}))
vi.mock("@/services/userService", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/services/userService")>()),
  getRoles: mocks.getRoles,
  getUsers: mocks.getUsers,
  getMaintenanceRooms: mocks.getMaintenanceRooms,
}))
vi.mock("@/api/apiClient", () => ({ ApiError: class ApiError extends Error { status?: number; constructor(message: string, options: { status?: number } = {}) { super(message); this.status = options.status } } }))

const request = (overrides: Partial<MaintenanceRequestDto> = {}): MaintenanceRequestDto => ({
  requestId: 41,
  description: "Máy lạnh không hoạt động",
  status: "Pending",
  contractId: 100,
  customerId: 22,
  customerName: "Nguyễn An",
  roomId: 3,
  roomCode: "A103",
  equipmentId: 7,
  equipmentName: "Máy lạnh",
  equipmentStatus: "NeedsRepair",
  assignedTo: null,
  assignedStaffName: null,
  assignmentNote: null,
  ...overrides,
})

function renderPage() {
  return render(<MemoryRouter><ManagementMaintenancePage /></MemoryRouter>)
}

beforeEach(() => {
  vi.clearAllMocks()
  mocks.role = "ADMIN"
  mocks.getMaintenanceRequests.mockResolvedValue([request()])
  mocks.getMaintenanceRequest.mockImplementation(async (_token, id) => request({ requestId: id }))
  mocks.getMaintenanceLogs.mockResolvedValue([])
  mocks.getMaintenanceReport.mockReset().mockResolvedValue({ reportId: 7, requestId: 41, description: "Đã sửa máy lạnh", reportedBy: 9, reportedByName: "Manager thật", createdAt: "2026-10-17T08:02:11", images: [{ imageId: 1, imageUrl: "https://storage.test/evidence.jpg" }, { imageId: 2, imageUrl: "https://storage.test/evidence-2.jpg" }] })
  mocks.assignMaintenanceRequest.mockResolvedValue(request({ status: "Approved", assignedTo: 5, assignedStaffName: "Manager thật" }))
  mocks.submitMaintenanceReport.mockResolvedValue(undefined)
  mocks.getRoles.mockResolvedValue([{ roleId: 2, roleName: "Manager" }])
  mocks.getUsers.mockResolvedValue([{ userId: 5, fullName: "Manager thật", roleId: 2, roleName: "Manager", email: "m@example.test", phoneNumber: "", isActive: true }])
  mocks.getMaintenanceRooms.mockResolvedValue([])
})

describe("Management maintenance API flow", () => {
  it("loads real requests and assigns only after the API succeeds, including the internal note", async () => {
    renderPage()

    fireEvent.click(await screen.findByRole("button", { name: /#41/ }))
    expect(await screen.findByText("Nguyễn An")).toBeTruthy()
    fireEvent.change(screen.getByLabelText("Manager phụ trách"), { target: { value: "5" } })
    fireEvent.change(screen.getByLabelText("Ghi chú phân công"), { target: { value: "Mang linh kiện thay thế" } })
    fireEvent.click(screen.getByRole("button", { name: "Phân công Manager" }))

    await waitFor(() => expect(mocks.assignMaintenanceRequest).toHaveBeenCalledWith("test-token", 41, {
      assignedTo: 5,
      assignmentNote: "Mang linh kiện thay thế",
    }))
    await waitFor(() => expect(mocks.getMaintenanceRequests).toHaveBeenCalledTimes(2))
    expect(await screen.findByText("Đã phân công Manager thành công.")).toBeTruthy()
  })

  it("submits report description and actual image files for an assigned Manager", async () => {
    mocks.role = "MANAGER"
    mocks.getMaintenanceRequests.mockResolvedValue([request({ status: "Approved", assignedTo: 9, assignedStaffName: "Test User", assignmentNote: "Mang linh kiện" })])
    mocks.getMaintenanceRequest.mockResolvedValue(request({ status: "Approved", assignedTo: 9, assignedStaffName: "Test User", assignmentNote: "Mang linh kiện" }))
    const image = new File([new Uint8Array([0xff, 0xd8, 0xff, 0xd9])], "repair.jpg", { type: "image/jpeg" })
    renderPage()

    fireEvent.click(await screen.findByRole("button", { name: /#41/ }))
    expect(await screen.findByText("Mang linh kiện")).toBeTruthy()
    fireEvent.change(await screen.findByLabelText("Mô tả công việc"), { target: { value: "Đã thay tụ điện" } })
    fireEvent.change(await screen.findByLabelText("Ảnh hoàn thành"), { target: { files: [image] } })
    fireEvent.click(screen.getByRole("button", { name: "Gửi báo cáo hoàn thành" }))

    await waitFor(() => expect(mocks.submitMaintenanceReport).toHaveBeenCalledTimes(1))
    const form = mocks.submitMaintenanceReport.mock.calls[0]?.[2] as FormData
    expect(form.get("Description")).toBe("Đã thay tụ điện")
    expect(form.get("Images")).toBe(image)
    expect(await screen.findByText("Báo cáo hoàn thành đã được gửi.")).toBeTruthy()
  })

  it("does not show assignment or completion actions to a Manager who is not assigned", async () => {
    mocks.role = "MANAGER"
    mocks.getMaintenanceRequests.mockResolvedValue([request({ status: "Approved", assignedTo: 4 })])
    mocks.getMaintenanceRequest.mockResolvedValue(request({ status: "Approved", assignedTo: 4 }))
    renderPage()
    fireEvent.click(await screen.findByRole("button", { name: /#41/ }))
    await screen.findByText("Nguyễn An")
    expect(screen.queryByRole("button", { name: "Gửi báo cáo hoàn thành" })).toBeNull()
    expect(screen.queryByRole("button", { name: "Phân công Manager" })).toBeNull()
  })

  it("loads and displays the saved report and image gallery only for a completed request", async () => {
    mocks.getMaintenanceRequests.mockResolvedValue([request({ status: "Completed" })])
    mocks.getMaintenanceRequest.mockImplementation(async (_token, id) => request({ requestId: id, status: "Completed" }))
    renderPage()

    fireEvent.click(await screen.findByRole("button", { name: /#41/ }))

    expect(await screen.findByText("BÁO CÁO SỬA CHỮA & HÌNH ẢNH HOÀN THÀNH")).toBeTruthy()
    expect(screen.getByText("Đã sửa máy lạnh")).toBeTruthy()
    expect(screen.getByText("Manager thật")).toBeTruthy()
    expect(screen.getByText("15:02 17 thg 10, 2026")).toBeTruthy()
    expect(screen.getAllByRole("img", { name: /Ảnh hoàn thành sửa chữa/ })).toHaveLength(2)
    expect((screen.getAllByRole("img", { name: /Ảnh hoàn thành sửa chữa/ })[0] as HTMLImageElement).src).toBe("https://storage.test/evidence.jpg")
    expect(mocks.getMaintenanceReport).toHaveBeenCalledWith("test-token", 41, expect.any(AbortSignal))
    fireEvent.click(screen.getByRole("button", { name: "Phóng to ảnh 1" }))
    expect(screen.getByRole("dialog", { name: /Ảnh hoàn thành sửa chữa/ })).toBeTruthy()
    fireEvent.click(screen.getByRole("button", { name: "Ảnh tiếp theo" }))
    expect(screen.getAllByRole("img", { name: "Ảnh hoàn thành sửa chữa 2" })).toHaveLength(2)
    fireEvent.click(screen.getByRole("button", { name: "Ảnh trước" }))
    const lightboxDialog = screen.getByRole("dialog", { name: /Ảnh hoàn thành sửa chữa/ })
    fireEvent.keyDown(lightboxDialog, { key: "Escape" })
    expect(screen.queryByRole("dialog", { name: /Ảnh hoàn thành sửa chữa/ })).toBeNull()
    expect(screen.getByRole("button", { name: "Phóng to ảnh 1" }).ownerDocument.activeElement).toBe(screen.getByRole("button", { name: "Phóng to ảnh 1" }))
    fireEvent.click(screen.getByRole("button", { name: "Phóng to ảnh 1" }))
    fireEvent.click(screen.getByRole("button", { name: "Đóng ảnh" }))
    expect(screen.queryByRole("dialog", { name: /Ảnh hoàn thành sửa chữa/ })).toBeNull()
    fireEvent.click(screen.getByRole("button", { name: "Phóng to ảnh 1" }))
    fireEvent.keyDown(screen.getByRole("dialog", { name: /Ảnh hoàn thành sửa chữa/ }), { key: "Tab", shiftKey: true })
    expect(screen.getByRole("button", { name: "Ảnh tiếp theo" }).ownerDocument.activeElement).toBe(screen.getByRole("button", { name: "Ảnh tiếp theo" }))
    fireEvent.keyDown(screen.getByRole("dialog", { name: /Ảnh hoàn thành sửa chữa/ }), { key: "ArrowRight" })
    expect(screen.getAllByRole("img", { name: "Ảnh hoàn thành sửa chữa 2" })).toHaveLength(2)
    fireEvent.click(screen.getByRole("button", { name: "Đóng ảnh" }))
  })

  it("does not load completed reports for Pending or Approved requests", async () => {
    renderPage()
    fireEvent.click(await screen.findByRole("button", { name: /#41/ }))
    await screen.findByText("Nguyễn An")
    expect(mocks.getMaintenanceReport).not.toHaveBeenCalled()
    expect(screen.queryByText("BÁO CÁO SỬA CHỮA & HÌNH ẢNH HOÀN THÀNH")).toBeNull()
  })

  it("reloads the saved report immediately after the Manager completes a request", async () => {
    mocks.role = "MANAGER"
    mocks.getMaintenanceRequests.mockResolvedValue([request({ status: "Approved", assignedTo: 9 })])
    mocks.getMaintenanceRequest
      .mockResolvedValueOnce(request({ status: "Approved", assignedTo: 9 }))
      .mockResolvedValueOnce(request({ status: "Completed", assignedTo: 9 }))
    const image = new File([new Uint8Array([0xff, 0xd8, 0xff, 0xd9])], "repair.jpg", { type: "image/jpeg" })
    renderPage()
    fireEvent.click(await screen.findByRole("button", { name: /#41/ }))
    fireEvent.change(await screen.findByLabelText("Mô tả công việc"), { target: { value: "Đã thay tụ điện" } })
    fireEvent.change(await screen.findByLabelText("Ảnh hoàn thành"), { target: { files: [image] } })
    fireEvent.click(screen.getByRole("button", { name: "Gửi báo cáo hoàn thành" }))

    expect(await screen.findByText("BÁO CÁO SỬA CHỮA & HÌNH ẢNH HOÀN THÀNH")).toBeTruthy()
    expect(mocks.getMaintenanceReport).toHaveBeenCalledWith("test-token", 41, expect.any(AbortSignal))
  })

  it("shows the missing-report state for 404 and a retry action for storage errors", async () => {
    mocks.getMaintenanceRequests.mockResolvedValue([request({ status: "Completed" })])
    mocks.getMaintenanceRequest.mockImplementation(async (_token, id) => request({ requestId: id, status: "Completed" }))
    mocks.getMaintenanceReport.mockRejectedValueOnce(new ApiError("Not found", { status: 404 }))
      .mockRejectedValueOnce(new ApiError("Storage unavailable", { status: 502 }))
      .mockResolvedValueOnce({ reportId: 7, requestId: 41, description: "Retry thành công", reportedBy: 9, reportedByName: "Manager thật", createdAt: "2026-10-17T08:02:11Z", images: [] })
    renderPage()
    fireEvent.click(await screen.findByRole("button", { name: /#41/ }))
    expect(await screen.findByText("Chưa tìm thấy báo cáo hoàn thành.")).toBeTruthy()
    fireEvent.click(screen.getByRole("button", { name: "Đóng" }))
    fireEvent.click(screen.getByRole("button", { name: /#41/ }))
    expect(await screen.findByText(/Không thể tải báo cáo: .*Storage unavailable/)).toBeTruthy()
    fireEvent.click(screen.getByRole("button", { name: "Thử tải lại báo cáo" }))
    expect(await screen.findByText("Retry thành công")).toBeTruthy()
  })

  it("replaces report data when switching between completed requests", async () => {
    const secondRequest = request({ requestId: 42, status: "Completed" })
    mocks.getMaintenanceRequests.mockResolvedValue([request({ status: "Completed" }), secondRequest])
    mocks.getMaintenanceRequest.mockImplementation(async (_token, id) => request({ requestId: id, status: "Completed" }))
    mocks.getMaintenanceReport
      .mockResolvedValueOnce({ reportId: 7, requestId: 41, description: "Báo cáo request 41", reportedBy: 9, reportedByName: "Manager A", createdAt: "2026-10-17T08:02:11Z", images: [] })
      .mockResolvedValueOnce({ reportId: 8, requestId: 42, description: "Báo cáo request 42", reportedBy: 9, reportedByName: "Manager B", createdAt: "2026-10-17T08:02:11Z", images: [] })
    renderPage()
    fireEvent.click(await screen.findByRole("button", { name: /#41/ }))
    expect(await screen.findByText("Báo cáo request 41")).toBeTruthy()
    fireEvent.click(screen.getByRole("button", { name: /#42/ }))
    expect(await screen.findByText("Báo cáo request 42")).toBeTruthy()
    expect(screen.queryByText("Báo cáo request 41")).toBeNull()
    expect(screen.getByText("Báo cáo không có hình ảnh.")).toBeTruthy()
  })

  it("shows forbidden report access without exposing report details", async () => {
    mocks.getMaintenanceRequests.mockResolvedValue([request({ status: "Completed" })])
    mocks.getMaintenanceRequest.mockImplementation(async (_token, id) => request({ requestId: id, status: "Completed" }))
    mocks.getMaintenanceReport.mockRejectedValue(new ApiError("Forbidden", { status: 403 }))
    renderPage()
    fireEvent.click(await screen.findByRole("button", { name: /#41/ }))
    expect(await screen.findByText(/Bạn không có quyền thực hiện thao tác này/)).toBeTruthy()
    expect(screen.queryByText("Đã sửa máy lạnh")).toBeNull()
  })

  it("replaces a failed image with an accessible error state", async () => {
    mocks.getMaintenanceRequests.mockResolvedValue([request({ status: "Completed" })])
    mocks.getMaintenanceRequest.mockImplementation(async (_token, id) => request({ requestId: id, status: "Completed" }))
    renderPage()
    fireEvent.click(await screen.findByRole("button", { name: /#41/ }))
    const image = await screen.findByRole("img", { name: "Ảnh hoàn thành sửa chữa 1" })
    fireEvent.error(image)
    expect(screen.getByText("Không thể tải ảnh")).toBeTruthy()
  })
})
