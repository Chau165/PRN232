import { getProperties } from "@/services/propertyService"
import { getManagementRooms } from "@/services/roomService"
import { getInvoices } from "@/services/invoiceService"
import { getMaintenanceRequests } from "@/services/maintenanceService"

export async function getManagementDashboard(accessToken: string, signal?: AbortSignal) {
  // Reuse the existing authenticated, backend-scoped APIs. Never fall back to demo data.
  const [properties, rooms, invoices, maintenance] = await Promise.all([
    getProperties(accessToken, signal),
    getManagementRooms(accessToken, {}, signal),
    getInvoices(accessToken, signal),
    getMaintenanceRequests(accessToken, signal),
  ])
  const propertyIds = new Set(properties.map((property) => property.propertyId))
  const scopedRooms = rooms.filter((room) => propertyIds.has(room.propertyId))
  const roomsById = new Map(scopedRooms.map((room) => [room.roomId, room]))
  const roomCounts = new Map<number, { totalRooms: number; occupiedRooms: number; availableRooms: number }>()
  for (const room of scopedRooms) {
    const counts = roomCounts.get(room.propertyId) ?? { totalRooms: 0, occupiedRooms: 0, availableRooms: 0 }
    counts.totalRooms += 1
    if (room.status === "Occupied") counts.occupiedRooms += 1
    if (room.status === "Available") counts.availableRooms += 1
    roomCounts.set(room.propertyId, counts)
  }
  return {
    totalRooms: scopedRooms.length,
    occupiedRooms: scopedRooms.filter((room) => room.status === "Occupied").length,
    availableRooms: scopedRooms.filter((room) => room.status === "Available").length,
    maintenanceRooms: scopedRooms.filter((room) => room.status === "Maintenance").length,
    reservedRooms: scopedRooms.filter((room) => room.status === "Reserved").length,
    unpaidInvoices: invoices.filter((invoice) => invoice.status === "Unpaid" || invoice.status === "Overdue").length,
    properties: properties.map((property) => ({
      ...property,
      ...(roomCounts.get(property.propertyId) ?? { totalRooms: 0, occupiedRooms: 0, availableRooms: 0 }),
    })),
    attention: maintenance
      .filter((request) => request.status === "Pending" || request.status === "Approved")
      .sort((first, second) => second.requestId - first.requestId)
      .slice(0, 3)
      .map((request) => ({
        ...request,
        propertyName: roomsById.get(request.roomId)?.propertyName ?? "Khu trọ chưa xác định",
      })),
  }
}

export type ManagementDashboardData = Awaited<ReturnType<typeof getManagementDashboard>>
