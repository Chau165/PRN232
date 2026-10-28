import { apiRequest } from "@/api/apiClient"

export interface InvoiceLineDto {
  itemType: string
  description: string
  quantity: number | null
  unitPrice: number | null
  unitCode: string | null
  amount: number
}

export interface InvoiceDto {
  // Bản xem trước có invoiceId = 0 và status = "Preview".
  invoiceId: number
  contractId: number
  roomCode: string
  propertyName: string
  customerName: string
  invoiceMonth: string
  dueDate: string
  status: string
  total: number
  lines: InvoiceLineDto[]
}

export interface InvoiceRequest {
  contractId: number
  invoiceMonth: string
  dueDate: string
}

export function getInvoices(accessToken: string, signal?: AbortSignal) {
  return apiRequest<InvoiceDto[]>("/invoices", { accessToken, signal, cache: "no-store" })
}

export function previewInvoice(accessToken: string, request: InvoiceRequest) {
  return apiRequest<InvoiceDto>("/invoices/preview", { accessToken, method: "POST", body: JSON.stringify(request) })
}

export function createInvoice(accessToken: string, request: InvoiceRequest) {
  return apiRequest<InvoiceDto>("/invoices", { accessToken, method: "POST", body: JSON.stringify(request) })
}
