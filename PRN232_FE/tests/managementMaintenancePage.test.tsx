import { fireEvent, render, screen, waitFor } from "@testing-library/react"
import { MemoryRouter } from "react-router"
import { beforeEach, describe, expect, it, vi } from "vitest"
import ManagementMaintenancePage from "@/pages/ManagementMaintenancePage/ManagementMaintenancePage"
import type { MaintenanceRequestDto } from "@/api/maintenanceApi"

const mocks = vi.hoisted(() => ({
  getMaintenanceRequests: vi.fn(),
  getMaintenanceRequest: vi.fn(),
  getMaintenanceLogs: vi.fn(),
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
vi.mock("@/api/maintenanceApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/api/maintenanceApi")>()),
  getMaintenanceRequests: mocks.getMaintenanceRequests,
  getMaintenanceRequest: mocks.getMaintenanceRequest,
  getMaintenanceLogs: mocks.getMaintenanceLogs,
  assignMaintenanceRequest: mocks.assignMaintenanceRequest,
  submitMaintenanceReport: mocks.submitMaintenanceReport,
}))
vi.mock("@/api/usersApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/api/usersApi")>()),
  getRoles: mocks.getRoles,
  getUsers: mocks.getUsers,
  getMaintenanceRooms: mocks.getMaintenanceRooms,
}))
vi.mock("@/api/apiClient", () => ({ ApiError: class ApiError extends Error { status?: number } }))

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
    fireEvent.change(screen.getByLabelText("Mô tả công việc"), { target: { value: "Đã thay tụ điện" } })
    fireEvent.change(screen.getByLabelText("Ảnh hoàn thành"), { target: { files: [image] } })
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
})
