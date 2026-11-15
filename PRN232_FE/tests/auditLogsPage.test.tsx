import { fireEvent, render, screen, waitFor, within } from "@testing-library/react"
import { MemoryRouter } from "react-router"
import { beforeEach, describe, expect, it, vi } from "vitest"
import ManagementAuditLogsPage from "@/pages/ManagementAuditLogsPage/ManagementAuditLogsPage"
import ManagementSidebar from "@/components/management/ManagementSidebar"

const mocks = vi.hoisted(() => ({
  getAuditLogs: vi.fn(),
}))

vi.mock("@/auth/AuthContext", () => ({
  useAuth: () => ({
    accessToken: "admin-token",
    user: { id: "1", role: "ADMIN" },
  }),
}))

vi.mock("@/components/management/ManagementPage", () => ({
  default: ({
    title,
    children,
  }: {
    title: string
    children: React.ReactNode
  }) => (
    <main>
      <h1>{title}</h1>
      {children}
    </main>
  ),
}))

vi.mock("@/services/auditLogService", () => ({
  getAuditLogs: mocks.getAuditLogs,
}))

beforeEach(() => {
  vi.clearAllMocks()
  mocks.getAuditLogs.mockResolvedValue({
    items: [
      {
        auditLogId: 1201,
        createdAtUtc: "2026-11-15T03:12:44Z",
        userId: 10,
        userName: "Nguyễn Văn A",
        role: "Customer",
        action: "Bookings.Cancel",
        httpMethod: "POST",
        path: "/api/bookings/12/cancel",
        entityType: "Bookings",
        entityId: "12",
        statusCode: 200,
        succeeded: true,
        ipAddress: "203.0.113.5",
        correlationId: "0HN8823",
        oldValues: '{"status":"Deposited","depositAmount":3000000}',
        newValues: '{"status":"Cancelled","depositAmount":3000000,"cancellationReason":"Khách đổi kế hoạch"}',
      },
      {
        auditLogId: 1202,
        createdAtUtc: "2026-11-15T04:00:00Z",
        userId: 7,
        userName: "Manager A",
        role: "Manager",
        action: "Contracts.Terminate",
        httpMethod: "POST",
        path: "/api/contracts/15/terminate",
        entityType: "Contracts",
        entityId: "15",
        statusCode: 409,
        succeeded: false,
        ipAddress: "203.0.113.9",
        correlationId: "0HN8824",
        oldValues: null,
        newValues: null,
      },
    ],
    totalCount: 2,
    page: 1,
    pageSize: 50,
  })
})

describe("ManagementAuditLogsPage (C19)", () => {
  it("shows Nhật ký thao tác link in Admin sidebar and hides it from Manager", () => {
    const { rerender } = render(
      <MemoryRouter>
        <ManagementSidebar
          user={{ id: "1", name: "Admin", email: "a@b.c", phone: "0900000001", role: "ADMIN" }}
          pathname="/management/audit-logs"
          open={false}
          onClose={vi.fn()}
        />
      </MemoryRouter>,
    )
    expect(screen.getByRole("link", { name: "Nhật ký thao tác" })).toBeTruthy()

    rerender(
      <MemoryRouter>
        <ManagementSidebar
          user={{ id: "7", name: "Manager", email: "m@b.c", phone: "0900000007", role: "MANAGER" }}
          pathname="/management/dashboard"
          open={false}
          onClose={vi.fn()}
        />
      </MemoryRouter>,
    )
    expect(screen.queryByRole("link", { name: "Nhật ký thao tác" })).toBeNull()
  })

  it("loads audit logs, filters by entity/result/date, and opens row detail drawer with JSON values", async () => {
    render(
      <MemoryRouter initialEntries={["/management/audit-logs"]}>
        <ManagementAuditLogsPage />
      </MemoryRouter>,
    )

    expect(await screen.findByText("Hủy đặt cọc")).toBeTruthy()
    expect(screen.getAllByText("Thành công").length).toBeGreaterThanOrEqual(2)
    expect(screen.getByText("Thất bại 409")).toBeTruthy()
    expect(screen.getByText("Bookings #12")).toBeTruthy()

    // Bấm vào dòng đầu tiên -> mở chi tiết có đường dẫn, correlationId, bảng Trường / Trước / Sau (C24)
    fireEvent.click(screen.getByRole("button", { name: /Hủy đặt cọc/i }))
    expect(await screen.findByText("POST /api/bookings/12/cancel")).toBeTruthy()
    expect(screen.getByText("0HN8823")).toBeTruthy()

    const diffTable = screen.getByRole("table")
    expect(within(diffTable).getByRole("columnheader", { name: "Trường" })).toBeTruthy()
    expect(within(diffTable).getByRole("columnheader", { name: "Trước" })).toBeTruthy()
    expect(within(diffTable).getByRole("columnheader", { name: "Sau" })).toBeTruthy()

    const statusRow = within(diffTable).getByText("Trạng thái").closest("tr")
    expect(statusRow?.className).toContain("bg-amber-50")
    expect(within(statusRow!).getByText("Deposited")).toBeTruthy()
    expect(within(statusRow!).getByText("Cancelled")).toBeTruthy()

    const depositRow = within(diffTable).getByText("Tiền cọc").closest("tr")
    expect(depositRow?.className).toContain("bg-white")
    expect(within(depositRow!).getAllByText(/3\.000\.000/).length).toBe(2)

    const reasonRow = within(diffTable).getByText("Lý do hủy").closest("tr")
    expect(reasonRow?.className).toContain("bg-amber-50")
    expect(within(reasonRow!).getByText("Khách đổi kế hoạch")).toBeTruthy()
    expect(screen.queryByText(/"status": "Cancelled"/)).toBeNull()

    // Kiểm tra lọc sai ngày -> báo lỗi và không gọi API
    const filterSection = screen.getByRole("region", {
      name: "Bộ lọc nhật ký thao tác",
    })
    fireEvent.change(within(filterSection).getByLabelText("Từ ngày (giờ VN)"), {
      target: { value: "2026-11-20" },
    })
    fireEvent.change(
      within(filterSection).getByLabelText("Đến ngày (giờ VN)"),
      { target: { value: "2026-11-10" } },
    )
    fireEvent.click(
      within(filterSection).getByRole("button", { name: "Lọc nhật ký" }),
    )
    expect(
      await within(filterSection).findByText(
        "Ngày kết thúc không được nhỏ hơn ngày bắt đầu.",
      ),
    ).toBeTruthy()
    expect(mocks.getAuditLogs).toHaveBeenCalledTimes(1)

    // Sửa ngày hợp lệ, chọn đối tượng Bookings -> gọi getAuditLogs kèm bộ lọc
    fireEvent.change(
      within(filterSection).getByLabelText("Đến ngày (giờ VN)"),
      { target: { value: "2026-11-25" } },
    )
    fireEvent.change(within(filterSection).getByLabelText("Loại đối tượng"), {
      target: { value: "Bookings" },
    })
    fireEvent.change(within(filterSection).getByLabelText("Kết quả"), {
      target: { value: "true" },
    })
    fireEvent.click(
      within(filterSection).getByRole("button", { name: "Lọc nhật ký" }),
    )

    await waitFor(() => expect(mocks.getAuditLogs).toHaveBeenCalledTimes(2))
    expect(mocks.getAuditLogs).toHaveBeenLastCalledWith(
      "admin-token",
      {
        userId: undefined,
        entityType: "Bookings",
        entityId: undefined,
        succeeded: "true",
        from: "2026-11-20",
        to: "2026-11-25",
        page: 1,
        pageSize: 50,
      },
      expect.any(AbortSignal),
    )
  })
})
