import { useCallback, useEffect, useState } from "react"
import { useNavigate } from "react-router"
import { useAuth } from "@/auth/AuthContext"
import { ApiError } from "@/api/apiClient"
import ManagementDataTable from "@/components/management/ManagementDataTable"
import ManagementPage from "@/components/management/ManagementPage"
import PropertyDetailDrawer from "@/components/management/PropertyDetailDrawer"
import PropertyFormModal from "@/components/management/PropertyFormModal"
import PropertyThumbnail from "@/components/management/PropertyThumbnail"
import {
  getMyManagedProperties,
  getManagersForProperties,
} from "@/services/propertyManagerAssignmentService"
import {
  createProperty,
  deleteProperty,
  getProperties as getManagementProperties,
  getProperty,
  updateProperty,
  type PropertyWriteRequest,
} from "@/services/propertyService"
import { managementPropertyRoomsPath } from "@/constants/routes"
import type { PropertyManagerAssignmentDto } from "@/types/propertyManagerAssignment"
import type { PropertyDto } from "@/types/user"
import { getPropertyManagerAssignmentError } from "@/utils/propertyManagerAssignmentErrors"
import { formatAddress } from "@/utils/publicFormatting"

export default function ManagementPropertiesPage() {
  const { accessToken, user } = useAuth()
  const navigate = useNavigate()
  const [properties, setProperties] = useState<PropertyDto[]>([])
  const [assignmentsByProperty, setAssignmentsByProperty] =
    useState<Record<string, PropertyManagerAssignmentDto[]>>({})
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState("")
  const [success, setSuccess] = useState("")
  const [reload, setReload] = useState(0)
  const [propertyDialog, setPropertyDialog] =
    useState<"create" | "edit" | null>(null)
  const [editingProperty, setEditingProperty] = useState<PropertyDto | null>(
    null,
  )
  const [detailProperty, setDetailProperty] = useState<PropertyDto | null>(null)
  const [savingProperty, setSavingProperty] = useState(false)
  const [deletingPropertyId, setDeletingPropertyId] = useState<number | null>(
    null,
  )

  const loadData = useCallback(
    async (signal: AbortSignal) => {
      if (!accessToken) return
      setLoading(true)
      setError("")
      try {
        const propertyRows =
          user?.role === "ADMIN"
            ? await getManagementProperties(accessToken, signal)
            : await getMyManagedProperties(accessToken, signal)
        if (signal.aborted) return
        setProperties(propertyRows)

        if (user?.role !== "ADMIN") {
          setAssignmentsByProperty({})
          return
        }

        const assignments = await getManagersForProperties(
          accessToken,
          propertyRows.map((property) => property.propertyId),
          signal,
        )
        if (signal.aborted) return
        setAssignmentsByProperty(assignments)
      } catch (cause) {
        if (!signal.aborted) setError(getPropertyManagerAssignmentError(cause))
      } finally {
        if (!signal.aborted) setLoading(false)
      }
    },
    [accessToken, user?.role],
  )

  useEffect(() => {
    const controller = new AbortController()
    void loadData(controller.signal)
    return () => controller.abort()
  }, [loadData, reload])

  function getCrudError(cause: unknown): string {
    if (!(cause instanceof ApiError)) {
      return cause instanceof Error
        ? cause.message
        : "Không thể cập nhật dữ liệu khu trọ."
    }
    if (cause.status === 409) {
      return (
        cause.message ||
        "Thao tác bị từ chối vì khu trọ còn dữ liệu nghiệp vụ liên quan."
      )
    }
    if (cause.status === 403) {
      return cause.message || "Bạn không có quyền thực hiện thao tác này."
    }
    if (cause.status === 400) {
      const messages = Object.values(cause.validationErrors ?? {}).flat()
      return messages.length > 0 ? messages.join(" ") : cause.message
    }
    return cause.message
  }

  async function submitProperty(
    request: PropertyWriteRequest,
  ): Promise<boolean> {
    if (!accessToken) return false
    setSavingProperty(true)
    setError("")
    setSuccess("")
    try {
      if (propertyDialog === "edit" && editingProperty) {
        await updateProperty(accessToken, editingProperty.propertyId, request)
        setSuccess(`Đã cập nhật ${request.propertyName}.`)
      } else {
        await createProperty(accessToken, request)
        setSuccess(`Đã tạo ${request.propertyName}.`)
      }
      setPropertyDialog(null)
      setEditingProperty(null)
      setReload((value) => value + 1)
      return true
    } catch (cause) {
      setError(getCrudError(cause))
      return false
    } finally {
      setSavingProperty(false)
    }
  }

  async function openPropertyDetail(property: PropertyDto) {
    if (!accessToken) return
    setError("")
    try {
      setDetailProperty(await getProperty(accessToken, property.propertyId))
    } catch (cause) {
      setError(getCrudError(cause))
    }
  }

  async function openPropertyEdit(property: PropertyDto) {
    if (!accessToken) return
    setError("")
    try {
      setEditingProperty(await getProperty(accessToken, property.propertyId))
      setPropertyDialog("edit")
    } catch (cause) {
      setError(getCrudError(cause))
    }
  }

  async function removeProperty(property: PropertyDto) {
    if (!accessToken) return
    const confirmed = window.confirm(
      `Bạn có chắc chắn muốn xóa khu trọ ${property.propertyName}? Chỉ khu trọ không còn dữ liệu liên quan mới có thể xóa.`,
    )
    if (!confirmed) return

    setDeletingPropertyId(property.propertyId)
    setError("")
    setSuccess("")
    try {
      await deleteProperty(accessToken, property.propertyId)
      setSuccess(`Đã xóa ${property.propertyName}.`)
      setReload((value) => value + 1)
    } catch (cause) {
      setError(getCrudError(cause))
    } finally {
      setDeletingPropertyId(null)
    }
  }

  const isAdmin = user?.role === "ADMIN"

  return (
    <ManagementPage
      title="Khu trọ"
      description={
        isAdmin
          ? "Theo dõi khu trọ; phân công Manager thực hiện tại trang Phân công Manager."
          : "Các khu trọ được Admin giao cho bạn quản lý."
      }
    >
      {isAdmin && (
        <div className="mb-5 flex justify-end">
          <button
            type="button"
            onClick={() => {
              setEditingProperty(null)
              setPropertyDialog("create")
            }}
            className="rounded-lg bg-[#087775] px-4 py-2.5 text-[13px] font-bold text-white"
          >
            + Thêm khu trọ
          </button>
        </div>
      )}
      {error && (
        <div
          role="alert"
          className="mb-4 rounded-lg bg-rose-50 px-4 py-3 text-sm text-rose-700"
        >
          <p>{error}</p>
          <button
            type="button"
            onClick={() => setReload((value) => value + 1)}
            className="mt-2 font-bold underline"
          >
            Tải lại
          </button>
        </div>
      )}
      {success && (
        <p
          role="status"
          className="mb-4 rounded-lg bg-emerald-50 px-4 py-3 text-sm text-emerald-800"
        >
          {success}
        </p>
      )}

      <ManagementDataTable
        headers={[
          "Khu trọ / Chủ sở hữu",
          "Địa chỉ",
          "Quy mô",
          "Manager được phân công",
          "Phòng",
          "Thao tác",
        ]}
      >
        {loading ? (
          <p role="status" className="px-5 py-8 text-sm text-slate-500">
            Đang tải khu trọ...
          </p>
        ) : properties.length === 0 ? (
          <div
            role="status"
            className="px-5 py-8 text-center text-sm text-slate-500"
          >
            <p>
              {isAdmin
                ? "Chưa có khu trọ nào. Hãy tạo khu trọ đầu tiên."
                : "Bạn chưa được phân công quản lý khu trọ. Vui lòng liên hệ Admin."}
            </p>
            {isAdmin && (
              <button
                type="button"
                onClick={() => {
                  setEditingProperty(null)
                  setPropertyDialog("create")
                }}
                className="mt-4 rounded-lg bg-[#087775] px-4 py-2.5 text-[13px] font-bold text-white"
              >
                + Thêm khu trọ
              </button>
            )}
          </div>
        ) : (
          <div className="divide-y divide-slate-100">
            {properties.map((property) => {
              const assignmentRows =
                assignmentsByProperty[String(property.propertyId)] ?? []

              return (
                <article
                  key={property.propertyId}
                  className="grid gap-4 px-5 py-5 lg:grid-cols-6 lg:items-center"
                >
                  <div className="flex min-w-0 items-center gap-3">
                    <PropertyThumbnail name={property.propertyName} imageUrl={property.primaryImageUrl} />
                    <div className="min-w-0">
                      <h2 className="break-words font-semibold text-slate-800">
                        {property.propertyName}
                      </h2>
                      <p className="mt-1 break-words text-xs text-slate-500">
                        Chủ sở hữu: {property.ownerName}
                      </p>
                    </div>
                  </div>
                  <p className="min-w-0 text-sm text-slate-600">
                    {formatAddress(property.addressText, property.ward, property.district, property.city)}
                  </p>
                  <p className="text-sm text-slate-600">
                    {property.roomCount} phòng
                  </p>
                  <div className="min-w-0 space-y-3">
                    {assignmentRows.length > 0 ? (
                      <ul className="space-y-2">
                        {assignmentRows.map((assignment) => (
                          <li
                            key={`${assignment.propertyId}-${assignment.managerId}`}
                            className="flex flex-wrap items-center gap-x-2 gap-y-1 rounded-lg bg-teal-50 px-3 py-2 text-sm"
                          >
                            <span className="min-w-0 break-words">
                              <strong className="text-slate-800">
                                {assignment.managerName}
                              </strong>
                              {!assignment.managerIsActive && (
                                <small className="ml-2 font-semibold text-rose-700">
                                  Đã khóa
                                </small>
                              )}
                            </span>
                          </li>
                        ))}
                      </ul>
                    ) : (
                      <p className="text-sm text-slate-400">
                        {isAdmin
                          ? "Chưa có Manager được phân công"
                          : `Bạn (${user?.name ?? "Manager"}) được phân công quản lý khu trọ này`}
                      </p>
                    )}

                  </div>
                  <button
                    type="button"
                    onClick={() =>
                      navigate(managementPropertyRoomsPath(property.propertyId))
                    }
                    aria-label={`Xem ${property.roomCount} phòng của ${property.propertyName}`}
                    className="inline-flex w-fit rounded-full bg-emerald-50 px-2.5 py-1 text-[11px] font-bold tracking-[.04em] text-emerald-700 transition hover:bg-emerald-100 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:ring-offset-2"
                  >
                    {`${property.roomCount} PHÒNG`}
                  </button>
                  <div className="flex flex-wrap gap-2">
                    <button
                      type="button"
                      onClick={() => void openPropertyDetail(property)}
                      className="rounded-lg bg-slate-100 px-3 py-2 text-[12px] font-bold text-slate-600"
                    >
                      Xem
                    </button>
                    {isAdmin && (
                      <>
                        <button
                          type="button"
                          onClick={() => void openPropertyEdit(property)}
                          className="rounded-lg bg-teal-50 px-3 py-2 text-[12px] font-bold text-[#087775]"
                        >
                          Sửa
                        </button>
                        <button
                          type="button"
                          onClick={() => void removeProperty(property)}
                          disabled={deletingPropertyId === property.propertyId}
                          className="rounded-lg bg-rose-50 px-3 py-2 text-[12px] font-bold text-rose-700 disabled:opacity-50"
                        >
                          {deletingPropertyId === property.propertyId
                            ? "Đang xóa..."
                            : "Xóa"}
                        </button>
                      </>
                    )}
                  </div>
                </article>
              )
            })}
          </div>
        )}
      </ManagementDataTable>
      {detailProperty && (
        <PropertyDetailDrawer
          property={detailProperty}
          onClose={() => setDetailProperty(null)}
        />
      )}
      {propertyDialog && (
        <PropertyFormModal
          mode={propertyDialog}
          initialProperty={editingProperty}
          saving={savingProperty}
          onClose={() => {
            if (!savingProperty) {
              setPropertyDialog(null)
              setEditingProperty(null)
            }
          }}
          onSubmit={submitProperty}
        />
      )}
    </ManagementPage>
  )
}
