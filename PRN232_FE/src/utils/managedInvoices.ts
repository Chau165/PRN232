import { managedInvoices } from "@/data/management"
import type { ManagedInvoice } from "@/types/management"

// Hoá đơn mock chỉ còn phục vụ trang tổng quan và báo cáo (chưa nối API);
// trang Hoá đơn đã dùng /api/invoices.
export function getManagedInvoices(): ManagedInvoice[] {
  return managedInvoices
}
