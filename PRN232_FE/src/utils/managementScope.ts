import {
  areas,
  equipment,
  managedBookings,
  managedContracts,
  managedMaintenance,
  managementProperties,
  managementRooms,
  tenants,
} from "@/data/management"
import { getAccessiblePropertyIds } from "@/utils/managementAuth"
import type { CurrentUser } from "@/types/user"
import { getManagedInvoices } from "@/utils/managedInvoices"

export function getManagementScope(user: CurrentUser) {
  const propertyIds = getAccessiblePropertyIds(
    user,
    managementProperties.map((property) => property.id),
  )
  return {
    propertyIds,
    properties: managementProperties.filter((property) =>
      propertyIds.includes(property.id),
    ),
    rooms: managementRooms.filter((room) =>
      propertyIds.includes(room.propertyId),
    ),
    tenants: tenants.filter((tenant) =>
      propertyIds.includes(tenant.propertyId),
    ),
    bookings: managedBookings.filter((booking) =>
      propertyIds.includes(booking.propertyId),
    ),
    contracts: managedContracts.filter((contract) =>
      propertyIds.includes(contract.propertyId),
    ),
    invoices: getManagedInvoices().filter((invoice) =>
      propertyIds.includes(invoice.propertyId),
    ),
    equipment: equipment.filter((item) =>
      propertyIds.includes(item.propertyId),
    ),
    maintenance: managedMaintenance.filter((request) =>
      propertyIds.includes(request.propertyId),
    ),
    areas: areas.filter((area) =>
      area.propertyIds.some((id) => propertyIds.includes(id)),
    ),
  }
}
