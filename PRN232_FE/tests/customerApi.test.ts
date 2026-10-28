import { describe, expect, it, vi } from "vitest"
import { assignMaintenanceRequest, createMaintenanceRequest, getMaintenanceRequest, getMaintenanceLogs, getMaintenanceRequests, submitMaintenanceReport } from "@/api/maintenanceApi"
import { getCurrentRentals, getRentalHistory } from "@/api/rentalsApi"
import { getNotifications, markNotificationRead } from "@/api/notificationsApi"
import { getMaintenanceRooms } from "@/api/usersApi"

function response(body: unknown, status = 200) {
  return new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  })
}

describe("Customer API clients", () => {
  it("passes the current token and loads current and expired rentals", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValue(response([]))
    await getCurrentRentals("customer-token")
    await getRentalHistory("customer-token")
    expect(new Headers(fetchMock.mock.calls[0][1]?.headers).get("Authorization")).toBe("Bearer customer-token")
    expect(String(fetchMock.mock.calls[0][0])).toContain("/api/me/rentals")
    expect(String(fetchMock.mock.calls[1][0])).toContain("/api/me/rentals/history")
  })

  it("posts the selected ContractId, EquipmentId and description without fabricating fields", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValue(response({ requestId: 42 }))
    await createMaintenanceRequest("customer-token", { contractId: 123, equipmentId: 11, description: "Broken" })
    expect(fetchMock.mock.calls[0][1]?.method).toBe("POST")
    expect(JSON.parse(String(fetchMock.mock.calls[0][1]?.body))).toEqual({ contractId: 123, equipmentId: 11, description: "Broken" })
  })

  it("fetches all requests and customer notifications, then marks a notification read", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch")
      .mockResolvedValueOnce(response([]))
      .mockResolvedValueOnce(response([]))
      .mockResolvedValueOnce(response(undefined, 204))
    await getMaintenanceRequests("customer-token")
    await getNotifications("customer-token")
    await markNotificationRead("customer-token", 8)
    expect(String(fetchMock.mock.calls[0][0])).toContain("/api/maintenance-requests")
    expect(String(fetchMock.mock.calls[1][0])).toContain("/api/notifications")
    expect(fetchMock.mock.calls[2][1]?.method).toBe("PATCH")
    expect(JSON.parse(String(fetchMock.mock.calls[2][1]?.body))).toEqual({ isRead: true })
  })

  it("uses the assignment PATCH and multipart report field names defined by the API", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch")
      .mockResolvedValueOnce(response({ requestId: 42 }))
      .mockResolvedValueOnce(response({ reportId: 3 }))
      .mockResolvedValueOnce(response([]))
      .mockResolvedValueOnce(response({ requestId: 42 }))
      .mockResolvedValueOnce(response([]))
    const image = new File(["jpeg"], "proof.jpg", { type: "image/jpeg" })
    const form = new FormData()
    form.set("Description", "Replaced capacitor")
    form.append("Images", image)
    await assignMaintenanceRequest("manager-token", 42, { assignedTo: 9, assignmentNote: "Bring spare part" })
    await submitMaintenanceReport("manager-token", 42, form)
    await getMaintenanceRooms("manager-token")
    await getMaintenanceRequest("manager-token", 42)
    await getMaintenanceLogs("manager-token", 42)
    expect(fetchMock.mock.calls[0]?.[1]?.method).toBe("PATCH")
    expect(JSON.parse(String(fetchMock.mock.calls[0]?.[1]?.body))).toEqual({ assignedTo: 9, assignmentNote: "Bring spare part" })
    expect((fetchMock.mock.calls[1]?.[1]?.body as FormData).get("Description")).toBe("Replaced capacitor")
    expect((fetchMock.mock.calls[1]?.[1]?.body as FormData).get("Images")).toBe(image)
    expect(new Headers(fetchMock.mock.calls[1]?.[1]?.headers).has("Content-Type")).toBe(false)
  })

  it("queries unread notifications without mutating their read state", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValue(response([]))
    await getNotifications("manager-token", undefined, false)
    expect(String(fetchMock.mock.calls[0]?.[0])).toContain("/api/notifications?isRead=false")
    expect(fetchMock.mock.calls[0]?.[1]?.method).toBeUndefined()
  })
})
