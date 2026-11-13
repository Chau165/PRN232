import { apiRequest } from "@/api/apiClient"

export interface RevenueReport {
  totalCollected: number
  paymentCount: number
  unknownDateAmount: number
  unknownDatePaymentCount: number
  byProperty: { propertyId: number; propertyName: string; collectedAmount: number; paymentCount: number; unknownDateAmount: number; unknownDatePaymentCount: number }[]
}
export interface ReceivablesReport {
  asOfDate: string
  outstandingAmount: number
  outstandingInvoiceCount: number
  overdueAmount: number
  overdueInvoiceCount: number
}
export function getRevenueReport(token: string, month: string, signal?: AbortSignal) {
  const query = new URLSearchParams({ fromMonth: `${month}-01`, toMonth: `${month}-01` })
  return apiRequest<RevenueReport>(`/reports/revenue?${query}`, { accessToken: token, signal, cache: "no-store" })
}
export function getReceivablesReport(token: string, signal?: AbortSignal) {
  return apiRequest<ReceivablesReport>("/reports/receivables", { accessToken: token, signal, cache: "no-store" })
}
export interface DepositTotals {
  collectedAmount: number
  collectionCount: number
  refundedAmount: number
  refundCount: number
  netCashFlow: number
}
export interface DepositReport {
  fromMonth: string
  toMonth: string
  totals: DepositTotals
  unknownDate: DepositTotals
  byProperty: { propertyId: number; propertyName: string; totals: DepositTotals; unknownDate: DepositTotals }[]
}
export function getDepositReport(token: string, month: string, signal?: AbortSignal) {
  const query = new URLSearchParams({ fromMonth: `${month}-01`, toMonth: `${month}-01` })
  return apiRequest<DepositReport>(`/reports/booking-deposits?${query}`, { accessToken: token, signal, cache: "no-store" })
}
