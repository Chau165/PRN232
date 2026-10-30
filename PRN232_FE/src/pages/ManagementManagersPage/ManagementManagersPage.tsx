import { useEffect, useState } from "react"
import { useAuth } from "@/auth/AuthContext"
import {
  getPropertiesOwnedBy,
  getRoles,
  getUser,
  getUsers,
  updateUserStatus,
} from "@/services/userService"
import type { BackendUserDto } from "@/types/auth"
import type { PropertyDto } from "@/types/user"
import CreateManagerModal from "@/components/management/CreateManagerModal"
import ManagementDataTable from "@/components/management/ManagementDataTable"
import ManagementPage from "@/components/management/ManagementPage"
import StatusBadge from "@/components/customer/StatusBadge"

export default function ManagementManagersPage() {
  const { accessToken } = useAuth()
  const [managers, setManagers] = useState<BackendUserDto[]>([])
  const [propertiesByOwner, setPropertiesByOwner] = useState<Record<string, PropertyDto[]>>({})
  const [managerRoleId, setManagerRoleId] = useState<number | null>(null)
  const [loading, setLoading] = useState(true)
  const [showCreate, setShowCreate] = useState(false)
  const [selectedManager, setSelectedManager] = useState<BackendUserDto | null>(null)
  const [error, setError] = useState("")

  useEffect(() => {
    if (!accessToken) return
    let active = true
    setLoading(true)
    setError("")
    void getRoles()
      .then(async (roles) => {
        const managerRole = roles.find((role) => role.roleName.trim().toLowerCase() === "manager")
        if (!managerRole) throw new Error("Backend chưa khai báo role Manager; không thể chọn roleId an toàn.")
        const rows = await getUsers(accessToken, managerRole.roleId)
        if (!active) return
        setManagerRoleId(managerRole.roleId)
        setManagers(rows)
        const propertyResults = await Promise.all(rows.map(async (row) => [
          String(row.userId),
          await getPropertiesOwnedBy(accessToken, row.userId),
        ] as const))
        if (active) setPropertiesByOwner(Object.fromEntries(propertyResults))
      })
      .catch((cause: unknown) => {
        if (active) setError(cause instanceof Error ? cause.message : "Không thể tải danh sách Manager.")
      })
      .finally(() => {
        if (active) setLoading(false)
      })
    return () => { active = false }
  }, [accessToken])

  async function showDetails(managerId: number | string) {
    if (!accessToken) return
    setError("")
    try {
      setSelectedManager(await getUser(accessToken, managerId))
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Không thể tải thông tin Manager.")
    }
  }

  async function toggleStatus(manager: BackendUserDto) {
    if (!accessToken) return
    setError("")
    try {
      const updated = await updateUserStatus(accessToken, manager.userId, !manager.isActive)
      setManagers((rows) => rows.map((row) => row.userId === manager.userId ? updated : row))
      if (selectedManager?.userId === manager.userId) setSelectedManager(updated)
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Không thể cập nhật trạng thái Manager.")
    }
  }

  return (
    <ManagementPage title="Quản lý Manager" description="Danh sách và trạng thái tài khoản Manager trên hệ thống.">
      {error && <p role="alert" className="mb-4 rounded-lg bg-rose-50 px-4 py-3 text-sm text-rose-700">{error}</p>}
      <div className="mb-5 flex justify-end">
        <button disabled={managerRoleId === null} onClick={() => setShowCreate(true)} className="rounded-lg bg-[#087775] px-4 py-2.5 text-[13px] font-bold text-white disabled:opacity-50">+ Thêm Manager</button>
      </div>
      <p className="mb-5 rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">
        Backend hiện chưa có API gán hoặc chuyển quyền quản lý khu trọ. Danh sách bên dưới chỉ hiển thị khu trọ có OwnerId trùng Manager; thao tác gán chưa thể lưu.
      </p>
      {selectedManager && (
        <section aria-label="Chi tiết Manager" className="mb-5 rounded-xl border border-teal-100 bg-teal-50/50 p-4 text-sm">
          <div className="flex items-start justify-between gap-3">
            <div>
              <h2 className="font-bold text-slate-800">{selectedManager.fullName}</h2>
              <p className="mt-1 text-slate-600">{selectedManager.email} · {selectedManager.phoneNumber}</p>
              <p className="mt-1 text-slate-600">{selectedManager.roleName} · {selectedManager.isActive ? "Đang hoạt động" : "Đã khóa"}</p>
            </div>
            <button onClick={() => setSelectedManager(null)} aria-label="Đóng chi tiết" className="text-slate-500">×</button>
          </div>
        </section>
      )}
      <ManagementDataTable headers={["Manager", "Liên hệ", "Khu trọ theo OwnerId", "Trạng thái", "Thao tác"]}>
        {loading ? (
          <p role="status" className="px-5 py-8 text-sm text-slate-500">Đang tải Manager...</p>
        ) : managers.length === 0 ? (
          <p className="px-5 py-8 text-sm text-slate-500">Chưa có tài khoản Manager.</p>
        ) : (
          <div className="divide-y divide-slate-100">
            {managers.map((manager) => {
              const ownedProperties = propertiesByOwner[String(manager.userId)] ?? []
              return (
                <div key={manager.userId} className="grid gap-3 px-5 py-5 lg:grid-cols-[1.2fr_1.5fr_2fr_1fr_1fr] lg:items-center">
                  <div><p className="font-semibold text-slate-800">{manager.fullName}</p><p className="mt-1 text-[12px] text-slate-400">#{manager.userId}</p></div>
                  <div className="break-all text-[13px] text-slate-600"><p>{manager.email}</p><p className="mt-1 text-slate-400">{manager.phoneNumber}</p></div>
                  <div className="flex flex-wrap gap-1.5">
                    {ownedProperties.length ? ownedProperties.map((property) => <span key={property.propertyId} className="rounded-full bg-teal-50 px-2.5 py-1 text-[11px] font-semibold text-[#087775]">{property.propertyName}</span>) : <span className="text-xs text-slate-400">Chưa có khu trọ đứng tên</span>}
                  </div>
                  <StatusBadge tone={manager.isActive ? "green" : "rose"}>{manager.isActive ? "ACTIVE" : "INACTIVE"}</StatusBadge>
                  <div className="flex flex-wrap gap-2">
                    <button onClick={() => void showDetails(manager.userId)} className="rounded-lg bg-slate-100 px-3 py-2 text-[12px] font-bold text-slate-600">Xem</button>
                    <button onClick={() => void toggleStatus(manager)} className="rounded-lg bg-rose-50 px-3 py-2 text-[12px] font-bold text-rose-700">{manager.isActive ? "Khóa" : "Mở khóa"}</button>
                  </div>
                </div>
              )
            })}
          </div>
        )}
      </ManagementDataTable>
      {showCreate && accessToken && managerRoleId !== null && (
        <CreateManagerModal
          accessToken={accessToken}
          roleId={managerRoleId}
          onClose={() => setShowCreate(false)}
          onCreated={(manager) => {
            setManagers((rows) => [manager, ...rows])
            setPropertiesByOwner((rows) => ({ ...rows, [String(manager.userId)]: [] }))
            setShowCreate(false)
          }}
        />
      )}
    </ManagementPage>
  )
}
