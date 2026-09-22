import { useState } from "react"
import { useNavigate } from "react-router"
import AuthShell from "@/components/auth/AuthShell"
import PasswordInput from "@/components/auth/PasswordInput"
import { registerMockAccount } from "@/utils/authMock"
import { routes } from "@/constants/routes"

const fields = [
  ["Họ và tên", "Nhập họ và tên"],
  ["Số điện thoại", "Nhập số điện thoại"],
  ["Email", "Nhập địa chỉ email"],
]

export default function RegisterPage() {
  const navigate = useNavigate()
  const [name, setName] = useState("")
  const [phone, setPhone] = useState("")
  const [email, setEmail] = useState("")
  const [otp, setOtp] = useState("")
  const [password, setPassword] = useState("")
  const [confirm, setConfirm] = useState("")
  const [accepted, setAccepted] = useState(false)
  const [submitted, setSubmitted] = useState(false)
  const [registered, setRegistered] = useState(false)
  const invalid = submitted && (password !== confirm || otp !== "123456" || !accepted || !name || !phone || !email)

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
          Đăng ký tài khoản để đặt lịch xem, đặt cọc và quản lý phòng thuê của
          bạn.
        </p>
        <form
          onSubmit={(event) => {
            event.preventDefault()
            setSubmitted(true)
            if (password === confirm && otp === "123456" && accepted && name && phone && email) {
              const user = { id: `customer-${Date.now()}`, name, email, phone, role: "CUSTOMER" as const }
              registerMockAccount({ email, password, user })
              setRegistered(true)
              window.setTimeout(() => navigate(routes.login), 1500)
            }
          }}
          className="mt-7 space-y-3.5"
        >
          {fields.map(([label, placeholder], index) => (
            <label
              key={label}
              className="block text-[12px] font-semibold text-slate-700"
            >
              {label}
              <input
                placeholder={placeholder}
                value={[name, phone, email][index]}
                onChange={(event) => [setName, setPhone, setEmail][index](event.target.value)}
                className="mt-2 h-11 w-full rounded-xl border border-slate-200 bg-white px-3.5 text-[13px] outline-none transition placeholder:text-slate-400 focus:border-[#087775] focus:ring-2 focus:ring-teal-50"
              />
            </label>
          ))}
          <label className="block text-[12px] font-semibold text-slate-700">
            Mã OTP
            <input
              inputMode="numeric"
              maxLength={6}
              value={otp}
              onChange={(event) => setOtp(event.target.value.replace(/\D/g, "").slice(0, 6))}
              placeholder="Nhập mã OTP 6 chữ số"
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
              <PasswordInput
                value={confirm}
                onChange={setConfirm}
                invalid={invalid}
              />
            </div>
          </label>
          {invalid && (
            <p className="text-[12px] text-rose-600">
              Mật khẩu xác nhận chưa khớp.
            </p>
          )}
          <label className="flex items-start gap-2 pt-1 text-[11px] leading-5 text-slate-500">
            <input
              type="checkbox"
              checked={accepted}
              onChange={(event) => setAccepted(event.target.checked)}
              className="mt-0.5 h-4 w-4 shrink-0 accent-[#087775]"
            />
            Tôi đồng ý với{" "}
            <span className="font-semibold text-[#087775]">
              Điều khoản sử dụng
            </span>{" "}
            và{" "}
            <span className="font-semibold text-[#087775]">
              Chính sách bảo mật.
            </span>
          </label>
          <button
            type="submit"
            className="mt-2 w-full rounded-lg bg-[#087775] py-3 text-[13px] font-bold text-white shadow-sm hover:bg-[#066a68]"
          >
            Đăng ký
          </button>
          {submitted && otp !== "123456" && (
            <p className="text-[12px] text-rose-600">Mã OTP không đúng. Với bản demo, hãy nhập 123456.</p>
          )}
          {registered && (
            <p className="rounded-lg bg-emerald-50 px-3 py-2 text-[12px] text-emerald-700">Đã tạo tài khoản thành công</p>
          )}
        </form>
        <p className="mt-6 text-center text-[12px] text-slate-500">
          Đã có tài khoản?{" "}
          <a href="/login" className="font-bold text-[#087775] hover:underline">
            Đăng nhập
          </a>
        </p>
      </div>
    </AuthShell>
  )
}
