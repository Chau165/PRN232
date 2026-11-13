import { fireEvent, render, screen, waitFor } from "@testing-library/react"
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { MemoryRouter, Route, Routes, useLocation } from "react-router"
import { AuthProvider } from "@/auth/AuthContext"
import type { GoogleAccountsId, GoogleCredentialResponse } from "@/auth/googleIdentity"
import PhoneNumberBanner from "@/components/auth/PhoneNumberBanner"
import { roleLandingPath } from "@/components/auth/ProtectedRoute"
import LoginPage from "@/pages/LoginPage/LoginPage"
import ProfilePage from "@/pages/ProfilePage/ProfilePage"
import RegisterPage from "@/pages/RegisterPage/RegisterPage"

const SESSION_KEY = "troviet-auth-session-v1"

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  })
}

const googleUser = {
  userId: 40,
  fullName: "Google Customer",
  email: "google.customer@example.test",
  phoneNumber: "",
  roleId: 1,
  roleName: "Customer",
  isActive: true,
  hasPassword: false,
  phoneNumberMissing: true,
}

function LocationProbe() {
  return <p data-testid="location">{useLocation().pathname}</p>
}

function renderAt(path: string, element: React.ReactNode) {
  return render(
    <AuthProvider>
      <MemoryRouter initialEntries={[path]}>
        <PhoneNumberBanner />
        <Routes>
          <Route path={path} element={element} />
          <Route path="*" element={<LocationProbe />} />
        </Routes>
      </MemoryRouter>
    </AuthProvider>,
  )
}

function fakeGoogle() {
  let callback: ((response: GoogleCredentialResponse) => void) | undefined
  const api: GoogleAccountsId = {
    initialize: vi.fn((options) => {
      callback = options.callback
    }),
    renderButton: vi.fn(),
  }
  window.google = { accounts: { id: api } }
  return {
    api,
    signIn(credential: string) {
      if (!callback) throw new Error("Google was not initialized.")
      callback({ credential })
    },
  }
}

function storeSession() {
  window.sessionStorage.setItem(SESSION_KEY, JSON.stringify({
    accessToken: "backend.jwt",
    expiresAtUtc: new Date(Date.now() + 60_000).toISOString(),
  }))
}

beforeEach(() => {
  window.sessionStorage.clear()
})

afterEach(() => {
  vi.unstubAllEnvs()
  vi.unstubAllGlobals()
  delete window.google
})

describe("Sign in with Google", () => {
  it("hides the Google button when VITE_GOOGLE_CLIENT_ID is not set", async () => {
    vi.stubEnv("VITE_GOOGLE_CLIENT_ID", "")
    const google = fakeGoogle()
    vi.stubGlobal("fetch", vi.fn<typeof fetch>(async () => jsonResponse({ title: "Not Found" }, 404)))

    renderAt("/login", <LoginPage />)

    expect(await screen.findByLabelText("Email")).toBeTruthy()
    expect(screen.queryByTestId("google-signin-button")).toBeNull()
    expect(screen.queryByText("hoặc")).toBeNull()
    expect(google.api.initialize).not.toHaveBeenCalled()
  })

  it("renders the official button on the login and register pages when configured", async () => {
    vi.stubEnv("VITE_GOOGLE_CLIENT_ID", "client-id.apps.googleusercontent.com")
    const google = fakeGoogle()
    vi.stubGlobal("fetch", vi.fn<typeof fetch>(async () => jsonResponse({ title: "Not Found" }, 404)))

    const { unmount } = renderAt("/login", <LoginPage />)
    await waitFor(() => expect(google.api.renderButton).toHaveBeenCalledTimes(1))
    expect(google.api.initialize).toHaveBeenCalledWith(expect.objectContaining({
      client_id: "client-id.apps.googleusercontent.com",
    }))
    expect(vi.mocked(google.api.renderButton).mock.calls[0][1]).toMatchObject({ text: "signin_with" })
    unmount()

    renderAt("/register", <RegisterPage />)
    await waitFor(() => expect(google.api.renderButton).toHaveBeenCalledTimes(2))
    expect(vi.mocked(google.api.renderButton).mock.calls[1][1]).toMatchObject({ text: "signup_with" })
  })

  it("sends the Google credential to the backend and reuses the normal login flow", async () => {
    vi.stubEnv("VITE_GOOGLE_CLIENT_ID", "client-id.apps.googleusercontent.com")
    const google = fakeGoogle()
    const fetchMock = vi.fn<typeof fetch>(async (input) => {
      if (String(input).endsWith("/auth/google")) {
        return jsonResponse({
          accessToken: "backend.jwt.from.google",
          expiresAtUtc: new Date(Date.now() + 60_000).toISOString(),
          user: googleUser,
        })
      }
      return jsonResponse([], 200)
    })
    vi.stubGlobal("fetch", fetchMock)

    renderAt("/login", <LoginPage />)
    await waitFor(() => expect(google.api.initialize).toHaveBeenCalled())
    google.signIn("google-id-token")

    expect((await screen.findByTestId("location")).textContent).toBe(roleLandingPath("CUSTOMER"))
    const googleCall = fetchMock.mock.calls.find(([url]) => String(url).endsWith("/api/auth/google"))!
    expect(googleCall[1]?.method).toBe("POST")
    expect(JSON.parse(String(googleCall[1]?.body))).toEqual({ idToken: "google-id-token" })
    expect(JSON.parse(window.sessionStorage.getItem(SESSION_KEY)!).accessToken).toBe("backend.jwt.from.google")
    // The new account has no phone number, so the reminder banner appears right away.
    expect(screen.getByText(/chưa có số điện thoại/)).toBeTruthy()
  })

  it("shows the backend error when Google sign-in is refused", async () => {
    vi.stubEnv("VITE_GOOGLE_CLIENT_ID", "client-id.apps.googleusercontent.com")
    const google = fakeGoogle()
    vi.stubGlobal("fetch", vi.fn<typeof fetch>(async () =>
      jsonResponse({ title: "Account inactive", detail: "Tài khoản đã bị khoá." }, 403)))

    renderAt("/login", <LoginPage />)
    await waitFor(() => expect(google.api.initialize).toHaveBeenCalled())
    google.signIn("google-id-token")

    expect((await screen.findByRole("alert")).textContent).toContain("Tài khoản đã bị khoá.")
    expect(window.sessionStorage.getItem(SESSION_KEY)).toBeNull()
  })
})

describe("phone number completion", () => {
  it("shows a banner linking to the profile when the phone number is missing", async () => {
    storeSession()
    vi.stubGlobal("fetch", vi.fn<typeof fetch>(async () => jsonResponse(googleUser)))

    renderAt("/somewhere", <p>Trang bất kỳ</p>)

    const link = await screen.findByRole("link", { name: "Cập nhật trong trang cá nhân" })
    expect(link.getAttribute("href")).toBe("/profile")
  })

  it("hides the banner when the user has a phone number", async () => {
    storeSession()
    vi.stubGlobal("fetch", vi.fn<typeof fetch>(async () =>
      jsonResponse({ ...googleUser, phoneNumber: "0915000000", phoneNumberMissing: false })))

    renderAt("/somewhere", <p>Trang bất kỳ</p>)

    expect(await screen.findByText("Trang bất kỳ")).toBeTruthy()
    await waitFor(() => expect(screen.queryByText(/chưa có số điện thoại/)).toBeNull())
  })
})

describe("ProfilePage password section", () => {
  it("offers 'Đặt mật khẩu' without a current password for accounts that have none", async () => {
    storeSession()
    const fetchMock = vi.fn<typeof fetch>(async (input, init) => {
      if (String(input).endsWith("/users/me/password") && init?.method === "PUT") return jsonResponse(undefined, 204)
      return jsonResponse(googleUser)
    })
    vi.stubGlobal("fetch", fetchMock)

    renderAt("/profile", <ProfilePage />)

    expect(await screen.findByRole("heading", { name: "Đặt mật khẩu" })).toBeTruthy()
    expect(screen.queryByLabelText("Mật khẩu hiện tại")).toBeNull()
    // The banner stays hidden on the profile page; the profile shows its own hint instead.
    expect(screen.queryByRole("link", { name: "Cập nhật trong trang cá nhân" })).toBeNull()
    expect(screen.getByRole("note").textContent).toContain("chưa có số điện thoại")

    fireEvent.change(screen.getByLabelText("Mật khẩu mới"), { target: { value: "NewPassword123" } })
    fireEvent.change(screen.getByLabelText("Xác nhận mật khẩu mới"), { target: { value: "NewPassword123" } })
    fireEvent.click(screen.getByRole("button", { name: "Đặt mật khẩu" }))

    await waitFor(() => expect(screen.getByText("Mật khẩu đã được thiết lập.")).toBeTruthy())
    const call = fetchMock.mock.calls.find(([url, init]) =>
      String(url).endsWith("/users/me/password") && init?.method === "PUT")!
    expect(JSON.parse(String(call[1]?.body))).toEqual({ newPassword: "NewPassword123" })
    expect(await screen.findByRole("heading", { name: "Đổi mật khẩu" })).toBeTruthy()
  })

  it("keeps 'Đổi mật khẩu' with the current password for normal accounts", async () => {
    storeSession()
    vi.stubGlobal("fetch", vi.fn<typeof fetch>(async () =>
      jsonResponse({ ...googleUser, hasPassword: true, phoneNumber: "0915000000", phoneNumberMissing: false })))

    renderAt("/profile", <ProfilePage />)

    expect(await screen.findByRole("heading", { name: "Đổi mật khẩu" })).toBeTruthy()
    expect(screen.getByLabelText("Mật khẩu hiện tại")).toBeTruthy()
    expect(screen.queryByRole("note")).toBeNull()
  })
})
