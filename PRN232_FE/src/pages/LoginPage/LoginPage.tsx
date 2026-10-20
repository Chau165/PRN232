import { useState, type FormEvent } from "react"
import { Link, useNavigate } from "react-router"
import AuthShell from "@/components/auth/AuthShell"
import PasswordInput from "@/components/auth/PasswordInput"
import { useAuth } from "@/auth/AuthContext"
import { roleLandingPath } from "@/components/auth/ProtectedRoute"

export default function LoginPage() {
  const navigate = useNavigate()
  const { login } = useAuth()
  const [email, setEmail] = useState("")
  const [password, setPassword] = useState("")
  const [error, setError] = useState("")
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError("")
    setIsSubmitting(true)
    try {
      const user = await login(email.trim(), password)
      navigate(roleLandingPath(user.role), { replace: true })
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Đăng nhập không thành công.")
    } finally {
      setIsSubmitting(false)
    }
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
            Email
            <input
              aria-label="Email"
              type="email"
              autoComplete="email"
              required
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              placeholder="Nhập địa chỉ email"
              className="mt-2 h-12 w-full rounded-xl border border-slate-200 bg-white px-3.5 text-[13px] outline-none transition placeholder:text-slate-400 focus:border-[#087775] focus:ring-2 focus:ring-teal-50"
            />
          </label>
          <label className="block text-[12px] font-semibold text-slate-700">
            Mật khẩu
            <div className="mt-2">
              <PasswordInput value={password} onChange={setPassword} />
            </div>
          </label>
          {error && (
            <p role="alert" className="rounded-lg bg-rose-50 px-3 py-2 text-[12px] text-rose-700">
              {error}
            </p>
          )}
          <button
            type="submit"
            disabled={isSubmitting}
            className="mt-2 w-full rounded-lg bg-[#087775] py-3 text-[13px] font-bold text-white shadow-sm hover:bg-[#066a68] disabled:cursor-wait disabled:opacity-60"
          >
            {isSubmitting ? "Đang đăng nhập..." : "Đăng nhập"}
          </button>
        </form>
        <p className="mt-6 text-center text-[12px] text-slate-500">
          Chưa có tài khoản?{" "}
          <Link to="/register" className="font-bold text-[#087775] hover:underline">
            Đăng ký ngay
          </Link>
        </p>
      </div>
    </AuthShell>
  )
}
