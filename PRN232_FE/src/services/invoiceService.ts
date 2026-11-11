import { apiRequest } from "@/api/apiClient"
import type { InvoiceDto, InvoiceRequest } from "@/types/invoice"

export function getInvoices(accessToken: string, signal?: AbortSignal) {
  return apiRequest<InvoiceDto[]>("/invoices", { accessToken, signal, cache: "no-store" })
}

export function previewInvoice(accessToken: string, request: InvoiceRequest) {
  return apiRequest<InvoiceDto>("/invoices/preview", { accessToken, method: "POST", body: JSON.stringify(request) })
}

export function createInvoice(accessToken: string, request: InvoiceRequest) {
  return apiRequest<InvoiceDto>("/invoices", { accessToken, method: "POST", body: JSON.stringify(request) })
}

export function recordCashPayment(accessToken: string, invoiceId: number) {
  return apiRequest<InvoiceDto>(`/invoices/${invoiceId}/record-cash`, { accessToken, method: "POST" })
}
