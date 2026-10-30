import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react"
import { ApiError, setUnauthorizedHandler } from "@/api/apiClient"
import * as authService from "@/services/authService"
import * as userService from "@/services/userService"
import { mapBackendUser, type AuthResponseDto } from "@/types/auth"
import type { CurrentUser } from "@/types/user"
import { setCurrentUserSnapshot } from "@/auth/currentUserStore"

const SESSION_STORAGE_KEY = "troviet-auth-session-v1"

type AuthState =
  | { status: "loading" }
  | { status: "anonymous" }
  | { status: "error"; error: string }
  | {
      status: "authenticated"
      user: CurrentUser
      accessToken: string
      expiresAtUtc: string
    }

interface StoredSession {
  accessToken: string
  expiresAtUtc: string
}

interface AuthContextValue {
  status: AuthState["status"]
  user: CurrentUser | null
  accessToken: string | null
  error: string | null
  login(email: string, password: string): Promise<CurrentUser>
  register(input: {
    fullName: string
    email: string
    password: string
    phoneNumber: string
  }): Promise<CurrentUser>
  logout(): void
  retrySessionCheck(): void
  updateProfile(input: { fullName: string; phoneNumber: string }): Promise<CurrentUser>
  changePassword(input: {
    currentPassword: string
    newPassword: string
  }): Promise<void>
}

const AuthContext = createContext<AuthContextValue | null>(null)

function readStoredSession(): StoredSession | null {
  try {
    const raw = window.sessionStorage.getItem(SESSION_STORAGE_KEY)
    if (!raw) return null
    const parsed = JSON.parse(raw) as Partial<StoredSession>
    if (
      typeof parsed.accessToken !== "string" ||
      !parsed.accessToken ||
      typeof parsed.expiresAtUtc !== "string" ||
      !Number.isFinite(Date.parse(parsed.expiresAtUtc))
    ) {
      return null
    }
    return { accessToken: parsed.accessToken, expiresAtUtc: parsed.expiresAtUtc }
  } catch {
    return null
  }
}

function clearStoredSession() {
  try {
    window.sessionStorage.removeItem(SESSION_STORAGE_KEY)
  } catch {
    // An unavailable session store must not keep the in-memory session alive.
  }
}

function saveStoredSession(session: StoredSession) {
  try {
    window.sessionStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(session))
  } catch {
    throw new ApiError("Không thể lưu phiên đăng nhập trong tab hiện tại.")
  }
}

function errorMessage(error: unknown) {
  return error instanceof Error
    ? error.message
    : "Không thể xác minh phiên đăng nhập. Vui lòng thử lại."
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState>({ status: "loading" })
  const [sessionCheckVersion, setSessionCheckVersion] = useState(0)

  const logout = useCallback(() => {
    clearStoredSession()
    setCurrentUserSnapshot(null)
    setState({ status: "anonymous" })
  }, [])

  useEffect(() => {
    setUnauthorizedHandler(logout)
    return () => setUnauthorizedHandler(() => undefined)
  }, [logout])

  useEffect(() => {
    let active = true
    const session = readStoredSession()
    if (!session) {
      clearStoredSession()
      setCurrentUserSnapshot(null)
      setState({ status: "anonymous" })
      return () => {
        active = false
      }
    }

    if (Date.parse(session.expiresAtUtc) <= Date.now()) {
      clearStoredSession()
      setCurrentUserSnapshot(null)
      setState({ status: "anonymous" })
      return () => {
        active = false
      }
    }

    setState({ status: "loading" })
    void userService.getCurrentUser(session.accessToken)
      .then((dto) => {
        if (!active) return
        const user = mapBackendUser(dto)
        if (user.isActive === false) {
          logout()
          return
        }
        setCurrentUserSnapshot(user)
        setState({ ...session, status: "authenticated", user })
      })
      .catch((error: unknown) => {
        if (!active) return
        if (
          !(error instanceof ApiError) ||
          error.status === 401 ||
          error.status === 404
        ) {
          logout()
          return
        }
        setState({ status: "error", error: errorMessage(error) })
      })

    return () => {
      active = false
    }
  }, [logout, sessionCheckVersion])

  useEffect(() => {
    if (state.status !== "authenticated") return
    let timer: number
    const expireOrReschedule = () => {
      const remaining = Date.parse(state.expiresAtUtc) - Date.now()
      if (remaining <= 0) {
        logout()
        return
      }
      timer = window.setTimeout(expireOrReschedule, Math.min(remaining, 2_147_000_000))
    }
    expireOrReschedule()
    return () => window.clearTimeout(timer)
  }, [logout, state])

  const establishSession = useCallback((response: AuthResponseDto) => {
    const expiresAt = Date.parse(response.expiresAtUtc)
    if (
      typeof response.accessToken !== "string" ||
      !response.accessToken ||
      !Number.isFinite(expiresAt) ||
      expiresAt <= Date.now()
    ) {
      throw new ApiError("Máy chủ trả về phiên đăng nhập không hợp lệ.")
    }
    const user = mapBackendUser(response.user)
    const session = {
      accessToken: response.accessToken,
      expiresAtUtc: response.expiresAtUtc,
    }
    saveStoredSession(session)
    setCurrentUserSnapshot(user)
    setState({ ...session, status: "authenticated", user })
    return user
  }, [])

  const login = useCallback(
    async (email: string, password: string) => {
      return establishSession(await authService.login({ email, password }))
    },
    [establishSession],
  )

  const register = useCallback(
    async (input: {
      fullName: string
      email: string
      password: string
      phoneNumber: string
    }) => establishSession(await authService.register(input)),
    [establishSession],
  )

  const retrySessionCheck = useCallback(() => {
    setSessionCheckVersion((version) => version + 1)
  }, [])

  const updateProfile = useCallback(
    async (input: { fullName: string; phoneNumber: string }) => {
      if (state.status !== "authenticated") throw new ApiError("Bạn cần đăng nhập lại.", { status: 401 })
      const dto = await userService.updateCurrentUser(state.accessToken, input)
      const storedSession = readStoredSession()
      if (
        !storedSession ||
        storedSession.accessToken !== state.accessToken ||
        Date.parse(storedSession.expiresAtUtc) <= Date.now()
      ) {
        logout()
        throw new ApiError("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.", { status: 401 })
      }
      const user = mapBackendUser(dto)
      if (user.isActive === false) {
        logout()
        throw new ApiError("Tài khoản đã bị vô hiệu hóa.", { status: 403 })
      }
      setCurrentUserSnapshot(user)
      setState({ ...state, user })
      return user
    },
    [logout, state],
  )

  const changePassword = useCallback(
    async (input: { currentPassword: string; newPassword: string }) => {
      if (state.status !== "authenticated") throw new ApiError("Bạn cần đăng nhập lại.", { status: 401 })
      await userService.changeCurrentPassword(state.accessToken, input)
    },
    [state],
  )

  const value = useMemo<AuthContextValue>(() => ({
    status: state.status,
    user: state.status === "authenticated" ? state.user : null,
    accessToken: state.status === "authenticated" ? state.accessToken : null,
    error: state.status === "error" ? state.error : null,
    login,
    register,
    logout,
    retrySessionCheck,
    updateProfile,
    changePassword,
  }), [state, login, register, logout, retrySessionCheck, updateProfile, changePassword])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) throw new Error("useAuth must be used within AuthProvider.")
  return context
}

export function useOptionalAuth() {
  return useContext(AuthContext)
}
