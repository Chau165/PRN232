import { useCallback, useEffect, useMemo, useState, type FormEvent } from "react"
import { ApiError } from "@/api/apiClient"
import { useAuth } from "@/auth/AuthContext"
import { getPropertyRoomTypes } from "@/services/propertyRoomTypeService"
import type { PropertyRoomTypeDto } from "@/types/propertyRoomType"
import type { PropertyDto } from "@/types/user"
import type {
  CreateRoomRequest,
  ManagementRoomDto,
  UpdateRoomRequest,
} from "@/types/managementRoom"
import {
  ROOM_AREA_MAX,
  ROOM_AREA_MIN,
  ROOM_AREA_STEP,
  ROOM_FLOOR_MAX,
  ROOM_FLOOR_MIN,
  ROOM_MAX_OCCUPANTS_MAX,
  ROOM_MAX_OCCUPANTS_MIN,
  maxOccupantsByArea,
  splitValidationErrors,
  validateRoomForm,
  type RoomFormField,
} from "@/utils/roomValidation"

const ROOM_FORM_FIELDS: readonly RoomFormField[] = [
  "propertyId",
  "roomTypeId",
  "roomCode",
  "areaSqm",
  "maxOccupants",
  "floor",
  "description",
]

type RoomFormModalProps = {
  mode: "create" | "edit"
  initialRoom?: ManagementRoomDto | null
  properties: PropertyDto[]
  lockedPropertyId?: number
  saving: boolean
  serverError?: unknown
  onClose: () => void
  onSubmit: (request: CreateRoomRequest | UpdateRoomRequest) => Promise<boolean>
}

type FormValues = {
  propertyId: string
  roomTypeId: string
  roomCode: string
  areaSqm: string
  maxOccupants: string
  floor: string
  description: string
}

function valuesFromRoom(
  room: ManagementRoomDto | null | undefined,
  lockedPropertyId?: number,
): FormValues {
  return {
    propertyId: room
      ? String(room.propertyId)
      : lockedPropertyId !== undefined
        ? String(lockedPropertyId)
        : "",
    roomTypeId: room ? String(room.roomTypeId) : "",
    roomCode: room?.roomCode ?? "",
    areaSqm: room ? String(room.areaSqm) : "",
    maxOccupants:
      room?.maxOccupants !== undefined && room?.maxOccupants !== null
        ? String(room.maxOccupants)
        : "",
    floor:
      room?.floor !== undefined && room?.floor !== null
        ? String(room.floor)
        : "",
    description: room?.description ?? "",
  }
}

export default function RoomFormModal({
  mode,
  initialRoom,
  properties,
  lockedPropertyId,
  saving,
  serverError,
  onClose,
  onSubmit,
}: RoomFormModalProps) {
  const { accessToken } = useAuth()
  const [values, setValues] = useState<FormValues>(() =>
    valuesFromRoom(initialRoom, lockedPropertyId),
  )
  const [propertyRoomTypes, setPropertyRoomTypes] = useState<PropertyRoomTypeDto[]>([])
  const [loadingRoomTypes, setLoadingRoomTypes] = useState(false)
  const [roomTypesError, setRoomTypesError] = useState("")
  const [fieldErrors, setFieldErrors] = useState<
    Partial<Record<RoomFormField, string>>
  >({})
  const [formError, setFormError] = useState("")

  const applyServerError = useCallback((cause: unknown) => {
    if (!cause) return
    if (cause instanceof ApiError && cause.status === 400) {
      const { fieldErrors: mapped, generalError } = splitValidationErrors(
        cause.validationErrors,
        ROOM_FORM_FIELDS,
        cause.message,
      )
      setFieldErrors(mapped)
      setFormError(generalError)
      return
    }
    setFieldErrors({})
    if (cause instanceof ApiError) {
      if (cause.status === 409) {
        setFormError(
          cause.message ||
            "Thao tác bị từ chối vì phòng còn dữ liệu nghiệp vụ cần bảo toàn.",
        )
        return
      }
      if (cause.status === 403) {
        setFormError(
          cause.message || "Bạn không có quyền thực hiện thao tác này.",
        )
        return
      }
      setFormError(cause.message)
      return
    }
    setFormError(
      cause instanceof Error ? cause.message : "Không thể lưu dữ liệu phòng.",
    )
  }, [])

  useEffect(() => {
    setValues(valuesFromRoom(initialRoom, lockedPropertyId))
    setFieldErrors({})
    setFormError("")
  }, [initialRoom, lockedPropertyId, mode])

  useEffect(() => {
    if (serverError) {
      applyServerError(serverError)
    }
  }, [applyServerError, serverError])

  useEffect(() => {
    const propertyId = Number(values.propertyId)
    if (!accessToken || !Number.isSafeInteger(propertyId) || propertyId < 1) {
      setPropertyRoomTypes([])
      setRoomTypesError("")
      setLoadingRoomTypes(false)
      return
    }

    const controller = new AbortController()
    setLoadingRoomTypes(true)
    setRoomTypesError("")
    void getPropertyRoomTypes(accessToken, propertyId, controller.signal)
      .then((roomTypes) => {
        if (!controller.signal.aborted) setPropertyRoomTypes(roomTypes)
      })
      .catch((cause: unknown) => {
        if (!controller.signal.aborted) {
          setPropertyRoomTypes([])
          setRoomTypesError(cause instanceof Error ? cause.message : "Không thể tải loại phòng của khu trọ.")
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoadingRoomTypes(false)
      })

    return () => controller.abort()
  }, [accessToken, values.propertyId])

  const roomTypes = useMemo(
    () => propertyRoomTypes.filter((roomType) =>
      roomType.isActive || roomType.roomTypeId === initialRoom?.roomTypeId),
    [initialRoom?.roomTypeId, propertyRoomTypes],
  )

  const selectedRoomType = useMemo(
    () =>
      roomTypes.find(
        (roomType) => String(roomType.roomTypeId) === values.roomTypeId,
      ),
    [roomTypes, values.roomTypeId],
  )

  function updateValue(field: keyof FormValues, value: string) {
    setValues((current) => ({
      ...current,
      [field]: value,
      ...(field === "propertyId" ? { roomTypeId: "" } : {}),
    }))
    setFieldErrors((current) => {
      if (
        !current[field] &&
        !(field === "propertyId" && current.roomTypeId) &&
        !(field === "areaSqm" && current.maxOccupants)
      ) {
        return current
      }
      const next = { ...current }
      delete next[field]
      if (field === "propertyId") delete next.roomTypeId
      if (field === "areaSqm") delete next.maxOccupants
      return next
    })
    setFormError("")
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const nextFieldErrors = validateRoomForm({
      mode,
      propertyId: values.propertyId,
      roomTypeId: values.roomTypeId,
      roomCode: values.roomCode,
      areaSqm: values.areaSqm,
      maxOccupants: values.maxOccupants,
      floor: values.floor,
      description: values.description,
    })

    const roomTypeId = Number(values.roomTypeId)
    if (!nextFieldErrors.roomTypeId) {
      const selectedConfiguration = propertyRoomTypes.find(
        (item) => item.roomTypeId === roomTypeId,
      )
      if (
        !selectedConfiguration ||
        (!selectedConfiguration.isActive &&
          roomTypeId !== initialRoom?.roomTypeId)
      ) {
        nextFieldErrors.roomTypeId =
          "Loại phòng chưa được kích hoạt tại khu trọ đã chọn."
      }
    }

    if (Object.keys(nextFieldErrors).length > 0) {
      setFieldErrors(nextFieldErrors)
      setFormError("")
      return
    }

    const propertyId = Number(values.propertyId)
    const areaSqm = Number(values.areaSqm)
    const roomCode = values.roomCode.trim()
    const maxOccupants =
      values.maxOccupants.trim() === "" ? null : Number(values.maxOccupants)
    const floor = values.floor.trim() === "" ? null : Number(values.floor)
    const description =
      values.description.trim() === "" ? null : values.description.trim()

    setFieldErrors({})
    setFormError("")
    const request: CreateRoomRequest | UpdateRoomRequest =
      mode === "create"
        ? {
            propertyId,
            roomTypeId,
            roomCode,
            areaSqm,
            ...(maxOccupants !== null ? { maxOccupants } : {}),
            ...(floor !== null ? { floor } : {}),
            ...(description !== null ? { description } : {}),
          }
        : {
            roomTypeId,
            roomCode,
            areaSqm,
            maxOccupants,
            floor,
            description,
          }
    try {
      const saved = await onSubmit(request)
      if (!saved) return
    } catch (cause) {
      applyServerError(cause)
    }
  }

  const maxAllowedOccupants = maxOccupantsByArea(values.areaSqm)
  const isMissingDependency = properties.length === 0
  const title = mode === "create" ? "Thêm phòng" : "Sửa phòng"
  const submitLabel = mode === "create" ? "Tạo phòng" : "Lưu phòng"

  return (
    <div className="fixed inset-0 z-[70] grid place-items-center bg-slate-950/40 p-4">
      <section
        role="dialog"
        aria-modal="true"
        aria-labelledby="room-form-title"
        className="w-full max-w-lg rounded-2xl bg-white p-6 shadow-2xl"
      >
        <div className="flex items-start justify-between gap-4">
          <div>
            <p className="text-[11px] font-bold uppercase tracking-[.14em] text-[#087775]">
              Phòng
            </p>
            <h2
              id="room-form-title"
              className="mt-1 font-display text-2xl font-bold text-slate-900"
            >
              {title}
            </h2>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="Đóng"
            className="text-2xl text-slate-400"
          >
            ×
          </button>
        </div>
        {isMissingDependency && mode === "create" ? (
          <div
            className="mt-6 space-y-3 rounded-xl bg-amber-50 p-4 text-sm text-amber-900"
            role="status"
          >
            {properties.length === 0 && (
              <p>Vui lòng tạo khu trọ trước khi thêm phòng.</p>
            )}
          </div>
        ) : (
          <form onSubmit={submit} noValidate className="mt-6 space-y-4">
            {formError && (
              <p
                role="alert"
                className="rounded-lg bg-rose-50 px-3 py-2 text-sm text-rose-700"
              >
                {formError}
              </p>
            )}
            <div>
              <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
                Khu trọ
                <select
                  aria-label="Khu trọ"
                  aria-invalid={Boolean(fieldErrors.propertyId)}
                  value={values.propertyId}
                  onChange={(event) =>
                    updateValue("propertyId", event.target.value)
                  }
                  disabled={mode === "edit" || lockedPropertyId !== undefined}
                  className="rounded-lg border border-slate-200 px-3 py-2 font-normal disabled:bg-slate-50"
                >
                  <option value="">Chọn khu trọ</option>
                  {properties.map((property) => (
                    <option key={property.propertyId} value={property.propertyId}>
                      {property.propertyName}
                    </option>
                  ))}
                </select>
              </label>
              {fieldErrors.propertyId && (
                <p role="alert" className="mt-1 text-xs text-rose-600">
                  {fieldErrors.propertyId}
                </p>
              )}
            </div>
            <div>
              <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
                Loại phòng
                <select
                  aria-label="Loại phòng"
                  aria-invalid={Boolean(fieldErrors.roomTypeId)}
                  value={values.roomTypeId}
                  onChange={(event) =>
                    updateValue("roomTypeId", event.target.value)
                  }
                  disabled={!values.propertyId || loadingRoomTypes || !!roomTypesError}
                  className="rounded-lg border border-slate-200 px-3 py-2 font-normal disabled:bg-slate-50"
                >
                  <option value="">Chọn loại phòng</option>
                  {roomTypes.map((roomType) => (
                    <option
                      key={roomType.roomTypeId}
                      value={roomType.roomTypeId}
                      disabled={!roomType.isActive && roomType.roomTypeId !== initialRoom?.roomTypeId}
                    >
                      {roomType.roomTypeName}{roomType.isActive ? "" : " (ngừng dùng)"}
                    </option>
                  ))}
                </select>
              </label>
              {fieldErrors.roomTypeId && (
                <p role="alert" className="mt-1 text-xs text-rose-600">
                  {fieldErrors.roomTypeId}
                </p>
              )}
            </div>
            {loadingRoomTypes && <p role="status" className="text-xs text-slate-500">Đang tải loại phòng của khu trọ...</p>}
            {roomTypesError && <p role="alert" className="text-xs text-rose-600">{roomTypesError}</p>}
            {!loadingRoomTypes && !roomTypesError && values.propertyId && roomTypes.length === 0 && (
              <p role="status" className="text-xs text-amber-700">Khu trọ chưa có loại phòng đang hoạt động. Hãy cấu hình loại phòng trước.</p>
            )}
            <p className="rounded-lg bg-slate-50 px-3 py-2 text-sm text-slate-600">
              Giá thuê tại khu trọ:{" "}
              <strong>
                {selectedRoomType
                  ? new Intl.NumberFormat("vi-VN", {
                      style: "currency",
                      currency: "VND",
                    }).format(selectedRoomType.monthlyRent)
                  : "Chưa chọn"}
              </strong>
            </p>
            <div>
              <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
                Mã phòng
                <input
                  aria-label="Mã phòng"
                  aria-invalid={Boolean(fieldErrors.roomCode)}
                  value={values.roomCode}
                  onChange={(event) =>
                    updateValue("roomCode", event.target.value)
                  }
                  className="rounded-lg border border-slate-200 px-3 py-2 font-normal"
                />
              </label>
              {fieldErrors.roomCode && (
                <p role="alert" className="mt-1 text-xs text-rose-600">
                  {fieldErrors.roomCode}
                </p>
              )}
            </div>
            <div>
              <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
                Diện tích (m²)
                <input
                  aria-label="Diện tích (m²)"
                  aria-invalid={Boolean(fieldErrors.areaSqm)}
                  type="number"
                  min={ROOM_AREA_MIN}
                  max={ROOM_AREA_MAX}
                  step={ROOM_AREA_STEP}
                  value={values.areaSqm}
                  onChange={(event) => updateValue("areaSqm", event.target.value)}
                  className="rounded-lg border border-slate-200 px-3 py-2 font-normal"
                />
              </label>
              {fieldErrors.areaSqm && (
                <p role="alert" className="mt-1 text-xs text-rose-600">
                  {fieldErrors.areaSqm}
                </p>
              )}
            </div>
            <div className="grid gap-4 sm:grid-cols-2">
              <div>
                <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
                  Số người tối đa
                  <input
                    aria-label="Số người tối đa"
                    aria-invalid={Boolean(fieldErrors.maxOccupants)}
                    type="number"
                    min={ROOM_MAX_OCCUPANTS_MIN}
                    max={maxAllowedOccupants ?? ROOM_MAX_OCCUPANTS_MAX}
                    step={1}
                    value={values.maxOccupants}
                    onChange={(event) =>
                      updateValue("maxOccupants", event.target.value)
                    }
                    className="rounded-lg border border-slate-200 px-3 py-2 font-normal"
                  />
                </label>
                {maxAllowedOccupants !== null && (
                  <p className="mt-1 text-xs text-slate-500">
                    Tối đa {maxAllowedOccupants} người cho phòng {Number(values.areaSqm)} m²
                  </p>
                )}
                {fieldErrors.maxOccupants && (
                  <p role="alert" className="mt-1 text-xs text-rose-600">
                    {fieldErrors.maxOccupants}
                  </p>
                )}
              </div>
              <div>
                <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
                  Tầng
                  <input
                    aria-label="Tầng"
                    aria-invalid={Boolean(fieldErrors.floor)}
                    type="number"
                    min={ROOM_FLOOR_MIN}
                    max={ROOM_FLOOR_MAX}
                    step={1}
                    placeholder="0 là tầng trệt"
                    value={values.floor}
                    onChange={(event) => updateValue("floor", event.target.value)}
                    className="rounded-lg border border-slate-200 px-3 py-2 font-normal"
                  />
                </label>
                {fieldErrors.floor && (
                  <p role="alert" className="mt-1 text-xs text-rose-600">
                    {fieldErrors.floor}
                  </p>
                )}
              </div>
            </div>
            <div>
              <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
                Mô tả phòng
                <textarea
                  aria-label="Mô tả phòng"
                  aria-invalid={Boolean(fieldErrors.description)}
                  rows={3}
                  value={values.description}
                  onChange={(event) =>
                    updateValue("description", event.target.value)
                  }
                  className="rounded-lg border border-slate-200 px-3 py-2 font-normal"
                />
              </label>
              {fieldErrors.description && (
                <p role="alert" className="mt-1 text-xs text-rose-600">
                  {fieldErrors.description}
                </p>
              )}
            </div>
            <p className="text-xs text-slate-500">
              Trạng thái ban đầu do Backend xác định là Trống. Form này không
              thay đổi trạng thái vận hành.
            </p>
            <div className="flex justify-end gap-3 border-t border-slate-100 pt-4">
              <button
                type="button"
                onClick={onClose}
                className="rounded-lg bg-slate-100 px-4 py-2 text-sm font-bold text-slate-600"
              >
                Hủy
              </button>
              <button
                type="submit"
                disabled={saving}
                className="rounded-lg bg-[#087775] px-4 py-2 text-sm font-bold text-white disabled:opacity-50"
              >
                {saving ? "Đang lưu..." : submitLabel}
              </button>
            </div>
          </form>
        )}
      </section>
    </div>
  )
}
