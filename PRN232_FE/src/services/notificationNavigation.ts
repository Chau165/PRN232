import { routes } from "@/constants/routes"

export function notificationPath(type: string, role: string | undefined, relatedEntityId?: number | null): string {
  const customer = role === "CUSTOMER"
  switch (type) {
    case "Maintenance": return customer ? `${routes.myRoom}?tab=maintenance` : routes.managementMaintenance
    case "Viewing": {
      const base = customer ? routes.myBookings : routes.managementViewings
      return Number.isSafeInteger(relatedEntityId) && Number(relatedEntityId) > 0
        ? `${base}?viewingId=${Number(relatedEntityId)}`
        : base
    }
    case "Booking": return customer ? routes.myBookings : routes.managementBookings
    case "Contract": return customer ? routes.myContracts : routes.managementContracts
    // Khiếu nại hiển thị cùng trang hoá đơn (MyBillsPage / ManagementInvoicesPage).
    case "Invoice":
    case "Complaint": return customer ? routes.myBills : routes.managementInvoices
    default: return routes.notifications
  }
}
