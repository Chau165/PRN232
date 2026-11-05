import {
  managedBookings,
  managedContracts,
  managedMaintenance,
  managementProperties,
  managementRooms,
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

    bookings: managedBookings.filter((booking) =>
      propertyIds.includes(booking.propertyId),
    ),

    contracts: managedContracts.filter((contract) =>
      propertyIds.includes(contract.propertyId),
    ),

    invoices: getManagedInvoices().filter((invoice) =>
      propertyIds.includes(invoice.propertyId),
    ),

    maintenance: managedMaintenance.filter((request) =>
      propertyIds.includes(request.propertyId),
    ),
  }
}
