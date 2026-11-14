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

  managementProperties: "/management/properties",

  managementPropertyRooms: "/management/properties/:propertyId/rooms",
  managementPropertyRoomTypes: "/management/properties/:propertyId/room-types",
  managementRoomEquipment:
    "/management/properties/:propertyId/rooms/:roomId/equipment",

  managementRooms: "/management/rooms",

  managementBookings: "/management/bookings",
  managementRefunds: "/management/refunds",

  managementViewings: "/management/viewings",

  managementContracts: "/management/contracts",

  managementCreateContract: "/management/contracts/new",

  managementInvoices: "/management/invoices",

  managementEquipment: "/management/equipment",

  managementMaintenance: "/management/maintenance",

  managementReports: "/management/reports",

  managementManagers: "/management/managers",

  managementPaymentReviews: "/management/payment-reviews",

  managementUsers: "/management/users",

  login: "/login",

  register: "/register",

  deposit: "/deposit",

  viewingRequest: "/viewing-request",

  paymentSuccess: "/payment-success",
  paymentResult: "/payment-result",
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

export function viewingRequestPath(propertyId: number, roomTypeId: number): string {
  const validPropertyId = requirePositiveRouteId(propertyId)
  const validRoomTypeId = requirePositiveRouteId(roomTypeId)
  return `${routes.viewingRequest}?${new URLSearchParams({
    propertyId: String(validPropertyId),
    roomTypeId: String(validRoomTypeId),
  })}`
}

export function managementPropertyRoomsPath(propertyId: number): string {
  return "/management/properties/" + requirePositiveRouteId(propertyId) + "/rooms"
}

export function managementPropertyRoomTypesPath(propertyId: number): string {
  return "/management/properties/" + requirePositiveRouteId(propertyId) + "/room-types"
}

export function managementRoomEquipmentPath(
  propertyId: number,
  roomId: number,
): string {
  return (
    managementPropertyRoomsPath(propertyId) +
    "/" +
    requirePositiveRouteId(roomId) +
    "/equipment"
  )
}

export function parsePositiveRouteId(value: string | undefined): number | null {
  if (!value || !/^[1-9]\d*$/.test(value)) return null

  const id = Number(value)

  return Number.isSafeInteger(id) ? id : null
}
