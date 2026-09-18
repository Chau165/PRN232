import { useState } from "react"
import AuthShell from "@/components/auth/AuthShell"
import PasswordInput from "@/components/auth/PasswordInput"
import { routes } from "@/constants/routes"
import {
  authenticateMockUser,
  mockAccounts,
  startMockSession,
} from "@/utils/authMock"

export default function LoginPage() {
  const [email, setEmail] = useState("")
  const [password, setPassword] = useState("")
  const [submitted, setSubmitted] = useState(false)
  const [authError, setAuthError] = useState(false)
  const invalid = submitted && (!email || password.length < 4 || authError)

  const submit = (event: React.FormEvent) => {
    event.preventDefault()
    setSubmitted(true)
    const user = authenticateMockUser(email, password)
    if (!user) {
      setAuthError(true)
      return
    }
    setAuthError(false)
    startMockSession(user)
    window.location.assign(
      user.role === "CUSTOMER" ? routes.home : routes.managementDashboard,
    )
  }

  return (
    <AuthShell>
      <div className="w-full max-w-[430px] rounded-3xl bg-white p-8 shadow-[0_12px_42px_rgba(16,65,67,.10)]">
        <p className="text-[11px] font-bold uppercase tracking-[.16em] text-[#087775]">
          TroViet Account
        </p>
        <h1 className="mt-3 font-display text-[29px] font-bold tracking-[-.045em] text-slate-900">
          Chào mừng bạn trở lại
        </h1>
        <p className="mt-2 text-[13px] leading-6 text-slate-500">
          Đăng nhập để quản lý phòng thuê và tiếp tục sử dụng TroViệt.
        </p>
        <form onSubmit={submit} className="mt-7 space-y-4">
          <label className="block text-[12px] font-semibold text-slate-700">
            Email hoặc số điện thoại
            <input
              value={email}
              onChange={(event) => {
                setEmail(event.target.value)
                setAuthError(false)
              }}
              placeholder="Nhập email hoặc số điện thoại"
              className={`mt-2 h-12 w-full rounded-xl border bg-white px-3.5 text-[13px] outline-none transition placeholder:text-slate-400 ${
                invalid
                  ? "border-rose-300 ring-2 ring-rose-50"
                  : "border-slate-200 focus:border-[#087775] focus:ring-2 focus:ring-teal-50"
              }`}
            />
          </label>
          <label className="block text-[12px] font-semibold text-slate-700">
            Mật khẩu
            <div className="mt-2">
              <PasswordInput
                value={password}
                onChange={(value) => {
                  setPassword(value)
                  setAuthError(false)
                }}
                invalid={invalid}
              />
            </div>
          </label>
          {invalid && (
            <p className="rounded-lg bg-rose-50 px-3 py-2 text-[12px] text-rose-600">
              Email hoặc mật khẩu không chính xác.
            </p>
          )}
          <div className="flex items-center justify-between pt-1">
            <label className="flex items-center gap-2 text-[12px] text-slate-500">
              <input type="checkbox" className="h-4 w-4 accent-[#087775]" />
              Ghi nhớ đăng nhập
            </label>
            <button
              type="button"
              className="text-[12px] font-semibold text-[#087775] hover:underline"
            >
              Quên mật khẩu?
            </button>
          </div>
          <button
            type="submit"
            className="mt-2 w-full rounded-lg bg-[#087775] py-3 text-[13px] font-bold text-white shadow-sm hover:bg-[#066a68]"
          >
            Đăng nhập
          </button>
        </form>
        <div className="mt-6 rounded-xl border border-teal-100 bg-teal-50/60 p-3">
          <p className="text-[11px] font-bold uppercase tracking-[.12em] text-[#087775]">
            Tài khoản demo
          </p>
          <div className="mt-2 space-y-1.5 text-[11px] text-slate-600">
            {mockAccounts.map((account) => (
              <p key={account.email}>
                <span className="font-semibold text-slate-700">
                  {account.user.role}:
                </span>{" "}
                {account.email} / {account.password}
              </p>
            ))}
          </div>
        </div>
        <p className="mt-6 text-center text-[12px] text-slate-500">
          Chưa có tài khoản?{" "}
          <a
            href="/register"
            className="font-bold text-[#087775] hover:underline"
          >
            Đăng ký ngay
          </a>
        </p>
      </div>
    </AuthShell>
  )
}
