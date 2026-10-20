import { fireEvent, render, screen, waitFor } from "@testing-library/react"
import { beforeEach, describe, expect, it, vi } from "vitest"
import type { ReactNode } from "react"
import { MemoryRouter, Route, Routes, useLocation } from "react-router"
import { AuthProvider } from "@/auth/AuthContext"
import { ProtectedRoute } from "@/components/auth/ProtectedRoute"
import { apiRequest } from "@/api/apiClient"
import LoginPage from "@/pages/LoginPage/LoginPage"
import ProfilePage from "@/pages/ProfilePage/ProfilePage"
import RegisterPage from "@/pages/RegisterPage/RegisterPage"

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  })
}

function authResponse(roleName: string, userId = 7) {
  return {
    accessToken: "backend.jwt.token",
    expiresAtUtc: new Date(Date.now() + 60_000).toISOString(),
    user: {
      userId,
      fullName: "Người Dùng Thật",
      email: "person@example.test",
      phoneNumber: "0900123456",
      roleId: 9,
      roleName,
    },
  }
}

function currentUserResponse(overrides: Record<string, unknown> = {}) {
  return {
    userId: 7,
    fullName: "Người Dùng Thật",
    email: "person@example.test",
    phoneNumber: "0900123456",
    roleId: 9,
    roleName: "Customer",
    isActive: true,
    ...overrides,
  }
}

function CurrentPath() {
  return <output data-testid="current-path">{useLocation().pathname}</output>
}

function renderRoutes(initialPath: string, children: ReactNode) {
  return render(
    <MemoryRouter initialEntries={[initialPath]}>
      <AuthProvider>
        <CurrentPath />
        {children}
      </AuthProvider>
    </MemoryRouter>,
  )
}

function storeSession(expiresAtUtc = new Date(Date.now() + 60_000).toISOString()) {
  window.sessionStorage.setItem("troviet-auth-session-v1", JSON.stringify({
    accessToken: "stored.jwt.token",
    expiresAtUtc,
  }))
}

beforeEach(() => window.sessionStorage.clear())

describe("Phase 4A auth, routing, and profile integration", () => {
  it("submits login to the backend and does not expose demo credentials", async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(
      jsonResponse({ title: "Invalid credentials" }, 401),
    )
    vi.stubGlobal("fetch", fetchMock)

    render(
      <MemoryRouter initialEntries={["/login"]}>
        <AuthProvider><LoginPage /></AuthProvider>
      </MemoryRouter>,
    )
    fireEvent.change(screen.getByLabelText("Email"), {
      target: { value: "person@example.test" },
    })
    fireEvent.change(screen.getByLabelText("Mật khẩu"), {
      target: { value: "password-123" },
    })
    fireEvent.click(screen.getByRole("button", { name: "Đăng nhập" }))

    await waitFor(() => expect(fetchMock).toHaveBeenCalled())
    expect(String(fetchMock.mock.calls[0]?.[0])).toContain("/api/auth/login")
    expect(JSON.parse(String(fetchMock.mock.calls[0]?.[1]?.body))).toEqual({
      email: "person@example.test",
      password: "password-123",
    })
    expect(screen.queryByText(/Tài khoản demo/i)).toBeNull()
    expect(screen.queryByText(/admin123|customer123|manager123/i)).toBeNull()
  })

  it.each([
    ["Customer", "/"],
    ["Manager", "/management/dashboard"],
    ["Admin", "/management/dashboard"],
  ])("redirects %s to its existing landing route", async (roleName, expectedPath) => {
    vi.stubGlobal("fetch", vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(authResponse(roleName))))
    renderRoutes("/login", (
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/" element={<p>Trang khách thuê</p>} />
        <Route path="/management/dashboard" element={<p>Trang quản trị</p>} />
      </Routes>
    ))
    fireEvent.change(screen.getByLabelText("Email"), { target: { value: "person@example.test" } })
    fireEvent.change(screen.getByLabelText("Mật khẩu"), { target: { value: "password-123" } })
    fireEvent.click(screen.getByRole("button", { name: "Đăng nhập" }))
    await waitFor(() => expect(screen.getByTestId("current-path").textContent).toBe(expectedPath))
  })

  it("registers using only the public customer fields, with no OTP or role selector", async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(authResponse("Customer")))
    vi.stubGlobal("fetch", fetchMock)
    render(
      <MemoryRouter initialEntries={["/register"]}>
        <AuthProvider><RegisterPage /></AuthProvider>
      </MemoryRouter>,
    )

    expect(screen.queryByLabelText(/OTP/i)).toBeNull()
    expect(screen.queryByText(/123456/)).toBeNull()
    expect(screen.queryByLabelText(/role/i)).toBeNull()
    fireEvent.change(screen.getByLabelText("Họ và tên"), { target: { value: "Người Dùng Thật" } })
    fireEvent.change(screen.getByLabelText("Số điện thoại"), { target: { value: "0900123456" } })
    fireEvent.change(screen.getByLabelText("Email"), { target: { value: "person@example.test" } })
    const passwordInputs = screen.getAllByPlaceholderText("Nhập mật khẩu")
    fireEvent.change(passwordInputs[0]!, { target: { value: "password-123" } })
    fireEvent.change(passwordInputs[1]!, { target: { value: "password-123" } })
    fireEvent.click(screen.getByRole("checkbox"))
    fireEvent.click(screen.getByRole("button", { name: "Đăng ký" }))

    await waitFor(() => expect(fetchMock).toHaveBeenCalled())
    expect(String(fetchMock.mock.calls[0]?.[0])).toContain("/api/auth/register")
    const request = JSON.parse(String(fetchMock.mock.calls[0]?.[1]?.body))
    expect(request).toEqual({
      fullName: "Người Dùng Thật",
      phoneNumber: "0900123456",
      email: "person@example.test",
      password: "password-123",
    })
    expect(request).not.toHaveProperty("roleId")
    expect(request).not.toHaveProperty("roleName")
  })

  it("revalidates a stored session with Bearer and denies a role-ineligible route", async () => {
    storeSession()
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(currentUserResponse()))
    vi.stubGlobal("fetch", fetchMock)
    renderRoutes("/management/users", (
      <Routes>
        <Route path="/management/users" element={<ProtectedRoute roles={["ADMIN"]}><p>Quản lý user</p></ProtectedRoute>} />
      </Routes>
    ))

    expect(await screen.findByText("Không có quyền truy cập")).toBeTruthy()
    expect(String(fetchMock.mock.calls[0]?.[0])).toContain("/api/users/me")
    expect(new Headers(fetchMock.mock.calls[0]?.[1]?.headers).get("Authorization")).toBe("Bearer stored.jwt.token")
    expect(window.sessionStorage.getItem("troviet-auth-session-v1")).not.toBeNull()
  })

  it("clears expired and 401 sessions while retaining a session after 403", async () => {
    storeSession(new Date(Date.now() - 1_000).toISOString())
    const fetchMock = vi.fn<typeof fetch>()
    vi.stubGlobal("fetch", fetchMock)
    const expiredView = renderRoutes("/private", (
      <Routes>
        <Route path="/private" element={<ProtectedRoute><p>Private</p></ProtectedRoute>} />
        <Route path="/login" element={<p>Login</p>} />
      </Routes>
    ))
    await waitFor(() => expect(screen.getByTestId("current-path").textContent).toBe("/login"))
    expect(fetchMock).not.toHaveBeenCalled()
    expect(window.sessionStorage.getItem("troviet-auth-session-v1")).toBeNull()
    expiredView.unmount()

    storeSession()
    fetchMock.mockResolvedValueOnce(jsonResponse({ title: "Unauthorized" }, 401))
    const unauthorizedView = renderRoutes("/private", (
      <Routes>
        <Route path="/private" element={<ProtectedRoute><p>Private</p></ProtectedRoute>} />
        <Route path="/login" element={<p>Login</p>} />
      </Routes>
    ))
    await waitFor(() => expect(screen.getByTestId("current-path").textContent).toBe("/login"))
    expect(window.sessionStorage.getItem("troviet-auth-session-v1")).toBeNull()
    unauthorizedView.unmount()

    storeSession()
    fetchMock.mockResolvedValueOnce(jsonResponse({ title: "Forbidden" }, 403))
    await expect(apiRequest("/users/me", { accessToken: "stored.jwt.token" })).rejects.toMatchObject({ status: 403 })
    expect(window.sessionStorage.getItem("troviet-auth-session-v1")).not.toBeNull()
  })

  it("expires an active session at the backend token expiry time", async () => {
    storeSession(new Date(Date.now() + 250).toISOString())
    vi.stubGlobal("fetch", vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(currentUserResponse())))
    renderRoutes("/private", (
      <Routes>
        <Route path="/private" element={<ProtectedRoute><p>Private</p></ProtectedRoute>} />
        <Route path="/login" element={<p>Login</p>} />
      </Routes>
    ))

    expect(await screen.findByText("Private")).toBeTruthy()
    await waitFor(() => expect(window.sessionStorage.getItem("troviet-auth-session-v1")).toBeNull(), { timeout: 2_000 })
    await waitFor(() => expect(screen.getByTestId("current-path").textContent).toBe("/login"))
  })

  it("does not show a demo profile and refreshes the navbar from the profile API", async () => {
    storeSession()
    const fetchMock = vi.fn<typeof fetch>()
      .mockResolvedValueOnce(jsonResponse(currentUserResponse()))
      .mockResolvedValueOnce(jsonResponse(currentUserResponse({ fullName: "Tên Mới" })))
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
    vi.stubGlobal("fetch", fetchMock)
    render(
      <MemoryRouter initialEntries={["/profile"]}>
        <AuthProvider><ProfilePage /></AuthProvider>
      </MemoryRouter>,
    )

    expect(await screen.findByRole("heading", { name: "Người Dùng Thật" })).toBeTruthy()
    expect(screen.queryByText("Ngọc Anh")).toBeNull()
    fireEvent.click(screen.getByRole("button", { name: "Chỉnh sửa hồ sơ" }))
    fireEvent.change(screen.getByLabelText("Họ và tên"), { target: { value: "Tên Mới" } })
    fireEvent.click(screen.getByRole("button", { name: "Lưu thay đổi" }))
    await waitFor(() => expect(screen.getByRole("heading", { name: "Tên Mới" })).toBeTruthy())
    expect(String(fetchMock.mock.calls[1]?.[0])).toContain("/api/users/me")
    expect(fetchMock.mock.calls[1]?.[1]?.method).toBe("PUT")
    expect(JSON.parse(String(fetchMock.mock.calls[1]?.[1]?.body))).toEqual({ fullName: "Tên Mới", phoneNumber: "0900123456" })

    fireEvent.change(screen.getByLabelText("Mật khẩu hiện tại"), { target: { value: "old-password" } })
    fireEvent.change(screen.getByLabelText("Mật khẩu mới"), { target: { value: "new-password" } })
    fireEvent.change(screen.getByLabelText("Xác nhận mật khẩu mới"), { target: { value: "new-password" } })
    fireEvent.click(screen.getByRole("button", { name: "Cập nhật mật khẩu" }))
    await waitFor(() => expect(screen.getByText("Mật khẩu đã được thay đổi.")).toBeTruthy())
    expect(String(fetchMock.mock.calls[2]?.[0])).toContain("/api/users/me/password")
    expect(JSON.parse(String(fetchMock.mock.calls[2]?.[1]?.body))).toEqual({ currentPassword: "old-password", newPassword: "new-password" })
    expect(JSON.parse(String(fetchMock.mock.calls[2]?.[1]?.body))).not.toHaveProperty("confirmPassword")

    fireEvent.click(screen.getByRole("button", { name: /Tên Mới/ }))
    fireEvent.click(screen.getByRole("button", { name: "Đăng xuất" }))
    expect(window.sessionStorage.getItem("troviet-auth-session-v1")).toBeNull()
  })

  it("fails closed and clears a stored token when the backend returns an unknown role", async () => {
    storeSession()
    vi.stubGlobal("fetch", vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(currentUserResponse({ roleName: "Root" }))))
    renderRoutes("/private", (
      <Routes>
        <Route path="/private" element={<ProtectedRoute><p>Private</p></ProtectedRoute>} />
        <Route path="/login" element={<p>Login</p>} />
      </Routes>
    ))
    await waitFor(() => expect(screen.getByTestId("current-path").textContent).toBe("/login"))
    expect(window.sessionStorage.getItem("troviet-auth-session-v1")).toBeNull()
  })
})
