import type { InvoiceDto, InvoiceLineDto } from "@/types/invoice"
import { formatVnd } from "@/utils/publicFormatting"

const unitLabels: Record<string, string> = { kwh: "kWh", m3: "m³", room_month: "phòng" }

// "2026-10-01" -> "10/2026"
export function formatMonth(month: string) {
  const [year, monthNumber] = month.split("-")
  return `${monthNumber}/${year}`
}

// Hoá đơn mới luôn lưu Unpaid; quá hạn được suy ra từ hạn thanh toán (dữ liệu cũ có thể lưu sẵn Overdue).
export function invoiceStatusDisplay(invoice: Pick<InvoiceDto, "status" | "dueDate">, today: string) {
  if (invoice.status === "Paid") return { label: "Đã thanh toán", tone: "green" as const }
  if (invoice.status === "Overdue" || invoice.dueDate < today) return { label: "Quá hạn", tone: "rose" as const }
  return { label: "Chưa thanh toán", tone: "amber" as const }
}

export function invoiceLineLabel(line: InvoiceLineDto) {
  const usage = line.quantity !== null && line.unitPrice !== null && line.unitCode
    ? ` (${line.quantity.toLocaleString("vi-VN")} ${unitLabels[line.unitCode] ?? line.unitCode} × ${formatVnd(line.unitPrice)})`
    : ""
  if (line.itemType === "Rent") return "Tiền phòng"
  if (line.itemType === "Electricity") return `Điện${usage}`
  if (line.itemType === "Water") return `Nước${usage}`
  return line.description
}

// Hạn mặc định là ngày 10 của tháng kế tiếp, nhưng không sớm hơn hôm nay.
export function defaultDueDate(month: string, today: string) {
  const [year, monthNumber] = month.split("-").map(Number)
  const next = new Date(Date.UTC(year, monthNumber, 10))
  const candidate = next.toISOString().slice(0, 10)
  return candidate < today ? today : candidate
}
