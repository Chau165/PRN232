import { useState } from "react"
import type { PasswordInputProps } from "@/types/auth"

export default function PasswordInput({
  value,
  onChange,
  invalid,
}: PasswordInputProps) {
  const [shown, setShown] = useState(false)

  return (
    <div
      className={`flex h-12 items-center rounded-xl border bg-white px-3.5 transition ${
        invalid
          ? "border-rose-300 ring-2 ring-rose-50"
          : "border-slate-200 focus-within:border-[#087775] focus-within:ring-2 focus-within:ring-teal-50"
      }`}
    >
      <input
        value={value}
        onChange={(event) => onChange(event.target.value)}
        type={shown ? "text" : "password"}
        placeholder="Nhập mật khẩu"
        className="min-w-0 flex-1 bg-transparent text-[14px] outline-none placeholder:text-slate-400"
      />
      <button
        type="button"
        onClick={() => setShown(!shown)}
        className="text-[13px] font-semibold text-[#087775]"
      >
        {shown ? "Ẩn" : "Hiện"}
      </button>
    </div>
  )
}
