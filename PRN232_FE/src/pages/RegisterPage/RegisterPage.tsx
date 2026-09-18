import { useState } from "react"
import AuthShell from "@/components/auth/AuthShell"
import PasswordInput from "@/components/auth/PasswordInput"

const fields = [
  ["Họ và tên", "Nhập họ và tên"],
  ["Số điện thoại", "Nhập số điện thoại"],
  ["Email", "Nhập địa chỉ email"],
]

export default function RegisterPage() {
  const [password, setPassword] = useState("")
  const [confirm, setConfirm] = useState("")
  const [submitted, setSubmitted] = useState(false)
  const invalid = submitted && password !== confirm

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
          }}
          className="mt-7 space-y-3.5"
        >
          {fields.map(([label, placeholder]) => (
            <label
              key={label}
              className="block text-[12px] font-semibold text-slate-700"
            >
              {label}
              <input
                placeholder={placeholder}
                className="mt-2 h-11 w-full rounded-xl border border-slate-200 bg-white px-3.5 text-[13px] outline-none transition placeholder:text-slate-400 focus:border-[#087775] focus:ring-2 focus:ring-teal-50"
              />
            </label>
          ))}
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
