export const routes = {
  home: "/",
  propertyDetail: "/property/:propertyId",
  availableRooms: "/property/:propertyId/rooms",
  myRoom: "/my-room",
  myContracts: "/my-contracts",
  myBills: "/my-bills",
  myBookings: "/my-bookings",
  notifications: "/notifications",
  profile: "/profile",
  managementDashboard: "/management/dashboard",
  managementAreas: "/management/areas",
  managementProperties: "/management/properties",
  managementRooms: "/management/rooms",
  managementTenants: "/management/tenants",
  managementBookings: "/management/bookings",
  managementViewings: "/management/viewings",
  managementContracts: "/management/contracts",
  managementCreateContract: "/management/contracts/new",
  managementInvoices: "/management/invoices",
  managementEquipment: "/management/equipment",
  managementMaintenance: "/management/maintenance",
  managementReports: "/management/reports",
  managementManagers: "/management/managers",
  managementUsers: "/management/users",
  login: "/login",
  register: "/register",
  deposit: "/deposit",
  paymentSuccess: "/payment-success",
} as const

function requirePositiveRouteId(id: number): number {
  if (!Number.isSafeInteger(id) || id < 1) {
    throw new RangeError("A positive numeric route ID is required.")
  }
  return id
}

export function propertyDetailPath(propertyId: number): string {
  return "/property/" + requirePositiveRouteId(propertyId)
}

export function availableRoomsPath(propertyId: number): string {
  return propertyDetailPath(propertyId) + "/rooms"
}

export function parsePositiveRouteId(value: string | undefined): number | null {
  if (!value || !/^[1-9]\d*$/.test(value)) return null
  const id = Number(value)
  return Number.isSafeInteger(id) ? id : null
}
