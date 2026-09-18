import { useState } from "react"
import CustomerPageShell from "@/components/customer/CustomerPageShell"
import Toast from "@/components/customer/Toast"
import { getCurrentUser } from "@/utils/managementAuth"

export default function ProfilePage() {
  const user = getCurrentUser()
  const [profile, setProfile] = useState({
    fullName: user.name,
    email: user.email,
    phone: user.phone,
  })
  const [editing, setEditing] = useState(false)
  const [toast, setToast] = useState("")

  return (
    <CustomerPageShell
      title="Hồ sơ cá nhân"
      description="Cập nhật thông tin liên hệ để TrọViệt hỗ trợ bạn tốt hơn."
    >
      <section className="max-w-2xl rounded-2xl bg-white p-7 shadow-[0_5px_24px_rgba(16,65,67,.08)]">
        <div className="flex flex-col items-start gap-5 border-b border-slate-100 pb-6 sm:flex-row sm:items-center">
          <span className="grid h-20 w-20 place-items-center rounded-full bg-[#dff0ed] text-2xl font-bold text-[#087775]">
            NA
          </span>
          <div>
            <h2 className="font-display text-[24px] font-bold text-slate-900">
              {profile.fullName}
            </h2>
            <p className="mt-1 text-[14px] text-slate-500">
              {user.role === "ADMIN"
                ? "Chủ trọ"
                : user.role === "MANAGER"
                  ? "Quản lý"
                  : "Khách thuê phòng"}
            </p>
          </div>
        </div>
        <div className="mt-6 space-y-4">
          {[
            ["Họ và tên", "fullName"],
            ["Email", "email"],
            ["Số điện thoại", "phone"],
          ].map(([label, key]) => (
            <label
              key={key}
              className="block text-[13px] font-semibold text-slate-700"
            >
              {label}
              <input
                value={profile[(key as keyof typeof profile)]}
                readOnly={!editing}
                onChange={(event) =>
                  setProfile({ ...profile, [key]: event.target.value })
                }
                className={`mt-2 h-12 w-full rounded-xl border px-3.5 text-[14px] outline-none ${
                  editing
                    ? "border-slate-200 bg-white focus:border-[#087775]"
                    : "border-transparent bg-slate-50 text-slate-600"
                }`}
              />
            </label>
          ))}
        </div>
        <div className="mt-7 flex gap-3">
          {editing ? (
            <button
              onClick={() => {
                setEditing(false)
                setToast("Thông tin hồ sơ đã được cập nhật")
              }}
              className="rounded-lg bg-[#087775] px-5 py-3 text-[13px] font-bold text-white"
            >
              Lưu thay đổi
            </button>
          ) : (
            <button
              onClick={() => setEditing(true)}
              className="rounded-lg bg-teal-50 px-5 py-3 text-[13px] font-bold text-[#087775]"
            >
              Chỉnh sửa hồ sơ
            </button>
          )}
        </div>
      </section>
      {toast && <Toast message={toast} onClose={() => setToast("")} />}
    </CustomerPageShell>
  )
}
