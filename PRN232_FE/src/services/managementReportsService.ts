import { getManagementRooms } from "@/services/roomService"
import { getInvoices } from "@/services/invoiceService"
import { getMaintenanceRequests } from "@/services/maintenanceService"
import { getContracts } from "@/services/contractService"
import { isContractDate } from "@/utils/managementContracts"
import { formatMonth } from "@/utils/invoiceDisplay"

export async function getManagementReports(accessToken: string, signal?: AbortSignal, now = new Date()) {
  const [rooms, invoices, maintenance, contracts] = await Promise.all([
    getManagementRooms(accessToken, {}, signal),
    getInvoices(accessToken, signal),
    getMaintenanceRequests(accessToken, signal),
    getContracts(accessToken, signal),
  ])
  const parts = new Intl.DateTimeFormat("en-CA", {
    timeZone: "Asia/Ho_Chi_Minh", year: "numeric", month: "2-digit", day: "2-digit",
  }).formatToParts(now)
  const part = (type: string) => parts.find((item) => item.type === type)!.value
  const today = `${part("year")}-${part("month")}-${part("day")}`
  const year = Number(part("year"))
  const monthIndex = Number(part("month")) - 1
  const months = Array.from({ length: 7 }, (_, index) => {
    const key = new Date(Date.UTC(year, monthIndex - 6 + index, 1)).toISOString().slice(0, 7)
    return { key, label: formatMonth(key), revenue: 0 }
  })
  const monthsByKey = new Map(months.map((month) => [month.key, month]))
  for (const invoice of invoices) {
    if (invoice.invoiceId <= 0 || invoice.status === "Preview" || !isContractDate(invoice.invoiceMonth)) continue
    const month = monthsByKey.get(invoice.invoiceMonth.slice(0, 7))
    // BE supplies successful invoice payments, excluding prepaid credit lines.
    // Group by billing period, not payment date (the current API omits that date).
    const received = invoice.amountPaid ?? 0
    if (month && Number.isFinite(received) && received > 0) month.revenue += received
  }
  const todayUtc = Date.parse(`${today}T00:00:00Z`)
  const expiringContracts = contracts
    .filter((contract) => contract.status === "Active" && isContractDate(contract.startDate) &&
      isContractDate(contract.endDate) && contract.startDate <= today)
    .map((contract) => ({
      ...contract,
      daysRemaining: Math.round((Date.parse(`${contract.endDate}T00:00:00Z`) - todayUtc) / 86_400_000),
    }))
    .filter((contract) => contract.daysRemaining >= 0 && contract.daysRemaining <= 30)
    .sort((first, second) => first.daysRemaining - second.daysRemaining || first.contractId - second.contractId)
  const occupiedRooms = rooms.filter((room) => room.status === "Occupied").length
  return {
    period: formatMonth(today),
    monthlyRevenue: months[months.length - 1].revenue,
    months,
    totalRooms: rooms.length,
    occupiedRooms,
    occupancyRate: rooms.length > 0 ? occupiedRooms / rooms.length * 100 : 0,
    unpaidInvoices: invoices.filter((invoice) => invoice.status === "Unpaid" || invoice.status === "Overdue").length,
    completedMaintenance: maintenance.filter((request) => request.status === "Completed").length,
    expiringContracts,
  }
}

export type ManagementReportsData = Awaited<ReturnType<typeof getManagementReports>>
