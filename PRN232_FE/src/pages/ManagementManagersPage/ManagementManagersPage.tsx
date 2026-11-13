import { useEffect, useState } from "react"
import { useAuth } from "@/auth/AuthContext"
import {
  getRoles,
  getUser,
  getProperties,
  getUsers,
  updateUserStatus,
} from "@/services/userService"
import {
  getPropertiesForManager,
  getPropertiesForManagers,
  revokePropertyManager,
} from "@/services/propertyManagerAssignmentService"
import { getPropertyManagerAssignmentError } from "@/utils/propertyManagerAssignmentErrors"
import type { BackendUserDto } from "@/types/auth"
import type { PropertyDto } from "@/types/user"
import AssignManagerPropertiesModal from "@/components/management/AssignManagerPropertiesModal"
import CreateManagerModal from "@/components/management/CreateManagerModal"
import ManagementDataTable from "@/components/management/ManagementDataTable"
import ManagementPage from "@/components/management/ManagementPage"
import StatusBadge from "@/components/customer/StatusBadge"

export default function ManagementManagersPage() {
  const { accessToken, user } = useAuth()
  const [managers, setManagers] = useState<BackendUserDto[]>([])
  const [properties, setProperties] = useState<PropertyDto[]>([])
  const [propertiesByManager, setPropertiesByManager] = useState<Record<string, PropertyDto[]>>({})
  const [managerRoleId, setManagerRoleId] = useState<number | null>(null)
  const [loading, setLoading] = useState(true)
  const [showCreate, setShowCreate] = useState(false)
  const [selectedManager, setSelectedManager] = useState<BackendUserDto | null>(null)
  const [assignmentManager, setAssignmentManager] = useState<BackendUserDto | null>(null)
  const [error, setError] = useState("")
  const [success, setSuccess] = useState("")

  useEffect(() => {
    if (!accessToken) return
    let active = true
    setLoading(true)
    setError("")
    void Promise.all([getRoles(), getProperties(accessToken)])
      .then(async ([roles, propertyRows]) => {
        const managerRole = roles.find((role) => role.roleName.trim().toLowerCase() === "manager")
        if (!managerRole) throw new Error("Backend chưa khai báo role Manager; không thể chọn roleId an toàn.")
        const rows = await getUsers(accessToken, managerRole.roleId)
        if (!active) return
        setManagerRoleId(managerRole.roleId)
        setManagers(rows)
        setProperties(propertyRows)
        const managerProperties = await getPropertiesForManagers(
          accessToken,
          rows.map((row) => Number(row.userId)),
        )
        if (active) setPropertiesByManager(managerProperties)
      })
      .catch((cause: unknown) => {
        if (active) setError(getPropertyManagerAssignmentError(cause))
      })
      .finally(() => {
        if (active) setLoading(false)
      })
    return () => { active = false }
  }, [accessToken])

  async function reloadManagerProperties(managerId: number) {
    if (!accessToken) return
    const managerProperties = await getPropertiesForManager(accessToken, managerId)
    setPropertiesByManager((current) => ({
      ...current,
      [String(managerId)]: managerProperties,
    }))
    return managerProperties
  }

  async function revokeAssignment(manager: BackendUserDto, property: PropertyDto) {
    if (!accessToken) return
    const managerId = Number(manager.userId)
    const confirmed = window.confirm(
      `Thu hồi quyền quản lý của ${manager.fullName} tại ${property.propertyName}? Lịch xem, hợp đồng đang hiệu lực hoặc bảo trì chưa hoàn tất sẽ chặn thao tác này.`,
    )
    if (!confirmed) return

    setError("")
    setSuccess("")
    try {
      await revokePropertyManager(accessToken, property.propertyId, managerId)
      await reloadManagerProperties(managerId)
      setSuccess(`Đã thu hồi quyền tại ${property.propertyName}.`)
    } catch (cause) {
      setError(getPropertyManagerAssignmentError(cause))
    }
  }

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
      {success && <p role="status" className="mb-4 rounded-lg bg-emerald-50 px-4 py-3 text-sm text-emerald-800">{success}</p>}
      <div className="mb-5 flex justify-end">
        <button disabled={managerRoleId === null} onClick={() => setShowCreate(true)} className="rounded-lg bg-[#087775] px-4 py-2.5 text-[13px] font-bold text-white disabled:opacity-50">+ Thêm Manager</button>
      </div>
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
      <ManagementDataTable headers={["Manager", "Liên hệ", "Khu trọ được phân công", "Trạng thái", "Thao tác"]}>
        {loading ? (
          <p role="status" className="px-5 py-8 text-sm text-slate-500">Đang tải Manager...</p>
        ) : managers.length === 0 ? (
          <p className="px-5 py-8 text-sm text-slate-500">Chưa có tài khoản Manager.</p>
        ) : (
          <div className="divide-y divide-slate-100">
            {managers.map((manager) => {
              const managerProperties = propertiesByManager[String(manager.userId)] ?? []
              return (
                <div key={manager.userId} className="grid gap-3 px-5 py-5 lg:grid-cols-[1.2fr_1.5fr_2fr_1fr_1fr] lg:items-center">
                  <div><p className="font-semibold text-slate-800">{manager.fullName}</p><p className="mt-1 text-[12px] text-slate-400">#{manager.userId}</p></div>
                  <div className="break-all text-[13px] text-slate-600"><p>{manager.email}</p><p className="mt-1 text-slate-400">{manager.phoneNumber}</p></div>
                  <div className="flex flex-wrap gap-1.5">
                    {managerProperties.length > 0 ? (
                      managerProperties.map((property) => (
                        <span key={property.propertyId} className="inline-flex items-center gap-2 rounded-full bg-teal-50 px-2.5 py-1 text-[11px] font-semibold text-[#087775]">
                          {property.propertyName}
                          {property.ownerId === Number(user?.id) ? (
                            <button
                              type="button"
                              aria-label={`Thu hồi ${manager.fullName} tại ${property.propertyName}`}
                              onClick={() => void revokeAssignment(manager, property)}
                              className="rounded-full px-1 text-rose-700 hover:bg-rose-100"
                            >
                              ×
                            </button>
                          ) : null}
                        </span>
                      ))
                    ) : (
                      <span className="text-xs text-slate-400">Chưa có phân công</span>
                    )}
                  </div>
                  <StatusBadge tone={manager.isActive ? "green" : "rose"}>{manager.isActive ? "ACTIVE" : "INACTIVE"}</StatusBadge>
                  <div className="flex flex-wrap gap-2">
                    <button onClick={() => void showDetails(manager.userId)} className="rounded-lg bg-slate-100 px-3 py-2 text-[12px] font-bold text-slate-600">Xem</button>
                    <button
                      type="button"
                      onClick={() => {
                        setError("")
                        setSuccess("")
                        setAssignmentManager(manager)
                      }}
                      className="rounded-lg border border-[#087775] px-3 py-2 text-[12px] font-bold text-[#087775]"
                    >
                      Phân công khu trọ
                    </button>
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
            setPropertiesByManager((rows) => ({ ...rows, [String(manager.userId)]: [] }))
            setShowCreate(false)
          }}
        />
      )}
      {assignmentManager && accessToken && user?.id && (
        <AssignManagerPropertiesModal
          accessToken={accessToken}
          adminId={Number(user.id)}
          manager={assignmentManager}
          managers={managers}
          properties={properties}
          assignedProperties={propertiesByManager[String(assignmentManager.userId)] ?? []}
          onClose={() => setAssignmentManager(null)}
          onAssignmentsRefreshed={(managerProperties) => {
            setPropertiesByManager((current) => ({
              ...current,
              [String(assignmentManager.userId)]: managerProperties,
            }))
          }}
          onSaved={(managerProperties, changeCount) => {
            setPropertiesByManager((current) => ({
              ...current,
              [String(assignmentManager.userId)]: managerProperties,
            }))
            setSuccess(`Đã lưu ${changeCount} thay đổi phân công cho ${assignmentManager.fullName}.`)
            setAssignmentManager(null)
          }}
        />
      )}
    </ManagementPage>
  )
}
