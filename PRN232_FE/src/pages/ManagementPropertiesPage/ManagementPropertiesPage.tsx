import { useCallback, useEffect, useMemo, useState } from "react"
import { useNavigate } from "react-router"
import { useAuth } from "@/auth/AuthContext"
import { ApiError } from "@/api/apiClient"
import ManagementDataTable from "@/components/management/ManagementDataTable"
import ManagementPage from "@/components/management/ManagementPage"
import {
  ManagementEmptyState,
  ManagementErrorState,
  ManagementLoadingState,
  ManagementSuccessBanner,
} from "@/components/management/ManagementState"
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
  getAdministrativeProvinces,
  getAdministrativeWards,
  getProperties as getManagementProperties,
  getProperty,
  updateProperty,
  type PropertyWriteRequest,
} from "@/services/propertyService"
import { managementPropertyRoomTypesPath, managementPropertyRoomsPath } from "@/constants/routes"
import type { PropertyManagerAssignmentDto } from "@/types/propertyManagerAssignment"
import type {
  AdministrativeProvinceOption,
  AdministrativeWardOption,
} from "@/types/administrativeDivision"
import { DEFAULT_PAGE_SIZE } from "@/types/pagination"
import type { PropertyDto } from "@/types/user"
import { getPropertyManagerAssignmentError } from "@/utils/propertyManagerAssignmentErrors"
import { formatAddress } from "@/utils/publicFormatting"

function normalizeForSearch(value: string) {
  return value.trim().toLocaleLowerCase("vi")
}

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
  const [propertySubmitError, setPropertySubmitError] = useState<unknown>(null)
  const [deletingPropertyId, setDeletingPropertyId] = useState<number | null>(
    null,
  )
  const [propertySearch, setPropertySearch] = useState("")
  const [selectedProvinceCode, setSelectedProvinceCode] = useState("")
  const [selectedWardCode, setSelectedWardCode] = useState("")
  const [provinceOptions, setProvinceOptions] = useState<
    AdministrativeProvinceOption[]
  >([])
  const [wardOptions, setWardOptions] = useState<AdministrativeWardOption[]>([])
  const [provinceLoading, setProvinceLoading] = useState(false)
  const [wardLoading, setWardLoading] = useState(false)
  const [locationError, setLocationError] = useState("")
  const [page, setPage] = useState(1)

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
        setPage(1)

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

  useEffect(() => {
    if (!accessToken) return
    const controller = new AbortController()
    setProvinceLoading(true)
    setLocationError("")
    void getAdministrativeProvinces(accessToken, controller.signal)
      .then((options) => {
        if (!controller.signal.aborted) setProvinceOptions(options)
      })
      .catch((cause: unknown) => {
        if (!controller.signal.aborted) {
          setLocationError(
            cause instanceof Error
              ? cause.message
              : "Không thể tải danh mục tỉnh/thành phố.",
          )
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setProvinceLoading(false)
      })
    return () => controller.abort()
  }, [accessToken])

  useEffect(() => {
    if (!accessToken || !selectedProvinceCode) {
      setWardOptions([])
      setWardLoading(false)
      return
    }
    const controller = new AbortController()
    setWardOptions([])
    setWardLoading(true)
    setLocationError("")
    void getAdministrativeWards(
      accessToken,
      selectedProvinceCode,
      controller.signal,
    )
      .then((options) => {
        if (!controller.signal.aborted) setWardOptions(options)
      })
      .catch((cause: unknown) => {
        if (!controller.signal.aborted) {
          setLocationError(
            cause instanceof Error
              ? cause.message
              : "Không thể tải danh sách phường/xã.",
          )
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setWardLoading(false)
      })
    return () => controller.abort()
  }, [accessToken, selectedProvinceCode])

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
    return cause.message
  }

  async function submitProperty(
    request: PropertyWriteRequest,
  ): Promise<boolean> {
    if (!accessToken) return false
    setSavingProperty(true)
    setError("")
    setSuccess("")
    setPropertySubmitError(null)
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
      setPropertySubmitError(cause)
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
      if (detailProperty?.propertyId === property.propertyId) {
        setDetailProperty(null)
      }
      setSuccess(`Đã xóa ${property.propertyName}.`)
      setReload((value) => value + 1)
    } catch (cause) {
      setError(getCrudError(cause))
    } finally {
      setDeletingPropertyId(null)
    }
  }

  const isAdmin = user?.role === "ADMIN"
  const filteredProperties = useMemo(() => {
    const query = normalizeForSearch(propertySearch)
    const selectedProvince = provinceOptions.find(
      (option) => option.provinceCode === selectedProvinceCode,
    )
    const selectedWard = wardOptions.find(
      (option) => option.wardCode === selectedWardCode,
    )

    return properties.filter((property) => {
      if (
        query &&
        !normalizeForSearch(property.propertyName).includes(query)
      ) {
        return false
      }
      const provinceMatches =
        !selectedProvinceCode ||
        (property.provinceCode
          ? property.provinceCode === selectedProvinceCode
          : selectedProvince !== undefined &&
            normalizeForSearch(property.city) ===
              normalizeForSearch(selectedProvince.provinceName))
      if (!provinceMatches) return false

      const wardMatches =
        !selectedWardCode ||
        (property.wardCode
          ? property.wardCode === selectedWardCode
          : selectedWard !== undefined &&
            normalizeForSearch(property.ward) ===
              normalizeForSearch(selectedWard.wardName))
      if (!wardMatches) return false
      return true
    })
  }, [
    properties,
    propertySearch,
    provinceOptions,
    selectedProvinceCode,
    selectedWardCode,
    wardOptions,
  ])
  const pageCount = Math.max(
    1,
    Math.ceil(filteredProperties.length / DEFAULT_PAGE_SIZE),
  )
  const currentPage = Math.min(page, pageCount)
  const pageProperties = filteredProperties.slice(
    (currentPage - 1) * DEFAULT_PAGE_SIZE,
    currentPage * DEFAULT_PAGE_SIZE,
  )

  return (
    <ManagementPage
      title="Khu trọ"
      description={
        isAdmin
          ? "Theo dõi khu trọ; phân công Manager thực hiện tại trang Phân công Manager."
          : "Các khu trọ được Admin giao cho bạn quản lý."
      }
      actions={
        isAdmin ? (
          <button
            type="button"
            onClick={() => {
              setEditingProperty(null)
              setPropertyDialog("create")
            }}
            className="rounded-lg bg-[#087775] px-4 py-2.5 text-[13px] font-bold text-white hover:bg-[#066a68]"
          >
            + Thêm khu trọ
          </button>
        ) : undefined
      }
    >
      {error && (
        <ManagementErrorState
          variant="banner"
          message={error}
          retryLabel="Tải lại"
          onRetry={() => setReload((value) => value + 1)}
        />
      )}
      {success && <ManagementSuccessBanner message={success} />}

      <div className="mb-4 grid gap-3 rounded-2xl border border-slate-100 bg-white p-4 shadow-[0_3px_18px_rgba(16,65,67,.05)] sm:grid-cols-2 lg:grid-cols-3">
        <label className="block space-y-1.5 text-sm">
          <span className="font-medium text-slate-700">Tìm theo tên khu trọ</span>
          <input
            type="search"
            value={propertySearch}
            onChange={(event) => {
              setPropertySearch(event.target.value)
              setPage(1)
            }}
            placeholder="Nhập tên khu trọ"
            className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900"
          />
        </label>
        <label className="block space-y-1.5 text-sm">
          <span className="font-medium text-slate-700">Tỉnh/thành phố</span>
          <select
            value={selectedProvinceCode}
            onChange={(event) => {
              setSelectedProvinceCode(event.target.value)
              setSelectedWardCode("")
              setLocationError("")
              setPage(1)
            }}
            disabled={provinceLoading}
            className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 disabled:opacity-60"
          >
            <option value="">{provinceLoading ? "Đang tải tỉnh/thành phố..." : "Tất cả tỉnh/thành phố"}</option>
            {provinceOptions.map((province) => (
              <option key={province.provinceCode} value={province.provinceCode}>
                {province.provinceName}
              </option>
            ))}
          </select>
        </label>
        <label className="block space-y-1.5 text-sm">
          <span className="font-medium text-slate-700">Phường/xã</span>
          <select
            value={selectedWardCode}
            onChange={(event) => {
              setSelectedWardCode(event.target.value)
              setPage(1)
            }}
            disabled={!selectedProvinceCode || wardLoading}
            className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 disabled:opacity-60"
          >
            <option value="">
              {wardLoading
                ? "Đang tải phường/xã..."
                : selectedProvinceCode
                  ? "Tất cả phường/xã"
                  : "Chọn tỉnh/thành phố trước"}
            </option>
            {wardOptions.map((ward) => (
              <option key={ward.wardCode} value={ward.wardCode}>
                {ward.wardName}
              </option>
            ))}
          </select>
        </label>
      </div>
      {locationError && <p role="alert" className="mb-4 text-sm text-rose-600">{locationError}</p>}

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
          <ManagementLoadingState message="Đang tải khu trọ..." />
        ) : properties.length === 0 ? (
          <ManagementEmptyState
            icon="building"
            message={
              isAdmin
                ? "Chưa có khu trọ nào. Hãy tạo khu trọ đầu tiên."
                : "Bạn chưa được phân công quản lý khu trọ. Vui lòng liên hệ Admin."
            }
            action={
              isAdmin ? (
                <button
                  type="button"
                  onClick={() => {
                    setEditingProperty(null)
                    setPropertyDialog("create")
                  }}
                  className="rounded-lg bg-[#087775] px-4 py-2.5 text-[13px] font-bold text-white hover:bg-[#066a68]"
                >
                  + Thêm khu trọ
                </button>
              ) : undefined
            }
          />
        ) : filteredProperties.length === 0 ? (
          <ManagementEmptyState
            icon="building"
            message="Không tìm thấy khu trọ phù hợp với điều kiện tìm kiếm hoặc bộ lọc."
          />
        ) : (
          <div className="divide-y divide-slate-100">
            {pageProperties.map((property) => {
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
                    {formatAddress(property.addressText, property.ward, property.city)}
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
                          onClick={() => navigate(managementPropertyRoomTypesPath(property.propertyId))}
                          className="rounded-lg bg-emerald-50 px-3 py-2 text-[12px] font-bold text-emerald-800"
                        >
                          Loại phòng & giá
                        </button>
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
      {!loading && filteredProperties.length > 0 && (
        <nav aria-label="Phân trang khu trọ" className="mt-4 flex flex-wrap items-center justify-between gap-3 text-sm text-slate-600">
          <span>
            Hiển thị {(currentPage - 1) * DEFAULT_PAGE_SIZE + 1}–{Math.min(currentPage * DEFAULT_PAGE_SIZE, filteredProperties.length)} trong {filteredProperties.length} khu trọ
          </span>
          <div className="flex gap-2">
            <button
              type="button"
              disabled={currentPage <= 1}
              onClick={() => setPage(Math.max(1, currentPage - 1))}
              className="rounded-lg border border-slate-200 px-3 py-2 text-xs font-bold disabled:opacity-50"
            >
              Trang trước
            </button>
            <span className="px-2 py-2 text-xs font-semibold">Trang {currentPage}/{pageCount}</span>
            <button
              type="button"
              disabled={currentPage >= pageCount}
              onClick={() => setPage(Math.min(pageCount, currentPage + 1))}
              className="rounded-lg border border-slate-200 px-3 py-2 text-xs font-bold disabled:opacity-50"
            >
              Trang sau
            </button>
          </div>
        </nav>
      )}
      {detailProperty && (
        <PropertyDetailDrawer
          property={detailProperty}
          deleting={deletingPropertyId === detailProperty.propertyId}
          onEdit={(selected) => {
            setDetailProperty(null)
            void openPropertyEdit(selected)
          }}
          onDelete={(selected) => void removeProperty(selected)}
          onClose={() => setDetailProperty(null)}
        />
      )}
      {propertyDialog && (
        <PropertyFormModal
          mode={propertyDialog}
          accessToken={accessToken ?? ""}
          initialProperty={editingProperty}
          saving={savingProperty}
          serverError={propertySubmitError}
          onClose={() => {
            if (!savingProperty) {
              setPropertySubmitError(null)
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
