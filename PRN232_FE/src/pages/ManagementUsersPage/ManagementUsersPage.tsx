import { useEffect, useState, type FormEvent } from "react"
import { useAuth } from "@/auth/AuthContext"
import type { BackendUserDto } from "@/types/auth"
import {
  createUser,
  getRoles,
  getUser,
  getUsers,
  updateUserRole,
  updateUserStatus,
  type RoleDto,
} from "@/api/usersApi"
import ManagementDataTable from "@/components/management/ManagementDataTable"
import ManagementPage from "@/components/management/ManagementPage"
import StatusBadge from "@/components/customer/StatusBadge"

const emptyForm = { fullName: "", email: "", phoneNumber: "", password: "", roleId: "" }

function roleTone(roleName: string) {
  const role = roleName.toUpperCase()
  return role === "ADMIN" ? "rose" : role === "MANAGER" ? "teal" : "slate"
}

export default function ManagementUsersPage() {
  const { user: currentUser, accessToken } = useAuth()
  const [users, setUsers] = useState<BackendUserDto[]>([])
  const [roles, setRoles] = useState<RoleDto[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState("")
  const [form, setForm] = useState(emptyForm)
  const [creating, setCreating] = useState(false)
  const [selectedUser, setSelectedUser] = useState<BackendUserDto | null>(null)

  useEffect(() => {
    if (!accessToken) return
    let active = true
    setLoading(true)
    setError("")
    void Promise.all([getUsers(accessToken), getRoles()])
      .then(([userRows, roleRows]) => {
        if (!active) return
        setUsers(userRows)
        setRoles(roleRows)
        setForm((previous) => ({ ...previous, roleId: previous.roleId || String(roleRows[0]?.roleId ?? "") }))
      })
      .catch((cause: unknown) => {
        if (active) setError(cause instanceof Error ? cause.message : "Không thể tải danh sách người dùng.")
      })
      .finally(() => {
        if (active) setLoading(false)
      })
    return () => { active = false }
  }, [accessToken])

  async function submitCreate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!accessToken) return
    setError("")
    setCreating(true)
    try {
      const created = await createUser(accessToken, {
        fullName: form.fullName.trim(),
        email: form.email.trim(),
        phoneNumber: form.phoneNumber.trim(),
        password: form.password,
        roleId: Number(form.roleId),
      })
      setUsers((rows) => [created, ...rows])
      setForm({ ...emptyForm, roleId: String(roles[0]?.roleId ?? "") })
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Không thể tạo tài khoản.")
    } finally {
      setCreating(false)
    }
  }

  async function changeRole(row: BackendUserDto, roleId: number) {
    if (!accessToken || String(row.userId) === currentUser?.id || roleId === row.roleId) return
    setError("")
    try {
      const updated = await updateUserRole(accessToken, row.userId, roleId)
      setUsers((rows) => rows.map((item) => item.userId === row.userId ? updated : item))
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Không thể cập nhật role.")
    }
  }

  async function toggleStatus(row: BackendUserDto) {
    if (!accessToken || String(row.userId) === currentUser?.id) return
    setError("")
    try {
      const updated = await updateUserStatus(accessToken, row.userId, !row.isActive)
      setUsers((rows) => rows.map((item) => item.userId === row.userId ? updated : item))
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Không thể cập nhật trạng thái.")
    }
  }

  async function showDetails(userId: number | string) {
    if (!accessToken) return
    setError("")
    try {
      setSelectedUser(await getUser(accessToken, userId))
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Không thể tải thông tin người dùng.")
    }
  }

  return (
    <ManagementPage title="Người dùng" description="Quản lý tài khoản và quyền truy cập theo chính sách Backend.">
      {error && <p role="alert" className="mb-4 rounded-lg bg-rose-50 px-4 py-3 text-sm text-rose-700">{error}</p>}
      <section className="mb-6 rounded-2xl border border-slate-100 bg-white p-5 shadow-[0_3px_18px_rgba(16,65,67,.05)]">
        <h2 className="font-display text-lg font-bold text-slate-900">Tạo tài khoản</h2>
        <form onSubmit={submitCreate} className="mt-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
          <input required value={form.fullName} onChange={(event) => setForm({ ...form, fullName: event.target.value })} placeholder="Họ và tên" aria-label="Họ và tên người dùng mới" className="rounded-lg border border-slate-200 px-3 py-2 text-sm" />
          <input required type="email" value={form.email} onChange={(event) => setForm({ ...form, email: event.target.value })} placeholder="Email" aria-label="Email người dùng mới" className="rounded-lg border border-slate-200 px-3 py-2 text-sm" />
          <input required type="tel" value={form.phoneNumber} onChange={(event) => setForm({ ...form, phoneNumber: event.target.value })} placeholder="Số điện thoại" aria-label="Số điện thoại người dùng mới" className="rounded-lg border border-slate-200 px-3 py-2 text-sm" />
          <input required type="password" minLength={6} value={form.password} onChange={(event) => setForm({ ...form, password: event.target.value })} placeholder="Mật khẩu tạm" aria-label="Mật khẩu người dùng mới" className="rounded-lg border border-slate-200 px-3 py-2 text-sm" />
          <div className="flex gap-2">
            <select required aria-label="Role tài khoản mới" value={form.roleId} onChange={(event) => setForm({ ...form, roleId: event.target.value })} className="min-w-0 flex-1 rounded-lg border border-slate-200 px-3 py-2 text-sm">
              {roles.map((role) => <option key={role.roleId} value={role.roleId}>{role.roleName}</option>)}
            </select>
            <button disabled={creating || roles.length === 0} className="rounded-lg bg-[#087775] px-3 py-2 text-xs font-bold text-white disabled:opacity-50">{creating ? "Đang tạo..." : "Tạo"}</button>
          </div>
        </form>
      </section>

      {selectedUser && (
        <section aria-label="Chi tiết người dùng" className="mb-5 rounded-xl border border-teal-100 bg-teal-50/50 p-4 text-sm">
          <div className="flex items-start justify-between gap-3">
            <div>
              <h2 className="font-bold text-slate-800">{selectedUser.fullName}</h2>
              <p className="mt-1 text-slate-600">{selectedUser.email} · {selectedUser.phoneNumber}</p>
              <p className="mt-1 text-slate-600">{selectedUser.roleName} · {selectedUser.isActive ? "Đang hoạt động" : "Đã khóa"}</p>
            </div>
            <button onClick={() => setSelectedUser(null)} aria-label="Đóng chi tiết" className="text-slate-500">×</button>
          </div>
        </section>
      )}

      <ManagementDataTable headers={["Người dùng", "Email", "Role", "Trạng thái", "Thao tác"]}>
        {loading ? (
          <p role="status" className="px-5 py-8 text-sm text-slate-500">Đang tải người dùng...</p>
        ) : users.length === 0 ? (
          <p className="px-5 py-8 text-sm text-slate-500">Chưa có người dùng.</p>
        ) : (
          <div className="divide-y divide-slate-100">
            {users.map((row) => {
              const isSelf = String(row.userId) === currentUser?.id
              return (
                <div key={row.userId} className="grid gap-3 px-5 py-5 lg:grid-cols-[1.3fr_1.7fr_1fr_1fr_1.2fr] lg:items-center">
                  <div><p className="font-semibold text-slate-800">{row.fullName}</p><p className="mt-1 text-[12px] text-slate-400">#{row.userId}</p></div>
                  <span className="break-all text-[13px] text-slate-600">{row.email}</span>
                  <div className="flex items-center gap-2">
                    <StatusBadge tone={roleTone(row.roleName)}>{row.roleName}</StatusBadge>
                    <select aria-label={`Đổi role ${row.fullName}`} value={row.roleId} disabled={isSelf} onChange={(event) => void changeRole(row, Number(event.target.value))} className="max-w-28 rounded-md border border-slate-200 px-2 py-1 text-xs disabled:opacity-50">
                      {roles.map((role) => <option key={role.roleId} value={role.roleId}>{role.roleName}</option>)}
                    </select>
                  </div>
                  <StatusBadge tone={row.isActive ? "green" : "rose"}>{row.isActive ? "ACTIVE" : "INACTIVE"}</StatusBadge>
                  <div className="flex flex-wrap gap-2">
                    <button onClick={() => void showDetails(row.userId)} className="rounded-lg bg-slate-100 px-3 py-2 text-[12px] font-bold text-slate-600">Xem</button>
                    <button disabled={isSelf} onClick={() => void toggleStatus(row)} className="rounded-lg bg-rose-50 px-3 py-2 text-[12px] font-bold text-rose-700 disabled:opacity-50">{row.isActive ? "Khóa" : "Mở khóa"}</button>
                  </div>
                </div>
              )
            })}
          </div>
        )}
      </ManagementDataTable>
    </ManagementPage>
  )
}
