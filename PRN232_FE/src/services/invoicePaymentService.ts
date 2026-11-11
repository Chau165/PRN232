import { apiRequest } from "@/api/apiClient"
import type { InvoicePaymentLinkDto, PaymentRequestStatusDto } from "@/types/invoicePayment"

export function createInvoicePaymentLink(accessToken: string, invoiceId: number) {
  return apiRequest<InvoicePaymentLinkDto>(`/invoices/${invoiceId}/payment-link`, {
    accessToken, method: "POST", cache: "no-store", requireJson: true,
  })
}

export function getPaymentRequest(accessToken: string, requestId: number, signal?: AbortSignal) {
  return apiRequest<PaymentRequestStatusDto>(`/payment-requests/${requestId}`, {
    accessToken, signal, cache: "no-store", requireJson: true,
  })
}
