export type ComplaintStatus = "New" | "InProgress" | "Resolved"

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
