import { useState, type FormEvent } from "react"
import { Link, useNavigate } from "react-router"
import { useAuth } from "@/auth/AuthContext"
import AuthShell from "@/components/auth/AuthShell"
import PasswordInput from "@/components/auth/PasswordInput"
import { roleLandingPath } from "@/components/auth/ProtectedRoute"

export default function RegisterPage() {
  const navigate = useNavigate()
  const { register } = useAuth()
  const [fullName, setFullName] = useState("")
  const [phoneNumber, setPhoneNumber] = useState("")
  const [email, setEmail] = useState("")
  const [password, setPassword] = useState("")
  const [confirmPassword, setConfirmPassword] = useState("")
  const [accepted, setAccepted] = useState(false)
  const [error, setError] = useState("")
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError("")
    if (password.length < 6) {
      setError("Mật khẩu phải có ít nhất 6 ký tự.")
      return
    }
    if (password !== confirmPassword) {
      setError("Mật khẩu xác nhận chưa khớp.")
      return
    }
    if (!accepted) {
      setError("Vui lòng đồng ý với điều khoản sử dụng.")
      return
    }

    setIsSubmitting(true)
    try {
      const user = await register({
        fullName: fullName.trim(),
        email: email.trim(),
        password,
        phoneNumber: phoneNumber.trim(),
      })
      navigate(roleLandingPath(user.role), { replace: true })
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Đăng ký không thành công.")
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <AuthShell>
      <div className="w-full max-w-[450px] rounded-3xl bg-white p-8 shadow-[0_12px_42px_rgba(16,65,67,.10)]">
        <p className="text-[11px] font-bold uppercase tracking-[.16em] text-[#087775]">
          TroViet Account
        </p>
        <h1 className="mt-3 font-display text-[29px] font-bold tracking-[-.045em] text-slate-900">
          Tạo tài khoản TroViệt
        </h1>
        <p className="mt-2 text-[13px] leading-6 text-slate-500">
          Đăng ký tài khoản để đặt lịch xem, đặt cọc và quản lý phòng thuê của bạn.
        </p>
        <form onSubmit={submit} className="mt-7 space-y-3.5">
          <label className="block text-[12px] font-semibold text-slate-700">
            Họ và tên
            <input
              required
              autoComplete="name"
              value={fullName}
              onChange={(event) => setFullName(event.target.value)}
              placeholder="Nhập họ và tên"
              className="mt-2 h-11 w-full rounded-xl border border-slate-200 bg-white px-3.5 text-[13px] outline-none transition placeholder:text-slate-400 focus:border-[#087775] focus:ring-2 focus:ring-teal-50"
            />
          </label>
          <label className="block text-[12px] font-semibold text-slate-700">
            Số điện thoại
            <input
              required
              type="tel"
              autoComplete="tel"
              value={phoneNumber}
              onChange={(event) => setPhoneNumber(event.target.value)}
              placeholder="Nhập số điện thoại"
              className="mt-2 h-11 w-full rounded-xl border border-slate-200 bg-white px-3.5 text-[13px] outline-none transition placeholder:text-slate-400 focus:border-[#087775] focus:ring-2 focus:ring-teal-50"
            />
          </label>
          <label className="block text-[12px] font-semibold text-slate-700">
            Email
            <input
              required
              type="email"
              autoComplete="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              placeholder="Nhập địa chỉ email"
              className="mt-2 h-11 w-full rounded-xl border border-slate-200 bg-white px-3.5 text-[13px] outline-none transition placeholder:text-slate-400 focus:border-[#087775] focus:ring-2 focus:ring-teal-50"
            />
          </label>
          <label className="block text-[12px] font-semibold text-slate-700">
            Mật khẩu
            <div className="mt-2">
              <PasswordInput value={password} onChange={setPassword} />
            </div>
          </label>
          <label className="block text-[12px] font-semibold text-slate-700">
            Xác nhận mật khẩu
            <div className="mt-2">
              <PasswordInput value={confirmPassword} onChange={setConfirmPassword} />
            </div>
          </label>
          <label className="flex items-start gap-2 pt-1 text-[11px] leading-5 text-slate-500">
            <input
              type="checkbox"
              checked={accepted}
              onChange={(event) => setAccepted(event.target.checked)}
              className="mt-0.5 h-4 w-4 shrink-0 accent-[#087775]"
            />
            Tôi đồng ý với <span className="font-semibold text-[#087775]">Điều khoản sử dụng</span>
            và <span className="font-semibold text-[#087775]">Chính sách bảo mật.</span>
          </label>
          {error && <p role="alert" className="text-[12px] text-rose-700">{error}</p>}
          <button
            type="submit"
            disabled={isSubmitting}
            className="mt-2 w-full rounded-lg bg-[#087775] py-3 text-[13px] font-bold text-white shadow-sm hover:bg-[#066a68] disabled:cursor-wait disabled:opacity-60"
          >
            {isSubmitting ? "Đang tạo tài khoản..." : "Đăng ký"}
          </button>
        </form>
        <p className="mt-6 text-center text-[12px] text-slate-500">
          Đã có tài khoản?{" "}
          <Link to="/login" className="font-bold text-[#087775] hover:underline">Đăng nhập</Link>
        </p>
      </div>
    </AuthShell>
  )
}
