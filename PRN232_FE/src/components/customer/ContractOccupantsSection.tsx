import { useEffect, useRef, useState, type FormEvent } from "react"
import { ApiError } from "@/api/apiClient"
import { useAuth } from "@/auth/AuthContext"
import {
  addContractOccupant,
  getContractOccupants,
  recordContractOccupantMoveOut,
  revealContractOccupantIdentity,
} from "@/services/contractOccupantsService"
import type {
  ContractDto,
  ContractOccupantDto,
  ContractOccupantsListDto,
} from "@/types/contract"
import { getVietnamTodayIsoDate } from "@/utils/equipmentValidation"
import { formatContractDate } from "@/utils/managementContracts"
import {
  occupantFullNameError,
  occupantIdentityNumberError,
  occupantMoveInDateError,
  occupantMoveOutDateError,
  occupantPhoneNumberError,
  REVEAL_IDENTITY_TIMEOUT_MS,
} from "@/utils/occupantsValidation"

export default function ContractOccupantsSection({
  contract,
  revealTimeoutMs = REVEAL_IDENTITY_TIMEOUT_MS,
}: {
  contract: ContractDto
  revealTimeoutMs?: number
}) {
  const { accessToken } = useAuth()
  const [data, setData] = useState<ContractOccupantsListDto | null>(null)
  const [loading, setLoading] = useState(false)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [retryCount, setRetryCount] = useState(0)

  // Chỉ giữ tạm thời trong bộ nhớ của component, tuyệt đối không lưu localStorage/sessionStorage.
  const [revealedIdentities, setRevealedIdentities] = useState<
    Record<number, string>
  >({})
  const [revealingId, setRevealingId] = useState<number | null>(null)
  const [revealError, setRevealError] = useState<string | null>(null)
  const revealTimersRef = useRef<Record<number, ReturnType<typeof setTimeout>>>(
    {},
  )

  const [isAdding, setIsAdding] = useState(false)
  const [fullName, setFullName] = useState("")
  const [phoneNumber, setPhoneNumber] = useState("")
  const [identityNumber, setIdentityNumber] = useState("")
  const [moveInDate, setMoveInDate] = useState("")
  const [note, setNote] = useState("")
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const [addError, setAddError] = useState<string | null>(null)
  const [submittingAdd, setSubmittingAdd] = useState(false)

  const [movingOutOccupant, setMovingOutOccupant] =
    useState<ContractOccupantDto | null>(null)
  const [moveOutDate, setMoveOutDate] = useState("")
  const [moveOutError, setMoveOutError] = useState<string | null>(null)
  const [submittingMoveOut, setSubmittingMoveOut] = useState(false)

  function clearAllRevealTimers() {
    for (const timer of Object.values(revealTimersRef.current)) {
      clearTimeout(timer)
    }
    revealTimersRef.current = {}
  }

  useEffect(() => {
    return () => {
      clearAllRevealTimers()
    }
  }, [])

  useEffect(() => {
    if (!accessToken) return
    const controller = new AbortController()
    setLoading(true)
    setLoadError(null)
    clearAllRevealTimers()
    setRevealedIdentities({})

    Promise.resolve(
      getContractOccupants(accessToken, contract.contractId, controller.signal),
    )
      .then((res) => {
        if (!controller.signal.aborted && res) {
          setData(res)
        }
      })
      .catch((err: unknown) => {
        if (!controller.signal.aborted) {
          setLoadError(
            err instanceof Error
              ? err.message
              : "Không thể tải danh sách người ở trong phòng.",
          )
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setLoading(false)
        }
      })

    return () => controller.abort()
  }, [accessToken, contract.contractId, retryCount])

  if (!accessToken) return null

  function hideRevealedIdentity(occupantId: number) {
    if (revealTimersRef.current[occupantId]) {
      clearTimeout(revealTimersRef.current[occupantId])
      delete revealTimersRef.current[occupantId]
    }
    setRevealedIdentities((prev) => {
      if (!(occupantId in prev)) return prev
      const next = { ...prev }
      delete next[occupantId]
      return next
    })
  }

  async function handleToggleRevealIdentity(occupant: ContractOccupantDto) {
    if (!accessToken || !data?.canRevealIdentity) return
    const occupantId = occupant.contractOccupantId

    if (revealedIdentities[occupantId]) {
      hideRevealedIdentity(occupantId)
      return
    }

    setRevealingId(occupantId)
    setRevealError(null)
    try {
      const response = await revealContractOccupantIdentity(
        accessToken,
        contract.contractId,
        occupantId,
      )
      setRevealedIdentities((prev) => ({
        ...prev,
        [occupantId]: response.identityNumber,
      }))
      if (revealTimersRef.current[occupantId]) {
        clearTimeout(revealTimersRef.current[occupantId])
      }
      revealTimersRef.current[occupantId] = setTimeout(() => {
        hideRevealedIdentity(occupantId)
      }, revealTimeoutMs)
    } catch (err: unknown) {
      setRevealError(
        err instanceof Error ? err.message : "Không thể hiển thị số CCCD.",
      )
    } finally {
      setRevealingId(null)
    }
  }

  function openAddForm() {
    const today = getVietnamTodayIsoDate()
    const defaultMoveIn =
      contract.startDate && contract.startDate > today
        ? contract.startDate
        : today
    setFullName("")
    setPhoneNumber("")
    setIdentityNumber("")
    setMoveInDate(defaultMoveIn)
    setNote("")
    setFieldErrors({})
    setAddError(null)
    setIsAdding(true)
  }

  async function handleAddOccupant(event: FormEvent) {
    event.preventDefault()
    if (!accessToken || submittingAdd) return

    const nextErrors: Record<string, string> = {}
    const nameErr = occupantFullNameError(fullName)
    if (nameErr) nextErrors.fullName = nameErr

    const phoneErr = occupantPhoneNumberError(phoneNumber)
    if (phoneErr) nextErrors.phoneNumber = phoneErr

    const idErr = occupantIdentityNumberError(identityNumber)
    if (idErr) nextErrors.identityNumber = idErr

    const dateErr = occupantMoveInDateError({
      moveInDate,
      contractStartDate: contract.startDate,
      contractEndDate: contract.endDate,
    })
    if (dateErr) nextErrors.moveInDate = dateErr

    if (Object.keys(nextErrors).length > 0) {
      setFieldErrors(nextErrors)
      setAddError(null)
      return
    }

    setSubmittingAdd(true)
    setFieldErrors({})
    setAddError(null)

    try {
      const updated = await addContractOccupant(
        accessToken,
        contract.contractId,
        {
          fullName: fullName.trim(),
          phoneNumber: phoneNumber.trim() || null,
          identityNumber: identityNumber.trim(),
          moveInDate: moveInDate.trim(),
          note: note.trim() || null,
        },
      )
      setData(updated)
      setIsAdding(false)
      setIdentityNumber("")
    } catch (err: unknown) {
      if (err instanceof ApiError && err.validationErrors) {
        const mapped: Record<string, string> = {}
        for (const [key, messages] of Object.entries(err.validationErrors)) {
          const normalizedKey = key.charAt(0).toLowerCase() + key.slice(1)
          if (messages[0]) {
            mapped[normalizedKey] = messages[0]
          }
        }
        setFieldErrors(mapped)
        if (Object.keys(mapped).length === 0) {
          setAddError(err.message)
        }
      } else {
        setAddError(
          err instanceof Error
            ? err.message
            : "Không thể thêm người ở cùng.",
        )
      }
    } finally {
      setSubmittingAdd(false)
    }
  }

  function openMoveOutForm(occupant: ContractOccupantDto) {
    setMovingOutOccupant(occupant)
    setMoveOutDate(getVietnamTodayIsoDate())
    setMoveOutError(null)
  }

  async function handleConfirmMoveOut(event: FormEvent) {
    event.preventDefault()
    if (!accessToken || !movingOutOccupant || submittingMoveOut) return

    const dateErr = occupantMoveOutDateError({
      moveOutDate,
      moveInDate: movingOutOccupant.moveInDate,
      contractEndDate: contract.endDate,
    })
    if (dateErr) {
      setMoveOutError(dateErr)
      return
    }

    setSubmittingMoveOut(true)
    setMoveOutError(null)
    try {
      const updated = await recordContractOccupantMoveOut(
        accessToken,
        contract.contractId,
        movingOutOccupant.contractOccupantId,
        { moveOutDate: moveOutDate.trim() },
      )
      setData(updated)
      setMovingOutOccupant(null)
    } catch (err: unknown) {
      setMoveOutError(
        err instanceof Error
          ? err.message
          : "Không thể ghi nhận ngày dọn ra.",
      )
    } finally {
      setSubmittingMoveOut(false)
    }
  }

  const isRoomFull = Boolean(data && data.currentCount >= data.maxOccupants)

  return (
    <section
      className="mt-5 rounded-xl border border-slate-200 bg-slate-50/70 p-4 text-sm"
      aria-label={`Người ở trong phòng hợp đồng ${contract.contractId}`}
    >
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div>
          <div className="flex flex-wrap items-center gap-2">
            <h4 className="font-bold text-slate-900">Người ở trong phòng</h4>
            {data && (
              <span className="rounded-full bg-teal-100 px-2.5 py-0.5 text-xs font-bold text-teal-900">
                {data.currentCount}/{data.maxOccupants} người
              </span>
            )}
          </div>
          <p className="mt-0.5 text-xs text-slate-600">
            Người đứng tên hợp đồng tính 1 người trong sức chứa tối đa của
            phòng.
          </p>
        </div>

        {data?.canManage && !isAdding && (
          <div className="flex flex-col items-end gap-1">
            <button
              type="button"
              disabled={isRoomFull}
              onClick={openAddForm}
              className="rounded-lg bg-[#087775] px-3 py-1.5 text-xs font-bold text-white hover:bg-teal-800 disabled:cursor-not-allowed disabled:opacity-50"
            >
              Thêm người ở cùng
            </button>
            {isRoomFull && (
              <span className="text-[11px] font-semibold text-amber-800">
                Phòng đã đủ người
              </span>
            )}
          </div>
        )}
      </div>

      {loading && (
        <p role="status" className="mt-3 text-xs text-slate-500">
          Đang tải danh sách người ở trong phòng...
        </p>
      )}

      {!loading && loadError && (
        <p role="alert" className="mt-3 text-xs text-rose-600">
          {loadError}{" "}
          <button
            type="button"
            onClick={() => setRetryCount((c) => c + 1)}
            className="font-semibold underline"
          >
            Thử lại
          </button>
        </p>
      )}

      {revealError && (
        <p
          role="alert"
          className="mt-3 rounded-lg bg-rose-50 px-3 py-2 text-xs text-rose-800"
        >
          {revealError}
        </p>
      )}

      {!loading && !loadError && data && (
        <div className="mt-3 space-y-3">
          <div className="divide-y divide-slate-100 rounded-xl border border-slate-200 bg-white">
            {/* Dòng đầu: Người đứng tên */}
            <div className="flex flex-wrap items-center justify-between gap-2 px-4 py-3 text-xs">
              <div>
                <span className="font-bold text-slate-900">
                  {data.holderName || contract.customerName}
                </span>
                {contract.customerPhone && (
                  <span className="ml-2 text-slate-500">
                    · {contract.customerPhone}
                  </span>
                )}
              </div>
              <span className="rounded-full bg-[#087775]/10 px-2.5 py-0.5 text-[11px] font-bold text-[#087775]">
                Người đứng tên
              </span>
            </div>

            {/* Danh sách người ở cùng */}
            {data.occupants.map((occupant) => {
              const revealedValue =
                revealedIdentities[occupant.contractOccupantId]
              const isRevealed = Boolean(revealedValue)
              const isMovedOut =
                !occupant.isActive || Boolean(occupant.moveOutDate)

              return (
                <div
                  key={occupant.contractOccupantId}
                  className={`flex flex-wrap items-center justify-between gap-3 px-4 py-3 text-xs ${
                    isMovedOut ? "bg-slate-50/80 opacity-60" : ""
                  }`}
                >
                  <div className="space-y-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <strong className="font-bold text-slate-900">
                        {occupant.fullName}
                      </strong>
                      {occupant.phoneNumber && (
                        <span className="text-slate-600">
                          · {occupant.phoneNumber}
                        </span>
                      )}
                      {isMovedOut && occupant.moveOutDate ? (
                        <span className="rounded-full bg-slate-200 px-2 py-0.5 text-[11px] font-semibold text-slate-700">
                          Đã dọn ra {formatContractDate(occupant.moveOutDate)} (
                          {occupant.moveOutDate})
                        </span>
                      ) : (
                        <span className="rounded-full bg-emerald-100 px-2 py-0.5 text-[11px] font-semibold text-emerald-800">
                          Đang ở · Dọn vào{" "}
                          {formatContractDate(occupant.moveInDate)}
                        </span>
                      )}
                    </div>

                    <div className="flex flex-wrap items-center gap-2 text-slate-700">
                      <span>
                        CCCD:{" "}
                        <code className="rounded bg-slate-100 px-1.5 py-0.5 font-mono font-semibold text-slate-900">
                          {revealedValue ?? occupant.maskedIdentityNumber}
                        </code>
                      </span>

                      {data.canRevealIdentity && (
                        <button
                          type="button"
                          disabled={
                            revealingId === occupant.contractOccupantId
                          }
                          aria-label={
                            isRevealed
                              ? `Ẩn CCCD ${occupant.fullName}`
                              : `Hiện CCCD ${occupant.fullName}`
                          }
                          title={
                            isRevealed ? "Ẩn số CCCD" : "Xem số CCCD đầy đủ"
                          }
                          onClick={() =>
                            void handleToggleRevealIdentity(occupant)
                          }
                          className="inline-flex items-center gap-1 rounded border border-slate-300 bg-white px-2 py-0.5 text-[11px] font-semibold text-slate-700 hover:bg-slate-100 disabled:opacity-50"
                        >
                          <span aria-hidden="true">👁</span>
                          <span>{isRevealed ? "Ẩn" : "Xem"}</span>
                        </button>
                      )}

                      {isRevealed && (
                        <span className="rounded bg-amber-50 px-1.5 py-0.5 text-[11px] font-medium text-amber-900">
                          Lần xem này được ghi lại
                        </span>
                      )}
                    </div>

                    {occupant.note && (
                      <p className="text-[11px] text-slate-500">
                        Ghi chú: {occupant.note}
                      </p>
                    )}
                  </div>

                  {data.canManage && !isMovedOut && (
                    <div>
                      <button
                        type="button"
                        onClick={() => openMoveOutForm(occupant)}
                        className="rounded-lg border border-slate-300 bg-white px-2.5 py-1 text-[11px] font-bold text-slate-700 hover:bg-slate-100"
                      >
                        Ghi ngày dọn ra
                      </button>
                    </div>
                  )}
                </div>
              )
            })}
          </div>

          {isAdding && (
            <form
              noValidate
              onSubmit={(e) => void handleAddOccupant(e)}
              aria-label="Biểu mẫu thêm người ở cùng"
              className="space-y-3 rounded-xl border border-teal-200 bg-white p-4 text-xs"
            >
              <h5 className="text-sm font-bold text-slate-900">
                Thêm người ở cùng
              </h5>

              <div className="grid gap-3 sm:grid-cols-2">
                <label className="block font-semibold text-slate-700">
                  Họ tên *
                  <input
                    type="text"
                    value={fullName}
                    onChange={(e) => {
                      setFullName(e.target.value)
                      setFieldErrors((prev) => ({ ...prev, fullName: "" }))
                    }}
                    placeholder="Nguyễn Văn B"
                    className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-1.5 text-xs text-slate-900"
                  />
                  {fieldErrors.fullName && (
                    <span className="mt-1 block font-normal text-rose-600">
                      {fieldErrors.fullName}
                    </span>
                  )}
                </label>

                <label className="block font-semibold text-slate-700">
                  Số điện thoại
                  <input
                    type="tel"
                    value={phoneNumber}
                    onChange={(e) => {
                      setPhoneNumber(e.target.value)
                      setFieldErrors((prev) => ({ ...prev, phoneNumber: "" }))
                    }}
                    placeholder="0905123456"
                    className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-1.5 text-xs text-slate-900"
                  />
                  {fieldErrors.phoneNumber && (
                    <span className="mt-1 block font-normal text-rose-600">
                      {fieldErrors.phoneNumber}
                    </span>
                  )}
                </label>

                <label className="block font-semibold text-slate-700">
                  Số CCCD (12 chữ số) *
                  <input
                    type="text"
                    inputMode="numeric"
                    maxLength={12}
                    autoComplete="off"
                    value={identityNumber}
                    onChange={(e) => {
                      setIdentityNumber(e.target.value)
                      setFieldErrors((prev) => ({
                        ...prev,
                        identityNumber: "",
                      }))
                    }}
                    placeholder="079203001234"
                    className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-1.5 font-mono text-xs text-slate-900"
                  />
                  {fieldErrors.identityNumber && (
                    <span className="mt-1 block font-normal text-rose-600">
                      {fieldErrors.identityNumber}
                    </span>
                  )}
                </label>

                <label className="block font-semibold text-slate-700">
                  Ngày dọn vào *
                  <input
                    type="date"
                    min={contract.startDate}
                    max={contract.endDate}
                    value={moveInDate}
                    onChange={(e) => {
                      setMoveInDate(e.target.value)
                      setFieldErrors((prev) => ({ ...prev, moveInDate: "" }))
                    }}
                    className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-1.5 text-xs text-slate-900"
                  />
                  {fieldErrors.moveInDate && (
                    <span className="mt-1 block font-normal text-rose-600">
                      {fieldErrors.moveInDate}
                    </span>
                  )}
                </label>
              </div>

              <label className="block font-semibold text-slate-700">
                Ghi chú
                <input
                  type="text"
                  value={note}
                  maxLength={300}
                  onChange={(e) => setNote(e.target.value)}
                  placeholder="Ghi chú thêm (không bắt buộc)"
                  className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-1.5 text-xs text-slate-900"
                />
              </label>

              {addError && (
                <p
                  role="alert"
                  className="rounded-lg bg-rose-50 px-3 py-2 text-xs font-medium text-rose-800"
                >
                  {addError}
                </p>
              )}

              <div className="flex justify-end gap-2">
                <button
                  type="button"
                  disabled={submittingAdd}
                  onClick={() => {
                    setIsAdding(false)
                    setIdentityNumber("")
                    setFieldErrors({})
                    setAddError(null)
                  }}
                  className="rounded-lg border border-slate-300 px-3 py-1.5 text-xs font-semibold text-slate-700"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={submittingAdd}
                  className="rounded-lg bg-[#087775] px-3.5 py-1.5 text-xs font-bold text-white disabled:opacity-50"
                >
                  {submittingAdd ? "Đang lưu..." : "Lưu người ở cùng"}
                </button>
              </div>
            </form>
          )}

          {movingOutOccupant && (
            <form
              noValidate
              onSubmit={(e) => void handleConfirmMoveOut(e)}
              aria-label="Biểu mẫu ghi ngày dọn ra"
              className="space-y-3 rounded-xl border border-amber-200 bg-white p-4 text-xs"
            >
              <h5 className="text-sm font-bold text-slate-900">
                Ghi ngày dọn ra: {movingOutOccupant.fullName}
              </h5>
              <label className="block max-w-xs font-semibold text-slate-700">
                Ngày dọn ra *
                <input
                  type="date"
                  min={movingOutOccupant.moveInDate}
                  max={contract.endDate}
                  value={moveOutDate}
                  onChange={(e) => {
                    setMoveOutDate(e.target.value)
                    setMoveOutError(null)
                  }}
                  className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-1.5 text-xs text-slate-900"
                />
              </label>

              {moveOutError && (
                <p
                  role="alert"
                  className="rounded-lg bg-rose-50 px-3 py-2 text-xs font-medium text-rose-800"
                >
                  {moveOutError}
                </p>
              )}

              <div className="flex justify-end gap-2">
                <button
                  type="button"
                  disabled={submittingMoveOut}
                  onClick={() => {
                    setMovingOutOccupant(null)
                    setMoveOutError(null)
                  }}
                  className="rounded-lg border border-slate-300 px-3 py-1.5 text-xs font-semibold text-slate-700"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={submittingMoveOut}
                  className="rounded-lg bg-[#087775] px-3.5 py-1.5 text-xs font-bold text-white disabled:opacity-50"
                >
                  {submittingMoveOut ? "Đang lưu..." : "Lưu ngày dọn ra"}
                </button>
              </div>
            </form>
          )}
        </div>
      )}
    </section>
  )
}
