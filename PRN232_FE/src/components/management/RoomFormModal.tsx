import { useEffect, useMemo, useState, type FormEvent } from "react"
import { useAuth } from "@/auth/AuthContext"
import { getPropertyRoomTypes } from "@/services/propertyRoomTypeService"
import type { PropertyRoomTypeDto } from "@/types/propertyRoomType"
import type { PropertyDto } from "@/types/user"
import type {
  CreateRoomRequest,
  ManagementRoomDto,
  UpdateRoomRequest,
} from "@/types/managementRoom"

type RoomFormModalProps = {
  mode: "create" | "edit"
  initialRoom?: ManagementRoomDto | null
  properties: PropertyDto[]
  lockedPropertyId?: number
  saving: boolean
  onClose: () => void
  onSubmit: (request: CreateRoomRequest | UpdateRoomRequest) => Promise<boolean>
}

type FormValues = {
  propertyId: string
  roomTypeId: string
  roomCode: string
  areaSqm: string
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
  }
}

export default function RoomFormModal({
  mode,
  initialRoom,
  properties,
  lockedPropertyId,
  saving,
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
  const [formError, setFormError] = useState("")

  useEffect(() => {
    setValues(valuesFromRoom(initialRoom, lockedPropertyId))
    setFormError("")
  }, [initialRoom, lockedPropertyId, mode])

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
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const propertyId = Number(values.propertyId)
    const roomTypeId = Number(values.roomTypeId)
    const areaSqm = Number(values.areaSqm)
    const roomCode = values.roomCode.trim()

    if (mode === "create" && (!Number.isInteger(propertyId) || propertyId < 1)) {
      setFormError("Vui lòng chọn khu trọ.")
      return
    }
    if (!Number.isInteger(roomTypeId) || roomTypeId < 1) {
      setFormError("Vui lòng chọn loại phòng.")
      return
    }
    const selectedConfiguration = propertyRoomTypes.find((item) => item.roomTypeId === roomTypeId)
    if (!selectedConfiguration ||
      (!selectedConfiguration.isActive && roomTypeId !== initialRoom?.roomTypeId)) {
      setFormError("Loại phòng chưa được kích hoạt tại khu trọ đã chọn.")
      return
    }
    if (!roomCode) {
      setFormError("Mã phòng không được để trống.")
      return
    }
    if (!Number.isFinite(areaSqm) || areaSqm <= 0) {
      setFormError("Diện tích phòng phải lớn hơn 0.")
      return
    }

    setFormError("")
    const request =
      mode === "create"
        ? { propertyId, roomTypeId, roomCode, areaSqm }
        : { roomTypeId, roomCode, areaSqm }
    const saved = await onSubmit(request)
    if (!saved) return
  }

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
          <form onSubmit={submit} className="mt-6 space-y-4">
            {formError && (
              <p
                role="alert"
                className="rounded-lg bg-rose-50 px-3 py-2 text-sm text-rose-700"
              >
                {formError}
              </p>
            )}
            <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
              Khu trọ
              <select
                aria-label="Khu trọ"
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
            <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
              Loại phòng
              <select
                aria-label="Loại phòng"
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
            <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
              Mã phòng
              <input
                aria-label="Mã phòng"
                value={values.roomCode}
                onChange={(event) =>
                  updateValue("roomCode", event.target.value)
                }
                className="rounded-lg border border-slate-200 px-3 py-2 font-normal"
              />
            </label>
            <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700">
              Diện tích (m²)
              <input
                aria-label="Diện tích (m²)"
                type="number"
                min="0.01"
                step="0.01"
                value={values.areaSqm}
                onChange={(event) => updateValue("areaSqm", event.target.value)}
                className="rounded-lg border border-slate-200 px-3 py-2 font-normal"
              />
            </label>
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
