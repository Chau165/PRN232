import { useMemo, useState } from "react"
import type { BackendUserDto } from "@/types/auth"
import type { PropertyDto } from "@/types/user"
import {
  assignPropertyManager,
  getPropertiesForManager,
  revokePropertyManager,
} from "@/services/propertyManagerAssignmentService"
import { getPropertyManagerAssignmentError } from "@/utils/propertyManagerAssignmentErrors"

interface AssignManagerPropertiesModalProps {
  accessToken: string
  adminId: number
  manager: BackendUserDto
  managers: BackendUserDto[]
  properties: PropertyDto[]
  assignedProperties: PropertyDto[]
  onClose: () => void
  onAssignmentsRefreshed: (properties: PropertyDto[]) => void
  onSaved: (properties: PropertyDto[], changeCount: number) => void
}

interface AssignmentChange {
  property: PropertyDto
  shouldAssign: boolean
}

export default function AssignManagerPropertiesModal({
  accessToken,
  adminId,
  manager,
  managers,
  properties,
  assignedProperties,
  onClose,
  onAssignmentsRefreshed,
  onSaved,
}: AssignManagerPropertiesModalProps) {
  const [selectedPropertyIds, setSelectedPropertyIds] = useState<number[]>(() =>
    assignedProperties.map((property) => property.propertyId),
  )
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState("")
  const assignedPropertyIds = useMemo(
    () => new Set(assignedProperties.map((property) => property.propertyId)),
    [assignedProperties],
  )
  const selectedPropertyIdSet = useMemo(
    () => new Set(selectedPropertyIds),
    [selectedPropertyIds],
  )
  const managerByUserId = useMemo(
    () => new Map(managers.map((managerRow) => [Number(managerRow.userId), managerRow])),
    [managers],
  )
  const visibleProperties = properties
    .filter((property) =>
      property.ownerId === adminId ||
      assignedPropertyIds.has(property.propertyId) ||
      managerByUserId.has(property.ownerId),
    )
    .sort((left, right) => left.propertyName.localeCompare(right.propertyName))
  const changes: AssignmentChange[] = visibleProperties
    .filter((property) => property.ownerId === adminId)
    .filter((property) =>
      selectedPropertyIdSet.has(property.propertyId) !==
      assignedPropertyIds.has(property.propertyId),
    )
    .map((property) => ({
      property,
      shouldAssign: selectedPropertyIdSet.has(property.propertyId),
    }))

  function toggleProperty(propertyId: number, checked: boolean) {
    setError("")
    setSelectedPropertyIds((current) => {
      if (checked) {
        return current.includes(propertyId) ? current : [...current, propertyId]
      }
      return current.filter((selectedId) => selectedId !== propertyId)
    })
  }

  async function saveAssignments() {
    if (changes.length === 0) return

    setSaving(true)
    setError("")
    const results = await Promise.allSettled(
      changes.map((change) => {
        if (change.shouldAssign) {
          return assignPropertyManager(
            accessToken,
            change.property.propertyId,
            Number(manager.userId),
          )
        }
        return revokePropertyManager(
          accessToken,
          change.property.propertyId,
          Number(manager.userId),
        )
      }),
    )

    let refreshedProperties: PropertyDto[]
    try {
      refreshedProperties = await getPropertiesForManager(accessToken, Number(manager.userId))
    } catch (cause) {
      const succeededCount = results.filter((result) => result.status === "fulfilled").length
      const failureDetails = results.flatMap((result, index) =>
        result.status === "rejected"
          ? [
              `${changes[index].property.propertyName}: ${getPropertyManagerAssignmentError(result.reason)}`,
            ]
          : [],
      )
      const failures = failureDetails.length > 0 ? ` ${failureDetails.join(" ")}` : ""
      setError(
        `Đã gửi ${changes.length} thay đổi, có ${succeededCount} thao tác thành công nhưng không thể tải lại dữ liệu để xác nhận.${failures} ${getPropertyManagerAssignmentError(cause)}`,
      )
      setSaving(false)
      return
    }

    onAssignmentsRefreshed(refreshedProperties)
    setSelectedPropertyIds(refreshedProperties.map((property) => property.propertyId))
    const failedChanges = changes.flatMap((change, index) => {
      const result = results[index]
      if (result.status !== "rejected") {
        return []
      }
      const operation = change.shouldAssign ? "phân công" : "thu hồi"
      return [
        `Không thể ${operation} “${change.property.propertyName}”: ${getPropertyManagerAssignmentError(result.reason)}`,
      ]
    })

    if (failedChanges.length > 0) {
      const succeededCount = changes.length - failedChanges.length
      setError(
        `Đã lưu ${succeededCount}/${changes.length} thay đổi. ${failedChanges.join(" ")}`,
      )
      setSaving(false)
      return
    }

    onSaved(refreshedProperties, changes.length)
    setSaving(false)
  }

  return (
    <div className="fixed inset-0 z-[80] grid place-items-center bg-slate-950/45 p-4">
      <section
        role="dialog"
        aria-modal="true"
        aria-labelledby="assign-manager-properties-title"
        className="max-h-[90vh] w-full max-w-2xl overflow-y-auto rounded-2xl bg-white p-6 shadow-2xl sm:p-7"
      >
        <div className="flex items-start justify-between gap-4">
          <div>
            <p className="text-[11px] font-bold uppercase tracking-[.16em] text-[#087775]">
              Phân quyền theo khu trọ
            </p>
            <h2 id="assign-manager-properties-title" className="mt-2 text-xl font-bold text-slate-900">
              Phân công khu trọ
            </h2>
            <p className="mt-2 text-sm font-semibold text-slate-700">{manager.fullName}</p>
            <p className="text-sm text-slate-500">{manager.email}</p>
          </div>
          <button
            type="button"
            onClick={onClose}
            disabled={saving}
            aria-label="Đóng"
            className="text-xl text-slate-400 disabled:opacity-50"
          >
            ×
          </button>
        </div>

        {error && (
          <p role="alert" className="mt-4 rounded-lg bg-rose-50 px-3 py-2 text-sm text-rose-700">
            {error}
          </p>
        )}

        {visibleProperties.length === 0 ? (
          <p role="status" className="mt-5 rounded-lg bg-slate-50 px-4 py-4 text-sm text-slate-600">
            Admin hiện chưa có khu trọ đủ điều kiện để phân công. Hãy tạo khu trọ hoặc xác nhận
            OwnerId với Admin sở hữu hợp lệ; chức năng này không tự thay đổi OwnerId.
          </p>
        ) : (
          <fieldset className="mt-5 space-y-3" disabled={saving}>
            <legend className="mb-3 text-sm font-bold text-slate-700">
              Chọn các khu trọ Manager được quản lý
            </legend>
            {visibleProperties.map((property) => {
              const isAssigned = assignedPropertyIds.has(property.propertyId)
              const isOwnedByAdmin = property.ownerId === adminId
              const legacyManager = managerByUserId.get(property.ownerId)
              const checkboxDisabled = !isOwnedByAdmin || (!manager.isActive && !isAssigned)

              return (
                <label
                  key={property.propertyId}
                  htmlFor={`manager-${manager.userId}-property-${property.propertyId}`}
                  className="block rounded-xl border border-slate-200 px-4 py-3"
                >
                  <span className="flex items-start gap-3">
                    <input
                      id={`manager-${manager.userId}-property-${property.propertyId}`}
                      type="checkbox"
                      checked={selectedPropertyIdSet.has(property.propertyId)}
                      disabled={checkboxDisabled}
                      onChange={(event) =>
                        toggleProperty(property.propertyId, event.currentTarget.checked)
                      }
                      className="mt-1 h-4 w-4 accent-[#087775]"
                    />
                    <span className="min-w-0 flex-1">
                      <span className="block font-semibold text-slate-800">{property.propertyName}</span>
                      <span className="mt-1 block text-xs text-slate-500">
                        Chủ sở hữu: {property.ownerName} · OwnerId={property.ownerId}
                      </span>
                      {legacyManager ? (
                        <span role="note" className="mt-2 block text-xs text-amber-800">
                          OwnerId={property.ownerId} đang trỏ tới tài khoản Manager “{legacyManager.fullName}”.
                          Hãy xác nhận Admin sở hữu hợp lệ và mapping OwnerId; chức năng phân công
                          không tự thay OwnerId.
                        </span>
                      ) : !isOwnedByAdmin ? (
                        <span role="note" className="mt-2 block text-xs text-slate-500">
                          Chỉ Admin sở hữu khu trọ này mới được thay đổi phân công.
                        </span>
                      ) : !manager.isActive && !isAssigned ? (
                        <span role="note" className="mt-2 block text-xs text-rose-700">
                          Manager đã khóa; không thể thêm phân công mới.
                        </span>
                      ) : null}
                    </span>
                  </span>
                </label>
              )
            })}
          </fieldset>
        )}

        <div className="mt-6 grid grid-cols-2 gap-3">
          <button
            type="button"
            onClick={onClose}
            disabled={saving}
            className="rounded-lg bg-slate-100 py-3 text-sm font-bold text-slate-600 disabled:opacity-50"
          >
            Hủy
          </button>
          <button
            type="button"
            onClick={() => void saveAssignments()}
            disabled={changes.length === 0 || saving}
            className="rounded-lg bg-[#087775] py-3 text-sm font-bold text-white disabled:opacity-60"
          >
            {saving ? "Đang lưu..." : "Lưu phân công"}
          </button>
        </div>
      </section>
    </div>
  )
}
