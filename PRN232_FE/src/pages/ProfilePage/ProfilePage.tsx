import { useEffect, useState, type FormEvent } from "react"
import { useAuth } from "@/auth/AuthContext"
import CustomerPageShell from "@/components/customer/CustomerPageShell"

function initials(name: string) {
  return name
    .trim()
    .split(/\s+/)
    .map((part) => part[0] ?? "")
    .join("")
    .slice(0, 2)
    .toUpperCase()
}

export default function ProfilePage() {
  const { user, updateProfile, changePassword } = useAuth()
  const [fullName, setFullName] = useState("")
  const [phoneNumber, setPhoneNumber] = useState("")
  const [editing, setEditing] = useState(false)
  const [currentPassword, setCurrentPassword] = useState("")
  const [newPassword, setNewPassword] = useState("")
  const [confirmPassword, setConfirmPassword] = useState("")
  const [message, setMessage] = useState("")
  const [error, setError] = useState("")
  const [savingProfile, setSavingProfile] = useState(false)
  const [savingPassword, setSavingPassword] = useState(false)

  useEffect(() => {
    if (!user) return
    setFullName(user.name)
    setPhoneNumber(user.phone)
  }, [user?.id, user?.name, user?.phone])

  async function saveProfile(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!user) return
    setError("")
    setMessage("")
    setSavingProfile(true)
    try {
      await updateProfile({ fullName: fullName.trim(), phoneNumber: phoneNumber.trim() })
      setEditing(false)
      setMessage("Thông tin hồ sơ đã được cập nhật.")
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Không thể cập nhật hồ sơ.")
    } finally {
      setSavingProfile(false)
    }
  }

  async function savePassword(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError("")
    setMessage("")
    if (newPassword.length < 6) {
      setError("Mật khẩu mới phải có ít nhất 6 ký tự.")
      return
    }
    if (newPassword !== confirmPassword) {
      setError("Mật khẩu xác nhận chưa khớp.")
      return
    }
    setSavingPassword(true)
    try {
      await changePassword({ currentPassword, newPassword })
      setCurrentPassword("")
      setNewPassword("")
      setConfirmPassword("")
      setMessage("Mật khẩu đã được thay đổi.")
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Không thể thay đổi mật khẩu.")
    } finally {
      setSavingPassword(false)
    }
  }

  const roleLabel = user?.role === "ADMIN"
    ? "Quản trị viên"
    : user?.role === "MANAGER"
      ? "Quản lý"
      : "Khách thuê phòng"

  return (
    <CustomerPageShell
      title="Hồ sơ cá nhân"
      description="Cập nhật thông tin liên hệ và bảo mật tài khoản của bạn."
      centerHeader
    >
      {!user ? (
        <p role="status" className="mx-auto max-w-xl rounded-xl bg-slate-50 p-5 text-center text-sm text-slate-600">
          Vui lòng đăng nhập để xem hồ sơ cá nhân.
        </p>
      ) : (
        <div className="mx-auto max-w-5xl space-y-6">
          <section aria-label="Tài khoản" className="flex flex-col gap-5 rounded-2xl border border-slate-100 bg-white p-6 shadow-sm sm:flex-row sm:items-center sm:p-8">
            <div className="flex items-center gap-5">
              <span className="grid h-16 w-16 shrink-0 place-items-center rounded-full bg-[#dff0ed] text-xl font-bold text-[#087775] sm:h-20 sm:w-20 sm:text-2xl">
                {initials(user.name)}
              </span>
              <div>
                <h2 className="font-display text-2xl font-bold text-slate-900">{user.name}</h2>
                <p className="mt-1 text-sm text-slate-500">{roleLabel}</p>
              </div>
            </div>
          </section>

          {error && <p role="alert" className="rounded-lg bg-rose-50 px-4 py-3 text-sm text-rose-700">{error}</p>}
          {message && <p role="status" className="rounded-lg bg-emerald-50 px-4 py-3 text-sm text-emerald-700">{message}</p>}

          <div className="grid items-start gap-6 lg:grid-cols-2">
            <section className="rounded-2xl border border-slate-100 bg-white p-6 shadow-sm sm:p-8">
              <div className="mb-6">
                <h2 className="font-display text-xl font-bold text-slate-900">Thông tin cá nhân</h2>
                <p className="mt-1 text-sm leading-6 text-slate-500">Thông tin liên hệ gắn với tài khoản của bạn.</p>
              </div>
              <form onSubmit={saveProfile}>
                <div className="space-y-4">
                  <label className="block text-[13px] font-semibold text-slate-700">
                    Họ và tên
                    <input
                      required
                      value={fullName}
                      readOnly={!editing}
                      onChange={(event) => setFullName(event.target.value)}
                      className={`mt-2 h-12 w-full rounded-xl border px-3.5 text-[14px] outline-none ${editing ? "border-slate-200 bg-white focus:border-[#087775]" : "border-transparent bg-slate-50 text-slate-600"}`}
                    />
                  </label>
                  <label className="block text-[13px] font-semibold text-slate-700">
                    Email
                    <input
                      type="email"
                      value={user.email}
                      readOnly
                      className="mt-2 h-12 w-full rounded-xl border border-transparent bg-slate-50 px-3.5 text-[14px] text-slate-600 outline-none"
                    />
                  </label>
                  <label className="block text-[13px] font-semibold text-slate-700">
                    Số điện thoại
                    <input
                      type="tel"
                      value={phoneNumber}
                      readOnly={!editing}
                      onChange={(event) => setPhoneNumber(event.target.value)}
                      className={`mt-2 h-12 w-full rounded-xl border px-3.5 text-[14px] outline-none ${editing ? "border-slate-200 bg-white focus:border-[#087775]" : "border-transparent bg-slate-50 text-slate-600"}`}
                    />
                  </label>
                </div>
                <div className="mt-7 flex flex-wrap justify-end gap-3">
                  {editing ? (
                    <>
                      <button type="button" onClick={() => { setEditing(false); setFullName(user.name); setPhoneNumber(user.phone) }} className="rounded-lg bg-slate-100 px-5 py-3 text-[13px] font-bold text-slate-600">Hủy</button>
                      <button type="submit" disabled={savingProfile} className="rounded-lg bg-[#087775] px-5 py-3 text-[13px] font-bold text-white disabled:opacity-60">{savingProfile ? "Đang lưu..." : "Lưu thay đổi"}</button>
                    </>
                  ) : (
                    <button type="button" onClick={() => setEditing(true)} className="rounded-lg bg-teal-50 px-5 py-3 text-[13px] font-bold text-[#087775]">Chỉnh sửa hồ sơ</button>
                  )}
                </div>
              </form>
            </section>

            <section className="rounded-2xl border border-slate-100 bg-white p-6 shadow-sm sm:p-8">
              <div className="mb-6">
                <h2 className="font-display text-xl font-bold text-slate-900">Đổi mật khẩu</h2>
                <p className="mt-1 text-sm leading-6 text-slate-500">Dùng mật khẩu hiện tại để thiết lập mật khẩu mới.</p>
              </div>
              <form onSubmit={savePassword} className="space-y-4">
                {[
                  ["Mật khẩu hiện tại", currentPassword, setCurrentPassword],
                  ["Mật khẩu mới", newPassword, setNewPassword],
                  ["Xác nhận mật khẩu mới", confirmPassword, setConfirmPassword],
                ].map(([label, value, setter]) => (
                  <label key={label as string} className="block text-[13px] font-semibold text-slate-700">
                    {label as string}
                    <input
                      required
                      type="password"
                      autoComplete={label === "Mật khẩu hiện tại" ? "current-password" : "new-password"}
                      value={value as string}
                      onChange={(event) => (setter as (value: string) => void)(event.target.value)}
                      className="mt-2 h-12 w-full rounded-xl border border-slate-200 px-3.5 text-[14px] outline-none focus:border-[#087775]"
                    />
                  </label>
                ))}
                <button type="submit" disabled={savingPassword} className="rounded-lg bg-[#087775] px-5 py-3 text-[13px] font-bold text-white disabled:opacity-60">
                  {savingPassword ? "Đang cập nhật..." : "Cập nhật mật khẩu"}
                </button>
              </form>
            </section>
          </div>
        </div>
      )}
    </CustomerPageShell>
  )
}
