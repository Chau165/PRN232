export const MAX_IMAGE_EVIDENCE_BYTES = 5 * 1024 * 1024
export const MAX_PDF_EVIDENCE_BYTES = 10 * 1024 * 1024
export const MAX_EVIDENCE_FILES_PER_KIND = 20
export const MAX_HIDE_EVIDENCE_REASON_LENGTH = 500

const ALLOWED_IMAGE_MIME_TYPES = new Set([
  "image/jpeg",
  "image/png",
  "image/webp",
])

const ALLOWED_IMAGE_EXTENSIONS = new Set([".jpg", ".jpeg", ".png", ".webp"])
const PDF_MIME_TYPE = "application/pdf"
const PDF_EXTENSION = ".pdf"

function getLowerExtension(fileName: string): string {
  const dotIndex = fileName.lastIndexOf(".")
  if (dotIndex < 0) return ""
  return fileName.slice(dotIndex).toLowerCase()
}

export function isPdfEvidenceFile(
  contentType: string | null | undefined,
  fileName?: string | null,
): boolean {
  const normalizedType = (contentType ?? "").trim().toLowerCase()
  if (normalizedType === PDF_MIME_TYPE) return true
  if (!normalizedType && fileName) {
    return getLowerExtension(fileName) === PDF_EXTENSION
  }
  return false
}

export function validateContractEvidenceFile(file: {
  name: string
  type: string
  size: number
}): string | null {
  const mime = (file.type ?? "").trim().toLowerCase()
  const ext = getLowerExtension(file.name ?? "")

  const isImageByMime = ALLOWED_IMAGE_MIME_TYPES.has(mime)
  const isPdfByMime = mime === PDF_MIME_TYPE
  const isImageByExt = ALLOWED_IMAGE_EXTENSIONS.has(ext)
  const isPdfByExt = ext === PDF_EXTENSION

  const isImage = isImageByMime || (!mime && isImageByExt)
  const isPdf = isPdfByMime || (!mime && isPdfByExt)

  if (!isImage && !isPdf) {
    return "Chỉ nhận ảnh JPG, PNG, WEBP hoặc file PDF hợp lệ."
  }

  // Nếu có cả đuôi file và MIME nhưng mâu thuẫn nhau (ví dụ .pdf nhưng image/png hoặc ngược lại)
  if (ext && ((isImage && isPdfByExt) || (isPdf && isImageByExt))) {
    return "Chỉ nhận ảnh JPG, PNG, WEBP hoặc file PDF hợp lệ."
  }

  if (file.size <= 0) {
    return "Chỉ nhận ảnh JPG, PNG, WEBP hoặc file PDF hợp lệ."
  }

  if (isImage && file.size > MAX_IMAGE_EVIDENCE_BYTES) {
    return "Ảnh tối đa 5 MB, PDF tối đa 10 MB."
  }

  if (isPdf && file.size > MAX_PDF_EVIDENCE_BYTES) {
    return "Ảnh tối đa 5 MB, PDF tối đa 10 MB."
  }

  return null
}

export function validateEvidenceBatchCount(
  existingCount: number,
  addingCount: number,
): string | null {
  if (existingCount + addingCount > MAX_EVIDENCE_FILES_PER_KIND) {
    return "Mỗi loại minh chứng tối đa 20 file."
  }
  return null
}

export function validateHideEvidenceReason(reason: string): string | null {
  const trimmed = reason.trim()
  if (!trimmed) {
    return "Vui lòng nhập lý do ẩn minh chứng."
  }
  if (trimmed.length > MAX_HIDE_EVIDENCE_REASON_LENGTH) {
    return "Lý do ẩn minh chứng không được vượt quá 500 ký tự."
  }
  return null
}

export function formatEvidenceSize(sizeBytes: number): string {
  if (!Number.isFinite(sizeBytes) || sizeBytes <= 0) return "0 KB"
  if (sizeBytes >= 1024 * 1024) {
    return `${(sizeBytes / (1024 * 1024)).toFixed(1)} MB`
  }
  return `${Math.max(1, Math.round(sizeBytes / 1024))} KB`
}

export function formatEvidenceUploadedAt(value: string): string {
  if (!value) return ""
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return new Intl.DateTimeFormat("vi-VN", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  }).format(date)
}
