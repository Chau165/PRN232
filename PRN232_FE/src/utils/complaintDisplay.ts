import type { ComplaintStatus } from "@/api/invoiceComplaintsApi"

export const complaintStatuses: Array<{ value: ComplaintStatus; label: string }> = [
  { value: "New", label: "Mới" },
  { value: "InProgress", label: "Đang xử lý" },
  { value: "Resolved", label: "Đã xử lý" },
]

export function complaintStatusDisplay(status: ComplaintStatus) {
  const label = complaintStatuses.find((item) => item.value === status)?.label ?? status
  return { label, tone: status === "Resolved" ? "green" as const : "amber" as const }
}

export function formatComplaintDate(value: string) {
  return new Intl.DateTimeFormat("vi-VN").format(new Date(value))
}
