import { apiRequest } from "@/api/apiClient"

export type ComplaintStatus = "New" | "InProgress" | "Resolved"

// Loại phí chung, không gắn với một dòng cụ thể trên hoá đơn (khớp ComplaintStatuses.OtherFeeType ở BE).
export const OTHER_FEE_TYPE = "Khác"

export interface InvoiceComplaintDto {
  complaintId: number
  invoiceId: number
  invoiceMonth: string
  roomCode: string
  propertyName: string
  customerId: number
  customerName: string
  // Mô tả của dòng hoá đơn bị khiếu nại, hoặc "Khác".
  feeType: string
  content: string
  status: ComplaintStatus
  createdAt: string
}

export function getInvoiceComplaints(accessToken: string, signal?: AbortSignal) {
  return apiRequest<InvoiceComplaintDto[]>("/invoice-complaints", { accessToken, signal, cache: "no-store" })
}

export function createInvoiceComplaint(
  accessToken: string,
  request: { invoiceId: number; feeType: string; content: string },
) {
  return apiRequest<InvoiceComplaintDto>("/invoice-complaints", {
    accessToken,
    method: "POST",
    body: JSON.stringify(request),
  })
}

export function updateInvoiceComplaintStatus(accessToken: string, complaintId: number, status: ComplaintStatus) {
  return apiRequest<InvoiceComplaintDto>(`/invoice-complaints/${complaintId}`, {
    accessToken,
    method: "PATCH",
    body: JSON.stringify({ status }),
  })
}
