import { describe, expect, it } from "vitest"
import { notificationPath } from "@/services/notificationNavigation"
import { notificationHubUrl } from "@/services/notificationService"

describe("notificationPath", () => {
  it("routes customers and management users to the correct business screens", () => {
    expect(notificationPath("Viewing", "MANAGER", 48)).toBe("/management/viewings?viewingId=48")
    expect(notificationPath("Booking", "ADMIN")).toBe("/management/bookings")
    expect(notificationPath("Booking", "CUSTOMER")).toBe("/my-bookings")
    expect(notificationPath("Maintenance", "MANAGER")).toBe("/management/maintenance")
    expect(notificationPath("Maintenance", "CUSTOMER")).toBe("/my-room?tab=maintenance")
    expect(notificationPath("Viewing", "CUSTOMER", 48)).toBe("/my-bookings?viewingId=48")
    expect(notificationPath("Viewing", "ADMIN", null)).toBe("/management/viewings")
    expect(notificationPath("Contract", "CUSTOMER")).toBe("/my-contracts")
    expect(notificationPath("Invoice", "MANAGER")).toBe("/management/invoices")
    expect(notificationPath("unknown", "CUSTOMER")).toBe("/notifications")
  })

  it("builds a same-origin hub URL from the API base path", () => {
    expect(notificationHubUrl()).toBe(`${window.location.origin}/hubs/notifications`)
  })
})
