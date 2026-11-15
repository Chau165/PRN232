import { fireEvent, render, screen, within } from "@testing-library/react"
import { MemoryRouter } from "react-router"
import { beforeEach, describe, expect, it, vi } from "vitest"
import ManagementPage from "@/components/management/ManagementPage"
import {
  ManagementEmptyState,
  ManagementErrorState,
  ManagementLoadingState,
  ManagementSuccessBanner,
} from "@/components/management/ManagementState"
import type { CurrentUser } from "@/types/user"

const authState = vi.hoisted(() => ({
  user: {
    id: "1",
    name: "Quản Trị",
    email: "admin@example.test",
    phone: "0900000001",
    role: "ADMIN",
  } as CurrentUser,
}))

vi.mock("@/utils/managementAuth", async () => {
  const actual = await vi.importActual<typeof import("@/utils/managementAuth")>(
    "@/utils/managementAuth",
  )
  return {
    ...actual,
    getCurrentUser: () => authState.user,
  }
})

vi.mock("@/auth/AuthContext", () => ({
  useOptionalAuth: () => ({
    status: "authenticated",
    user: authState.user,
    logout: vi.fn(),
  }),
}))

vi.mock("@/components/common/NotificationDropdown", () => ({
  default: () => <div data-testid="notifications-dropdown" />,
}))

const adminUser: CurrentUser = {
  id: "1",
  name: "Quản Trị",
  email: "admin@example.test",
  phone: "0900000001",
  role: "ADMIN",
}

const managerUser: CurrentUser = {
  id: "2",
  name: "Quản Lý Khu A",
  email: "manager@example.test",
  phone: "0900000002",
  role: "MANAGER",
}

describe("management unified shell and states", () => {
  beforeEach(() => {
    authState.user = adminUser
  })

  it("renders a single h1, header scope badge, breadcrumbs, actions, and omits the dead search box", () => {
    const onAction = vi.fn()
    render(
      <MemoryRouter initialEntries={["/management/properties/1/rooms"]}>
        <ManagementPage
          title="Phòng"
          description="Danh sách phòng thuộc khu trọ."
          breadcrumbs={[
            { label: "Khu trọ", to: "/management/properties" },
            { label: "Nhà trọ Bình An", to: "/management/properties/1/rooms" },
            { label: "Phòng" },
          ]}
          actions={
            <button type="button" onClick={onAction}>
              + Thêm phòng
            </button>
          }
        >
          <p>Nội dung trang</p>
        </ManagementPage>
      </MemoryRouter>,
    )

    const h1Headings = screen.getAllByRole("heading", { level: 1 })
    expect(h1Headings).toHaveLength(1)
    expect(h1Headings[0].textContent).toBe("Phòng")

    expect(screen.queryByPlaceholderText("Tìm kiếm...")).toBeNull()
    expect(screen.queryByRole("searchbox")).toBeNull()

    const banner = screen.getByRole("banner")
    expect(within(banner).getByRole("link", { name: "Cổng quản lý" }).getAttribute("href")).toBe(
      "/management/dashboard",
    )
    expect(within(banner).getByText("Phòng")).toBeTruthy()
    expect(within(banner).getByText("Toàn hệ thống")).toBeTruthy()

    const breadcrumbNav = screen.getByRole("navigation", { name: "Đường dẫn" })
    expect(
      within(breadcrumbNav).getByRole("link", { name: "Khu trọ" }).getAttribute("href"),
    ).toBe("/management/properties")
    expect(
      within(breadcrumbNav).getByText("Phòng").getAttribute("aria-current"),
    ).toBe("page")

    expect(screen.getByText("Danh sách phòng thuộc khu trọ.")).toBeTruthy()
    fireEvent.click(screen.getByRole("button", { name: "+ Thêm phòng" }))
    expect(onAction).toHaveBeenCalledTimes(1)
  })

  it("displays assigned-scope labeling for managers in both header and page header", () => {
    authState.user = managerUser
    render(
      <MemoryRouter initialEntries={["/management/rooms"]}>
        <ManagementPage title="Phòng">
          <p>Dữ liệu</p>
        </ManagementPage>
      </MemoryRouter>,
    )

    const banner = screen.getByRole("banner")
    expect(within(banner).getByText("Phạm vi được phân công")).toBeTruthy()
    expect(screen.getByText("Dữ liệu được phân công")).toBeTruthy()
  })

  it("renders unified loading, empty, error, and success state components", () => {
    const onRetry = vi.fn()
    render(
      <div>
        <ManagementLoadingState
          variant="cards"
          rows={3}
          message="Đang tải tổng quan..."
        />
        <ManagementEmptyState
          icon="building"
          title="Chưa có khu trọ"
          message="Hãy tạo khu trọ đầu tiên để bắt đầu."
          action={<button type="button">Tạo ngay</button>}
        />
        <ManagementErrorState
          variant="banner"
          message="Không thể tải danh sách."
          retryLabel="Thử lại"
          onRetry={onRetry}
        />
        <ManagementSuccessBanner message="Đã lưu cấu hình thành công." />
      </div>,
    )

    const statuses = screen.getAllByRole("status")
    expect(statuses[0].getAttribute("aria-busy")).toBe("true")
    expect(screen.getByText("Đang tải tổng quan...")).toBeTruthy()
    expect(screen.getByText("Chưa có khu trọ")).toBeTruthy()
    expect(screen.getByText("Hãy tạo khu trọ đầu tiên để bắt đầu.")).toBeTruthy()
    expect(screen.getByRole("button", { name: "Tạo ngay" })).toBeTruthy()

    const alert = screen.getByRole("alert")
    expect(alert.textContent).toBe("Không thể tải danh sách.")
    fireEvent.click(screen.getByRole("button", { name: "Thử lại" }))
    expect(onRetry).toHaveBeenCalledTimes(1)

    expect(screen.getByText("Đã lưu cấu hình thành công.")).toBeTruthy()
  })
})
