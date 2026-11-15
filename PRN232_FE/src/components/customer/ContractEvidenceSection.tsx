import { useEffect, useState, type ChangeEvent, type FormEvent } from "react"
import { ApiError } from "@/api/apiClient"
import { useAuth } from "@/auth/AuthContext"
import {
  deleteContractEvidence,
  getContractEvidence,
  hideContractEvidence,
  uploadContractEvidence,
} from "@/services/contractEvidenceService"
import type {
  ContractDto,
  ContractEvidenceFileDto,
  ContractEvidenceKind,
  ContractEvidenceListDto,
} from "@/types/contract"
import {
  formatEvidenceSize,
  formatEvidenceUploadedAt,
  isPdfEvidenceFile,
  MAX_EVIDENCE_FILES_PER_KIND,
  MAX_HIDE_EVIDENCE_REASON_LENGTH,
  validateContractEvidenceFile,
  validateEvidenceBatchCount,
  validateHideEvidenceReason,
} from "@/utils/contractEvidenceValidation"
import { formatContractDate } from "@/utils/managementContracts"

interface EvidenceSectionConfig {
  kind: ContractEvidenceKind
  title: string
  description: string
  emptyText: string
}

const EVIDENCE_SECTIONS: EvidenceSectionConfig[] = [
  {
    kind: "MoveIn",
    title: "Ảnh nhận phòng",
    description:
      "Ảnh hiện trạng phòng lúc nhận bàn giao và biên bản nhận phòng giấy đã ký.",
    emptyText: "Chưa có ảnh hoặc tài liệu nhận phòng.",
  },
  {
    kind: "MoveOut",
    title: "Ảnh trả phòng",
    description:
      "Ảnh hiện trạng phòng lúc trả phòng và biên bản trả phòng giấy đã ký.",
    emptyText: "Chưa có ảnh hoặc tài liệu trả phòng.",
  },
  {
    kind: "SignedContract",
    title: "Hợp đồng đã ký",
    description: "Ảnh chụp hoặc bản scan PDF hợp đồng thuê phòng giấy đã ký.",
    emptyText: "Chưa có ảnh hoặc bản scan hợp đồng đã ký.",
  },
]

interface UploadProgressState {
  kind: ContractEvidenceKind
  current: number
  total: number
  fileName: string
}

export default function ContractEvidenceSection({
  contract,
  isDepositSettled = false,
}: {
  contract: ContractDto
  isDepositSettled?: boolean
}) {
  const { accessToken, user } = useAuth()
  const normalizedRole = user?.role?.toUpperCase() ?? "CUSTOMER"
  const isAdmin = normalizedRole === "ADMIN"

  const [evidence, setEvidence] = useState<ContractEvidenceListDto | null>(null)
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [sectionErrors, setSectionErrors] = useState<
    Partial<Record<ContractEvidenceKind, string>>
  >({})
  const [sectionSuccess, setSectionSuccess] = useState<
    Partial<Record<ContractEvidenceKind, string>>
  >({})
  const [uploadProgress, setUploadProgress] =
    useState<UploadProgressState | null>(null)

  const [confirmDeleteFile, setConfirmDeleteFile] =
    useState<ContractEvidenceFileDto | null>(null)
  const [deletingFileId, setDeletingFileId] = useState<number | null>(null)
  const [deleteError, setDeleteError] = useState<string | null>(null)

  const [hidingFile, setHidingFile] =
    useState<ContractEvidenceFileDto | null>(null)
  const [hideReason, setHideReason] = useState("")
  const [hideError, setHideError] = useState<string | null>(null)
  const [submittingHide, setSubmittingHide] = useState(false)

  const [previewImage, setPreviewImage] =
    useState<ContractEvidenceFileDto | null>(null)

  useEffect(() => {
    if (!accessToken) {
      setLoading(false)
      return
    }
    const controller = new AbortController()
    setLoading(true)
    setLoadError(null)
    getContractEvidence(accessToken, contract.contractId, controller.signal)
      .then((data) => {
        if (controller.signal.aborted) return
        setEvidence(data)
      })
      .catch((err) => {
        if (controller.signal.aborted) return
        setLoadError(
          err instanceof Error
            ? err.message
            : "Không thể tải danh sách minh chứng hợp đồng.",
        )
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setLoading(false)
        }
      })

    return () => controller.abort()
  }, [accessToken, contract.contractId])

  if (!accessToken) return null

  function canUploadForKind(kind: ContractEvidenceKind): boolean {
    if (!evidence || isAdmin) return false
    if (kind === "MoveIn") return Boolean(evidence.canUploadMoveIn)
    if (kind === "MoveOut")
      return !isDepositSettled && Boolean(evidence.canUploadMoveOut)
    return Boolean(evidence.canUploadSignedContract)
  }

  function filesByKind(kind: ContractEvidenceKind): ContractEvidenceFileDto[] {
    return (evidence?.files ?? []).filter((file) => file.kind === kind)
  }

  async function handleSelectFiles(
    kind: ContractEvidenceKind,
    event: ChangeEvent<HTMLInputElement>,
  ) {
    const fileList = event.target.files
    const selectedFiles = fileList ? Array.from(fileList) : []
    event.target.value = ""

    if (selectedFiles.length === 0 || !accessToken) return

    setSectionErrors((prev) => ({ ...prev, [kind]: undefined }))
    setSectionSuccess((prev) => ({ ...prev, [kind]: undefined }))

    const existingCount = filesByKind(kind).length
    const countError = validateEvidenceBatchCount(
      existingCount,
      selectedFiles.length,
    )
    if (countError) {
      setSectionErrors((prev) => ({ ...prev, [kind]: countError }))
      return
    }

    for (const file of selectedFiles) {
      const fileError = validateContractEvidenceFile(file)
      if (fileError) {
        setSectionErrors((prev) => ({
          ...prev,
          [kind]: `${file.name}: ${fileError}`,
        }))
        return
      }
    }

    let uploadedCount = 0
    for (let index = 0; index < selectedFiles.length; index++) {
      const file = selectedFiles[index]
      setUploadProgress({
        kind,
        current: index + 1,
        total: selectedFiles.length,
        fileName: file.name,
      })

      try {
        const updated = await uploadContractEvidence(
          accessToken,
          contract.contractId,
          kind,
          file,
        )
        setEvidence(updated)
        uploadedCount += 1
      } catch (err) {
        const message =
          err instanceof ApiError || err instanceof Error
            ? err.message
            : "Không thể tải file minh chứng lên."
        setSectionErrors((prev) => ({
          ...prev,
          [kind]: `${file.name}: ${message}`,
        }))
        setUploadProgress(null)
        return
      }
    }

    setUploadProgress(null)
    if (uploadedCount > 0) {
      setSectionSuccess((prev) => ({
        ...prev,
        [kind]: `Đã tải lên ${uploadedCount} file minh chứng thành công.`,
      }))
    }
  }

  async function handleConfirmDelete() {
    if (!confirmDeleteFile || !accessToken) return
    setDeletingFileId(confirmDeleteFile.contractEvidenceFileId)
    setDeleteError(null)
    try {
      const updated = await deleteContractEvidence(
        accessToken,
        contract.contractId,
        confirmDeleteFile.contractEvidenceFileId,
      )
      setEvidence(updated)
      setConfirmDeleteFile(null)
    } catch (err) {
      setDeleteError(
        err instanceof Error
          ? err.message
          : "Không thể xóa file minh chứng này.",
      )
    } finally {
      setDeletingFileId(null)
    }
  }

  function openHideDialog(file: ContractEvidenceFileDto) {
    setHidingFile(file)
    setHideReason("")
    setHideError(null)
  }

  async function handleConfirmHide(event: FormEvent) {
    event.preventDefault()
    if (!hidingFile || !accessToken) return

    const validationError = validateHideEvidenceReason(hideReason)
    if (validationError) {
      setHideError(validationError)
      return
    }

    setSubmittingHide(true)
    setHideError(null)
    try {
      const updated = await hideContractEvidence(
        accessToken,
        contract.contractId,
        hidingFile.contractEvidenceFileId,
        hideReason.trim(),
      )
      setEvidence(updated)
      setHidingFile(null)
      setHideReason("")
    } catch (err) {
      setHideError(
        err instanceof Error ? err.message : "Không thể ẩn file minh chứng này.",
      )
    } finally {
      setSubmittingHide(false)
    }
  }

  return (
    <div
      className="mt-5 rounded-xl border border-slate-200 bg-slate-50/70 p-4"
      aria-label={`Minh chứng hợp đồng ${contract.contractId}`}
    >
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div>
          <h4 className="text-sm font-bold text-slate-900">
            Ảnh & tài liệu minh chứng hợp đồng
          </h4>
          <p className="mt-0.5 text-xs text-slate-600">
            Hỗ trợ ảnh JPG, PNG, WEBP (tối đa 5 MB) và file PDF (tối đa 10 MB),
            tối đa {MAX_EVIDENCE_FILES_PER_KIND} file cho mỗi mục.
          </p>
        </div>
      </div>

      {loading && (
        <p className="mt-3 text-xs text-slate-500">
          Đang tải danh sách minh chứng...
        </p>
      )}

      {!loading && loadError && (
        <p
          role="alert"
          className="mt-3 rounded-lg bg-rose-50 px-3 py-2 text-xs text-rose-800"
        >
          {loadError}
        </p>
      )}

      {!loading && !loadError && evidence && (
        <div className="mt-4 space-y-4">
          {EVIDENCE_SECTIONS.map((section) => {
            const items = filesByKind(section.kind)
            const canUpload = canUploadForKind(section.kind)
            const sectionError = sectionErrors[section.kind]
            const sectionNotice = sectionSuccess[section.kind]
            const activeProgress =
              uploadProgress?.kind === section.kind ? uploadProgress : null
            const isUploadingAny = uploadProgress !== null

            return (
              <section
                key={section.kind}
                aria-label={section.title}
                className="rounded-xl border border-slate-200 bg-white p-4"
              >
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <div className="flex flex-wrap items-center gap-2">
                      <h5 className="text-sm font-bold text-slate-900">
                        {section.title}
                      </h5>
                      <span className="rounded-full bg-slate-100 px-2 py-0.5 text-[11px] font-semibold text-slate-700">
                        {items.length}/{MAX_EVIDENCE_FILES_PER_KIND} file
                      </span>
                    </div>
                    <p className="mt-0.5 text-xs text-slate-600">
                      {section.description}
                    </p>
                    {section.kind === "MoveIn" && (
                      <p className="mt-1.5 rounded-lg bg-amber-50 px-2.5 py-1.5 text-xs font-medium text-amber-900">
                        {evidence.moveInUploadDeadline
                          ? `Bạn có thể thêm hoặc xóa ảnh nhận phòng đến hết ngày ${formatContractDate(evidence.moveInUploadDeadline)} (${evidence.moveInUploadDeadline}) — trong vòng 7 ngày kể từ ngày nhận phòng.`
                          : "Bạn có thể thêm hoặc xóa ảnh nhận phòng trong vòng 7 ngày kể từ ngày nhận phòng."}
                      </p>
                    )}
                  </div>

                  {canUpload && (
                    <div>
                      <label
                        className={`inline-flex cursor-pointer items-center gap-1.5 rounded-lg bg-[#087775] px-3 py-1.5 text-xs font-bold text-white hover:bg-teal-800 ${
                          isUploadingAny ? "pointer-events-none opacity-60" : ""
                        }`}
                      >
                        <span>Thêm ảnh</span>
                        <input
                          type="file"
                          multiple
                          disabled={isUploadingAny}
                          accept="image/jpeg,image/png,image/webp,application/pdf,.jpg,.jpeg,.png,.webp,.pdf"
                          aria-label={`Thêm ảnh ${section.title}`}
                          onChange={(e) =>
                            void handleSelectFiles(section.kind, e)
                          }
                          className="sr-only"
                        />
                      </label>
                    </div>
                  )}
                </div>

                {activeProgress && (
                  <div
                    role="status"
                    aria-live="polite"
                    className="mt-3 rounded-lg border border-teal-200 bg-teal-50/70 p-3 text-xs text-teal-950"
                  >
                    <div className="flex items-center justify-between gap-2 font-semibold">
                      <span>
                        Đang tải lên {activeProgress.current}/
                        {activeProgress.total}: {activeProgress.fileName}
                      </span>
                      <span>
                        {Math.round(
                          (activeProgress.current / activeProgress.total) * 100,
                        )}
                        %
                      </span>
                    </div>
                    <div
                      role="progressbar"
                      aria-valuenow={activeProgress.current}
                      aria-valuemin={0}
                      aria-valuemax={activeProgress.total}
                      className="mt-1.5 h-2 w-full overflow-hidden rounded-full bg-teal-100"
                    >
                      <div
                        className="h-full bg-[#087775] transition-all"
                        style={{
                          width: `${(activeProgress.current / activeProgress.total) * 100}%`,
                        }}
                      />
                    </div>
                  </div>
                )}

                {sectionError && (
                  <p
                    role="alert"
                    className="mt-3 rounded-lg bg-rose-50 px-3 py-2 text-xs font-medium text-rose-800"
                  >
                    {sectionError}
                  </p>
                )}

                {sectionNotice && !sectionError && (
                  <p className="mt-3 rounded-lg bg-emerald-50 px-3 py-2 text-xs font-medium text-emerald-800">
                    {sectionNotice}
                  </p>
                )}

                {items.length === 0 ? (
                  <p className="mt-3 text-xs text-slate-500">
                    {section.emptyText}
                  </p>
                ) : (
                  <div className="mt-3 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
                    {items.map((file) => {
                      const isPdf = isPdfEvidenceFile(
                        file.contentType,
                        file.fileName,
                      )
                      const uploadedTimestamp = formatEvidenceUploadedAt(
                        file.uploadedAtUtc,
                      )

                      return (
                        <div
                          key={file.contractEvidenceFileId}
                          className="flex flex-col justify-between rounded-xl border border-slate-200 bg-slate-50/60 p-3 text-xs"
                        >
                          <div>
                            {file.isHidden ? (
                              <div className="flex min-h-28 flex-col items-center justify-center rounded-lg border border-dashed border-slate-300 bg-slate-100 p-3 text-center text-slate-700">
                                <p className="font-semibold text-slate-800">
                                  Đã ẩn: {file.hiddenReason || "Không có lý do"}
                                </p>
                                <p className="mt-1 text-[11px] text-slate-500">
                                  {file.fileName}
                                </p>
                              </div>
                            ) : isPdf ? (
                              <a
                                href={file.url ?? "#"}
                                target="_blank"
                                rel="noopener noreferrer"
                                className="flex min-h-28 flex-col items-center justify-center rounded-lg border border-slate-200 bg-white p-3 text-center hover:border-[#087775]"
                              >
                                <span className="rounded bg-rose-100 px-2 py-0.5 text-[11px] font-bold text-rose-800">
                                  PDF
                                </span>
                                <span className="mt-2 line-clamp-2 font-semibold text-slate-900 underline decoration-slate-300">
                                  {file.fileName}
                                </span>
                                <span className="mt-1 text-[11px] text-slate-500">
                                  {formatEvidenceSize(file.sizeBytes)} · Bấm để
                                  mở tab mới
                                </span>
                              </a>
                            ) : (
                              <div className="overflow-hidden rounded-lg border border-slate-200 bg-white">
                                <button
                                  type="button"
                                  onClick={() => setPreviewImage(file)}
                                  className="block w-full text-left"
                                >
                                  <img
                                    src={file.url ?? ""}
                                    alt={file.fileName}
                                    className="h-32 w-full object-cover"
                                  />
                                </button>
                                <div className="flex items-center justify-between gap-2 border-t border-slate-100 px-2.5 py-1.5">
                                  <span className="truncate font-medium text-slate-800">
                                    {file.fileName}
                                  </span>
                                  {file.url && (
                                    <a
                                      href={file.url}
                                      target="_blank"
                                      rel="noopener noreferrer"
                                      className="shrink-0 text-[11px] font-semibold text-[#087775] hover:underline"
                                    >
                                      Mở tab mới
                                    </a>
                                  )}
                                </div>
                              </div>
                            )}

                            <p className="mt-2 text-[11px] text-slate-600">
                              {file.uploadedByName} · {uploadedTimestamp}
                            </p>
                          </div>

                          <div className="mt-2.5 flex flex-wrap items-center justify-end gap-2">
                            {!file.isHidden && file.canDelete && (
                              <button
                                type="button"
                                onClick={() => {
                                  setConfirmDeleteFile(file)
                                  setDeleteError(null)
                                }}
                                className="rounded-lg border border-rose-200 bg-rose-50 px-2.5 py-1 text-[11px] font-bold text-rose-700 hover:bg-rose-100"
                              >
                                Xóa
                              </button>
                            )}
                            {isAdmin && !file.isHidden && (
                              <button
                                type="button"
                                onClick={() => openHideDialog(file)}
                                className="rounded-lg border border-amber-300 bg-amber-50 px-2.5 py-1 text-[11px] font-bold text-amber-900 hover:bg-amber-100"
                              >
                                Ẩn
                              </button>
                            )}
                          </div>
                        </div>
                      )
                    })}
                  </div>
                )}
              </section>
            )
          })}
        </div>
      )}

      {confirmDeleteFile && (
        <div
          role="dialog"
          aria-modal="true"
          aria-label="Xác nhận xóa file minh chứng"
          className="mt-4 rounded-xl border border-rose-200 bg-white p-4 shadow-sm"
        >
          <h5 className="text-sm font-bold text-slate-900">
            Xác nhận xóa minh chứng
          </h5>
          <p className="mt-1 text-xs text-slate-700">
            Bạn có chắc chắn muốn xóa file{" "}
            <strong className="font-semibold text-slate-900">
              {confirmDeleteFile.fileName}
            </strong>{" "}
            không?
          </p>
          {deleteError && (
            <p
              role="alert"
              className="mt-2 rounded-lg bg-rose-50 px-3 py-2 text-xs text-rose-800"
            >
              {deleteError}
            </p>
          )}
          <div className="mt-3 flex justify-end gap-2">
            <button
              type="button"
              disabled={deletingFileId !== null}
              onClick={() => {
                setConfirmDeleteFile(null)
                setDeleteError(null)
              }}
              className="rounded-lg border border-slate-300 px-3 py-1.5 text-xs font-semibold text-slate-700"
            >
              Hủy
            </button>
            <button
              type="button"
              disabled={deletingFileId !== null}
              onClick={() => void handleConfirmDelete()}
              className="rounded-lg bg-rose-600 px-3 py-1.5 text-xs font-bold text-white disabled:opacity-50"
            >
              {deletingFileId !== null ? "Đang xóa..." : "Xác nhận xóa"}
            </button>
          </div>
        </div>
      )}

      {hidingFile && (
        <form
          noValidate
          onSubmit={(e) => void handleConfirmHide(e)}
          aria-label="Biểu mẫu ẩn file minh chứng"
          className="mt-4 rounded-xl border border-amber-200 bg-white p-4 shadow-sm"
        >
          <h5 className="text-sm font-bold text-slate-900">
            Ẩn minh chứng: {hidingFile.fileName}
          </h5>
          <p className="mt-1 text-xs text-slate-600">
            Nhập lý do ẩn file minh chứng này (bắt buộc, tối đa{" "}
            {MAX_HIDE_EVIDENCE_REASON_LENGTH} ký tự).
          </p>
          <label className="mt-2 block text-xs font-semibold text-slate-700">
            Lý do ẩn
            <textarea
              rows={2}
              value={hideReason}
              maxLength={MAX_HIDE_EVIDENCE_REASON_LENGTH}
              onChange={(e) => {
                setHideReason(e.target.value)
                setHideError(null)
              }}
              placeholder="Ví dụ: Ảnh tải nhầm hợp đồng hoặc chứa thông tin không phù hợp"
              className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-xs text-slate-900"
            />
          </label>
          {hideError && (
            <p
              role="alert"
              className="mt-2 rounded-lg bg-rose-50 px-3 py-2 text-xs text-rose-800"
            >
              {hideError}
            </p>
          )}
          <div className="mt-3 flex justify-end gap-2">
            <button
              type="button"
              disabled={submittingHide}
              onClick={() => {
                setHidingFile(null)
                setHideReason("")
                setHideError(null)
              }}
              className="rounded-lg border border-slate-300 px-3 py-1.5 text-xs font-semibold text-slate-700"
            >
              Hủy
            </button>
            <button
              type="submit"
              disabled={submittingHide}
              className="rounded-lg bg-amber-600 px-3 py-1.5 text-xs font-bold text-white disabled:opacity-50"
            >
              {submittingHide ? "Đang ẩn..." : "Xác nhận ẩn"}
            </button>
          </div>
        </form>
      )}

      {previewImage && previewImage.url && (
        <div
          role="dialog"
          aria-modal="true"
          aria-label={`Xem ảnh ${previewImage.fileName}`}
          className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/75 p-4"
        >
          <div className="max-h-[90vh] max-w-3xl overflow-auto rounded-2xl bg-white p-4 shadow-xl">
            <div className="flex items-center justify-between gap-4 pb-3">
              <p className="text-sm font-bold text-slate-900">
                {previewImage.fileName}
              </p>
              <div className="flex items-center gap-2">
                <a
                  href={previewImage.url}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="rounded-lg border border-slate-300 px-3 py-1 text-xs font-semibold text-slate-700"
                >
                  Mở tab mới
                </a>
                <button
                  type="button"
                  onClick={() => setPreviewImage(null)}
                  className="rounded-lg bg-slate-900 px-3 py-1 text-xs font-bold text-white"
                >
                  Đóng
                </button>
              </div>
            </div>
            <img
              src={previewImage.url}
              alt={previewImage.fileName}
              className="max-h-[75vh] w-auto rounded-lg object-contain"
            />
          </div>
        </div>
      )}
    </div>
  )
}
