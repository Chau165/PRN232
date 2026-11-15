import { isContractDate } from "@/utils/managementContracts"
import { diffIsoDays } from "@/utils/moveOutNoticeValidation"
import { formatVnd } from "@/utils/publicFormatting"

export const MAX_AUDIT_LOG_RANGE_DAYS = 366
export const DEFAULT_AUDIT_LOG_PAGE_SIZE = 50
export const MAX_AUDIT_LOG_PAGE_SIZE = 100

export const AUDIT_ENTITY_TYPE_OPTIONS: { value: string; label: string }[] = [
  { value: "", label: "Tất cả đối tượng" },
  { value: "Bookings", label: "Đặt phòng & cọc (Bookings)" },
  { value: "Contracts", label: "Hợp đồng (Contracts)" },
  { value: "DepositSettlements", label: "Quyết toán cọc (DepositSettlements)" },
  { value: "ContractOccupants", label: "Người ở cùng (ContractOccupants)" },
  { value: "ContractTransfers", label: "Chuyển hợp đồng (ContractTransfers)" },
  { value: "ContractHandovers", label: "Bàn giao thiết bị (ContractHandovers)" },
  { value: "ContractEvidence", label: "Minh chứng HĐ (ContractEvidence)" },
  { value: "Invoices", label: "Hóa đơn (Invoices)" },
  { value: "Payments", label: "Thanh toán (Payments)" },
  { value: "Rooms", label: "Phòng (Rooms)" },
  { value: "Properties", label: "Khu trọ (Properties)" },
  { value: "Users", label: "Người dùng (Users)" },
  { value: "MaintenanceRequests", label: "Bảo trì (MaintenanceRequests)" },
  { value: "ViewingRequests", label: "Lịch xem phòng (ViewingRequests)" },
]

const AUDIT_ACTION_LABELS: Record<string, string> = {
  "Auth.Login": "Đăng nhập",
  "Auth.Register": "Đăng ký tài khoản",
  "Bookings.Create": "Tạo đặt phòng / đặt cọc",
  "Bookings.Cancel": "Hủy đặt cọc",
  "Bookings.ConfirmRefund": "Xác nhận đã hoàn cọc booking",
  "Bookings.ReleaseForfeit": "Nhả phòng (khách mất cọc)",
  "Contracts.Create": "Tạo hợp đồng",
  "Contracts.Extend": "Gia hạn hợp đồng",
  "Contracts.Terminate": "Xác nhận trả phòng / kết thúc HĐ",
  "Contracts.MarkRoomReady": "Đánh dấu phòng sẵn sàng",
  "Contracts.SaveMeterHandover": "Lưu chỉ số điện nước bàn giao",
  "Contracts.UpsertMoveOutNotice": "Báo trả phòng",
  "Contracts.CancelMoveOutNotice": "Hủy báo trả phòng",
  "ContractHandovers.SaveMoveIn": "Lưu biên bản nhận phòng",
  "ContractHandovers.SaveMoveOut": "Lưu biên bản trả phòng",
  "ContractEvidence.Upload": "Tải lên minh chứng hợp đồng",
  "ContractEvidence.Delete": "Xóa minh chứng hợp đồng",
  "ContractEvidence.Hide": "Ẩn minh chứng hợp đồng",
  "DepositSettlements.Settle": "Quyết toán cọc",
  "DepositSettlements.RefundConfirmed": "Xác nhận đã hoàn cọc quyết toán",
  "DepositSettlements.DueCollected": "Xác nhận đã thu tiền bồi thường",
  "ContractOccupants.Add": "Thêm người ở cùng",
  "ContractOccupants.MoveOut": "Ghi ngày dọn ra người ở cùng",
  "ContractOccupants.RevealIdentity": "Xem số CCCD người ở cùng",
  "ContractTransfers.Create": "Tạo đề nghị chuyển người đứng tên",
  "ContractTransfers.Confirm": "Xác nhận chuyển người đứng tên",
  "ContractTransfers.Cancel": "Hủy đề nghị chuyển người đứng tên",
  "ContractTransfers.Complete": "Hoàn tất chuyển người đứng tên",
  "Invoices.Create": "Phát hành hóa đơn",
  "Invoices.RecordCashPayment": "Ghi nhận thanh toán tiền mặt",
  "Properties.Create": "Tạo khu trọ",
  "Properties.Update": "Cập nhật khu trọ",
  "Rooms.Create": "Tạo phòng",
  "Rooms.Update": "Cập nhật phòng",
  "Rooms.UpdatePrice": "Đổi giá thuê phòng",
  "Users.Create": "Tạo tài khoản người dùng",
  "Users.UpdateRole": "Thay đổi vai trò người dùng",
  "Users.UpdateStatus": "Cập nhật trạng thái người dùng",
}

export function formatAuditActionLabel(action: string): string {
  return AUDIT_ACTION_LABELS[action] ?? action
}

export function validateAuditLogFilter(params: {
  userId?: string
  from?: string
  to?: string
  pageSize?: number
}): string | null {
  const userId = params.userId?.trim() ?? ""
  if (userId && !/^\d+$/.test(userId)) {
    return "Mã người dùng phải là số nguyên dương."
  }

  const from = params.from?.trim() ?? ""
  const to = params.to?.trim() ?? ""

  if (from && !isContractDate(from)) {
    return "Ngày bắt đầu không hợp lệ."
  }
  if (to && !isContractDate(to)) {
    return "Ngày kết thúc không hợp lệ."
  }
  if (from && to) {
    if (to < from) {
      return "Ngày kết thúc không được nhỏ hơn ngày bắt đầu."
    }
    const span = diffIsoDays(from, to) + 1
    if (span > MAX_AUDIT_LOG_RANGE_DAYS) {
      return "Khoảng thời gian tra cứu tối đa 366 ngày."
    }
  }

  if (
    params.pageSize !== undefined &&
    (params.pageSize < 1 || params.pageSize > MAX_AUDIT_LOG_PAGE_SIZE)
  ) {
    return "Số dòng mỗi trang phải từ 1 đến 100."
  }

  return null
}

export function formatAuditTimeVietnam(utcIso: string): string {
  if (!utcIso) return ""
  const date = new Date(utcIso)
  if (Number.isNaN(date.getTime())) return utcIso
  return new Intl.DateTimeFormat("vi-VN", {
    timeZone: "Asia/Ho_Chi_Minh",
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    hour12: false,
  }).format(date)
}

export function formatAuditJsonPayload(raw: string | null | undefined): string | null {
  if (!raw) return null
  try {
    return JSON.stringify(JSON.parse(raw), null, 2)
  } catch {
    return raw
  }
}

export const AUDIT_FIELD_LABELS: Record<string, string> = {
  roomCode: "Mã phòng",
  status: "Trạng thái",
  depositAmount: "Tiền cọc",
  agreedMonthlyRent: "Tiền thuê thỏa thuận",
  moveInDate: "Ngày dọn vào",
  occupantCount: "Số người ở",
  cancellationReason: "Lý do hủy",
  depositReturnedAtUtc: "Thời điểm hoàn cọc",
  bookingId: "Mã đặt phòng",
  customerName: "Khách thuê",
  startDate: "Ngày bắt đầu",
  endDate: "Ngày kết thúc",
  monthlyRent: "Tiền phòng/tháng",
  securityDepositAmount: "Tiền cọc giữ hợp đồng",
  outstandingDebt: "Công nợ chưa thu",
  roomReadyForRental: "Phòng sẵn sàng cho thuê",
  plannedMoveOutDate: "Ngày dự kiến trả phòng",
  noticeDays: "Số ngày báo trước",
  forfeitsDeposit: "Mất tiền cọc",
  contractId: "Mã hợp đồng",
  holderId: "Mã người đứng tên",
  effectiveDate: "Ngày hiệu lực",
  oldContractEndDate: "Ngày kết thúc HĐ cũ",
  depositCarriedOver: "Cọc chuyển tiếp",
  damageCharges: "Phí bồi thường hư hỏng",
  retainedForDebt: "Cọc giữ trừ công nợ",
  forfeitedAmount: "Tiền cọc bị mất",
  refundAmount: "Tiền hoàn lại",
  amountDue: "Tiền khách nộp thêm",
  earlyWithoutNotice: "Trả sớm không báo đủ ngày",
  refundConfirmedAtUtc: "Thời điểm xác nhận hoàn cọc",
  dueCollectedAtUtc: "Thời điểm xác nhận đã thu",
  invoiceMonth: "Kỳ hóa đơn",
  dueDate: "Hạn thanh toán",
  total: "Tổng tiền",
  lines: "Chi tiết dòng hóa đơn",
  paymentMethod: "Phương thức thanh toán",
  serviceName: "Dịch vụ",
  price: "Đơn giá",
  unitCode: "Đơn vị tính",
  billingBasis: "Cách tính phí",
  effectiveFrom: "Hiệu lực từ",
  effectiveTo: "Hiệu lực đến",
  roleId: "Vai trò (Role ID)",
  isActive: "Đang hoạt động",
  propertyId: "Mã khu trọ",
  propertyName: "Khu trọ",
  roomTypeName: "Loại phòng",
  fullName: "Họ và tên",
  email: "Email",
  phoneNumber: "Số điện thoại",
}

const CURRENCY_AUDIT_FIELDS = new Set([
  "depositAmount",
  "agreedMonthlyRent",
  "monthlyRent",
  "securityDepositAmount",
  "outstandingDebt",
  "depositCarriedOver",
  "damageCharges",
  "retainedForDebt",
  "forfeitedAmount",
  "refundAmount",
  "amountDue",
  "total",
  "price",
])

export type AuditDiffRow = {
  key: string
  label: string
  before: string
  after: string
  changed: boolean
}

export type AuditDiffResult = {
  rows: AuditDiffRow[]
  fallbackOld: string | null
  fallbackNew: string | null
}

function isPlainObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value)
}

function parseJsonObject(
  raw: string | null | undefined,
): { ok: true; obj: Record<string, unknown> | null } | { ok: false } {
  if (!raw || !raw.trim()) return { ok: true, obj: null }
  try {
    const parsed: unknown = JSON.parse(raw)
    if (parsed === null) return { ok: true, obj: null }
    if (isPlainObject(parsed)) return { ok: true, obj: parsed }
    return { ok: false }
  } catch {
    return { ok: false }
  }
}

function formatAuditFieldValue(key: string, value: unknown): string {
  if (value === undefined || value === null) return "—"
  if (typeof value === "boolean") return value ? "Có" : "Không"
  if (typeof value === "number") {
    return CURRENCY_AUDIT_FIELDS.has(key)
      ? formatVnd(value)
      : value.toLocaleString("vi-VN")
  }
  if (typeof value === "string") {
    if (
      (key.endsWith("AtUtc") || /^\d{4}-\d{2}-\d{2}T.*Z$/.test(value)) &&
      !Number.isNaN(Date.parse(value))
    ) {
      return formatAuditTimeVietnam(value)
    }
    return value || "—"
  }
  try {
    return JSON.stringify(value)
  } catch {
    return String(value)
  }
}

function normalizedForCompare(value: unknown): string {
  if (value === undefined || value === null) return "__EMPTY__"
  try {
    return JSON.stringify(value)
  } catch {
    return String(value)
  }
}

export function buildAuditDiffRows(
  oldRaw: string | null | undefined,
  newRaw: string | null | undefined,
): AuditDiffResult {
  const parsedOld = parseJsonObject(oldRaw)
  const parsedNew = parseJsonObject(newRaw)

  if (!parsedOld.ok || !parsedNew.ok) {
    return {
      rows: [],
      fallbackOld: formatAuditJsonPayload(oldRaw),
      fallbackNew: formatAuditJsonPayload(newRaw),
    }
  }

  const oldObj = parsedOld.obj
  const newObj = parsedNew.obj
  if (!oldObj && !newObj) {
    return { rows: [], fallbackOld: null, fallbackNew: null }
  }

  const keys: string[] = []
  if (oldObj) {
    for (const key of Object.keys(oldObj)) {
      keys.push(key)
    }
  }
  if (newObj) {
    for (const key of Object.keys(newObj)) {
      if (!keys.includes(key)) keys.push(key)
    }
  }

  const rows: AuditDiffRow[] = keys.map((key) => {
    const oldVal = oldObj && key in oldObj ? oldObj[key] : undefined
    const newVal = newObj && key in newObj ? newObj[key] : undefined
    return {
      key,
      label: AUDIT_FIELD_LABELS[key] ?? key,
      before: formatAuditFieldValue(key, oldVal),
      after: formatAuditFieldValue(key, newVal),
      changed: normalizedForCompare(oldVal) !== normalizedForCompare(newVal),
    }
  })

  return { rows, fallbackOld: null, fallbackNew: null }
}
