import { fireEvent, render, screen, within } from "@testing-library/react"
import type { ReactNode } from "react"
import { MemoryRouter, Route, Routes, useLocation } from "react-router"
import { describe, expect, it, vi } from "vitest"
import AuthShell from "@/components/auth/AuthShell"
import Navbar from "@/components/common/Navbar"
import ManagementHeader from "@/components/management/ManagementHeader"
import ManagementSidebar from "@/components/management/ManagementSidebar"
import type { CurrentUser } from "@/types/user"

// In-app links must use the router (no full page reload): with a MemoryRouter, a plain
// <a href> click does not change the location, while a <Link> click does.

const auth = vi.hoisted(() => ({ current: null as unknown }))
vi.mock("@/auth/AuthContext", () => ({ useOptionalAuth: () => auth.current }))

const customer: CurrentUser = { id: "7", name: "Lan Anh", email: "lan@example.test", phone: "0900", role: "CUSTOMER" } as CurrentUser
const admin: CurrentUser = { ...customer, id: "1", name: "Quản Trị", role: "ADMIN" } as CurrentUser
const manager: CurrentUser = { ...customer, id: "2", name: "Quản Lý", role: "MANAGER" } as CurrentUser

function Path() {
  return <output data-testid="path">{useLocation().pathname}</output>
}

function renderAt(path: string, ui: ReactNode) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Path />
      <Routes>
        <Route path="*" element={ui} />
      </Routes>
    </MemoryRouter>,
  )
}

const path = () => screen.getByTestId("path").textContent

describe("client-side navigation links", () => {
  it("moves between management pages from the sidebar without reloading", () => {
    const onClose = vi.fn()
    renderAt("/management/dashboard", <ManagementSidebar user={admin} pathname="/management/dashboard" open onClose={onClose} />)

    fireEvent.click(screen.getByRole("link", { name: "Hợp đồng" }))
    expect(path()).toBe("/management/contracts")
    expect(onClose).toHaveBeenCalled()

    fireEvent.click(screen.getByRole("link", { name: /Về trang khách thuê/ }))
    expect(path()).toBe("/")
  })

  it("groups sidebar items by domain and hides empty admin-only groups for Manager (6.1)", () => {
    const { unmount } = renderAt(
      "/management/dashboard",
      <ManagementSidebar user={admin} pathname="/management/dashboard" open onClose={vi.fn()} />,
    )

    expect(within(screen.getByRole("group", { name: "Tổng quan" })).getByRole("link", { name: "Dashboard" }).getAttribute("href")).toBe("/management/dashboard")
    expect(within(screen.getByRole("group", { name: "Khu trọ & Phòng" })).getByRole("link", { name: "Khu trọ" }).getAttribute("href")).toBe("/management/properties")

    const leasingGroup = screen.getByRole("group", { name: "Cho thuê" })
    expect(within(leasingGroup).getAllByRole("link").map((link) => link.textContent)).toEqual([
      "Lịch xem phòng",
      "Đặt phòng & Đặt cọc",
      "Hợp đồng",
      "Hồ sơ CCCD đặt cọc",
    ])

    const financeGroup = screen.getByRole("group", { name: "Tài chính" })
    expect(within(financeGroup).getAllByRole("link").map((link) => link.textContent)).toEqual([
      "Hóa đơn",
      "Đối soát thanh toán",
      "Xét duyệt hoàn cọc",
    ])

    expect(within(screen.getByRole("group", { name: "Vận hành" })).getByRole("link", { name: "Bảo trì / Sửa chữa" })).toBeTruthy()

    const adminGroup = screen.getByRole("group", { name: "Quản trị" })
    expect(within(adminGroup).getAllByRole("link").map((link) => link.textContent)).toEqual([
      "Người dùng",
      "Phân công Manager",
      "Báo cáo",
      "Nhật ký thao tác",
    ])

    unmount()

    renderAt(
      "/management/dashboard",
      <ManagementSidebar user={manager} pathname="/management/dashboard" open onClose={vi.fn()} />,
    )

    expect(screen.getByRole("group", { name: "Tổng quan" })).toBeTruthy()
    expect(screen.getByRole("group", { name: "Khu trọ & Phòng" })).toBeTruthy()
    expect(screen.getByRole("group", { name: "Cho thuê" })).toBeTruthy()
    expect(screen.getByRole("link", { name: "Hồ sơ CCCD đặt cọc" }).getAttribute("href")).toBe("/management/booking-identities")
    expect(within(screen.getByRole("group", { name: "Tài chính" })).getAllByRole("link").map((link) => link.textContent)).toEqual([
      "Hóa đơn",
    ])
    expect(screen.getByRole("group", { name: "Vận hành" })).toBeTruthy()
    expect(screen.queryByRole("group", { name: "Quản trị" })).toBeNull()
    expect(screen.queryByText("Quản trị")).toBeNull()
  })

  it("opens customer pages from the navbar account menu and closes the menu", () => {
    auth.current = { status: "authenticated", user: customer, logout: vi.fn() }
    renderAt("/", <Navbar />)

    fireEvent.click(screen.getByRole("link", { name: "Phòng của tôi" }))
    expect(path()).toBe("/my-room")

    fireEvent.click(screen.getByRole("button", { name: /Lan Anh/ }))
    fireEvent.click(screen.getByRole("link", { name: "Hợp đồng của tôi" }))
    expect(path()).toBe("/my-contracts")
    expect(screen.queryByRole("link", { name: "Hồ sơ cá nhân" })).toBeNull()

    fireEvent.click(screen.getByRole("link", { name: /TrọViệt/ }))
    expect(path()).toBe("/")
  })

  it("sends a guest to the login page from the navbar", () => {
    auth.current = { status: "anonymous", user: null }
    renderAt("/", <Navbar />)

    fireEvent.click(screen.getByRole("link", { name: "Đăng nhập" }))
    expect(path()).toBe("/login")
  })

  it("opens the profile from the management header menu and closes the menu", () => {
    auth.current = { status: "authenticated", user: admin, logout: vi.fn() }
    renderAt("/management/dashboard", <ManagementHeader user={admin} title="Dashboard" onMenu={vi.fn()} />)

    fireEvent.click(screen.getByRole("button", { name: /Quản Trị/ }))
    fireEvent.click(screen.getByRole("menuitem", { name: "Hồ sơ cá nhân" }))
    expect(path()).toBe("/profile")
    expect(screen.queryByRole("menuitem", { name: "Hồ sơ cá nhân" })).toBeNull()
  })

  it("returns home from the login and register shell", () => {
    renderAt("/login", <AuthShell><p>form</p></AuthShell>)

    fireEvent.click(screen.getByRole("link", { name: /Quay lại trang chủ/ }))
    expect(path()).toBe("/")
  })
})
