import type { ReactNode } from "react"
import { Navigate, useLocation } from "react-router"
import { useAuth } from "@/auth/AuthContext"
import type { AppRole } from "@/types/user"
import { routes } from "@/constants/routes"

export function roleLandingPath(role: AppRole) {
  return role === "CUSTOMER" ? routes.home : routes.managementDashboard
}

function SessionStatus({ message }: { message: string }) {
  const { retrySessionCheck } = useAuth()
  return (
    <main className="grid min-h-[50vh] place-items-center px-6 text-center">
      <div>
        <p role="status" className="text-sm text-slate-600">{message}</p>
        <button
          onClick={retrySessionCheck}
          className="mt-4 rounded-lg bg-[#087775] px-4 py-2 text-sm font-semibold text-white"
        >
          Thử lại
        </button>
      </div>
    </main>
  )
}

export function ProtectedRoute({
  children,
  roles,
}: {
  children: ReactNode
  roles?: readonly AppRole[]
}) {
  const { status, user, error } = useAuth()
  const location = useLocation()

  if (status === "loading") return <SessionStatus message="Đang kiểm tra phiên đăng nhập..." />
  if (status === "error") {
    return <SessionStatus message={error ?? "Không thể xác minh phiên đăng nhập."} />
  }
  if (status !== "authenticated" || !user) {
    return (
      <Navigate
        to={routes.login}
        replace
        state={{ from: `${location.pathname}${location.search}${location.hash}` }}
      />
    )
  }
  if (roles && !roles.includes(user.role)) {
    return (
      <main className="grid min-h-[50vh] place-items-center px-6 text-center">
        <div>
          <h1 className="font-display text-2xl font-bold text-slate-900">Không có quyền truy cập</h1>
          <p className="mt-2 text-sm text-slate-600">Tài khoản của bạn không được phép mở trang này.</p>
          <a className="mt-4 inline-block font-semibold text-[#087775]" href={roleLandingPath(user.role)}>
            Quay lại trang phù hợp
          </a>
        </div>
      </main>
    )
  }
  return children
}

export function GuestOnlyRoute({ children }: { children: ReactNode }) {
  const { status, user, error } = useAuth()
  if (status === "loading") return <SessionStatus message="Đang kiểm tra phiên đăng nhập..." />
  if (status === "error") {
    return <SessionStatus message={error ?? "Không thể xác minh phiên đăng nhập."} />
  }
  if (status === "authenticated" && user) {
    return <Navigate to={roleLandingPath(user.role)} replace />
  }
  return children
}
