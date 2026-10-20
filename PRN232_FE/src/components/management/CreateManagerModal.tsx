import { useState, type FormEvent } from "react"
import { createUser } from "@/api/usersApi"
import type { BackendUserDto } from "@/types/auth"

export default function CreateManagerModal({
  accessToken,
  roleId,
  onClose,
  onCreated,
}: {
  accessToken: string
  roleId: number
  onClose: () => void
  onCreated: (user: BackendUserDto) => void
}) {
  const [fullName, setFullName] = useState("")
  const [email, setEmail] = useState("")
  const [phoneNumber, setPhoneNumber] = useState("")
  const [password, setPassword] = useState("")
  const [error, setError] = useState("")
  const [saving, setSaving] = useState(false)

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError("")
    setSaving(true)
    try {
      const created = await createUser(accessToken, {
        fullName: fullName.trim(),
        email: email.trim(),
        phoneNumber: phoneNumber.trim(),
        password,
        roleId,
      })
      onCreated(created)
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Không thể tạo Manager.")
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="fixed inset-0 z-[70] grid place-items-center bg-slate-950/45 p-5">
      <section role="dialog" aria-modal="true" aria-labelledby="create-manager-title" className="w-full max-w-lg rounded-2xl bg-white p-7 shadow-2xl">
        <div className="flex items-start justify-between">
          <div>
            <p className="text-[11px] font-bold uppercase tracking-[.16em] text-[#087775]">Tài khoản Manager</p>
            <h2 id="create-manager-title" className="mt-2 font-display text-2xl font-bold text-slate-900">Thêm Manager</h2>
          </div>
          <button onClick={onClose} aria-label="Đóng" className="text-xl text-slate-400">×</button>
        </div>
        <form onSubmit={submit} className="mt-5 space-y-3">
          <input required value={fullName} onChange={(event) => setFullName(event.target.value)} placeholder="Họ và tên" aria-label="Họ và tên Manager" className="w-full rounded-lg border border-slate-200 px-3 py-2.5 text-sm" />
          <input required type="email" value={email} onChange={(event) => setEmail(event.target.value)} placeholder="Email" aria-label="Email Manager" className="w-full rounded-lg border border-slate-200 px-3 py-2.5 text-sm" />
          <input required type="tel" value={phoneNumber} onChange={(event) => setPhoneNumber(event.target.value)} placeholder="Số điện thoại" aria-label="Số điện thoại Manager" className="w-full rounded-lg border border-slate-200 px-3 py-2.5 text-sm" />
          <input required type="password" minLength={6} value={password} onChange={(event) => setPassword(event.target.value)} placeholder="Mật khẩu khởi tạo" aria-label="Mật khẩu Manager" className="w-full rounded-lg border border-slate-200 px-3 py-2.5 text-sm" />
          {error && <p role="alert" className="text-sm text-rose-700">{error}</p>}
          <div className="grid grid-cols-2 gap-3 pt-2">
            <button type="button" onClick={onClose} className="rounded-lg bg-slate-100 py-3 text-sm font-bold text-slate-600">Hủy</button>
            <button type="submit" disabled={saving} className="rounded-lg bg-[#087775] py-3 text-sm font-bold text-white disabled:opacity-60">{saving ? "Đang tạo..." : "Tạo tài khoản"}</button>
          </div>
        </form>
      </section>
    </div>
  )
}
