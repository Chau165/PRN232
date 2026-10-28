import { describe, expect, it, vi } from "vitest"
import { approveViewingSchedule, createViewingSchedule, getViewingSchedules, rejectViewingSchedule } from "@/api/viewingSchedulesApi"
import { localInputToIso, toLocalInput, viewingStatusDisplay, viewingTimeLabel } from "@/utils/viewingDisplay"

function response(body: unknown) {
  return new Response(JSON.stringify(body), { status: 200, headers: { "Content-Type": "application/json" } })
}

describe("Viewing schedule API client", () => {
  it("uses the viewing-schedules routes with the API field names", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch").mockImplementation(async () => response({ viewingId: 3 }))
    await getViewingSchedules("token")
    await createViewingSchedule("token", { roomId: 501, scheduledAt: null })
    await approveViewingSchedule("token", 3, "2030-01-01T02:00:00.000Z")
    await rejectViewingSchedule("token", 3)

    expect(fetchMock.mock.calls.map(([url, init]) => `${init?.method ?? "GET"} ${String(url).replace(/^.*\/api/, "/api")}`)).toEqual([
      "GET /api/viewing-schedules",
      "POST /api/viewing-schedules",
      "POST /api/viewing-schedules/3/approve",
      "POST /api/viewing-schedules/3/reject",
    ])
    expect(JSON.parse(String(fetchMock.mock.calls[1][1]?.body))).toEqual({ roomId: 501, scheduledAt: null })
    expect(JSON.parse(String(fetchMock.mock.calls[2][1]?.body))).toEqual({ scheduledAt: "2030-01-01T02:00:00.000Z" })
    expect(new Headers(fetchMock.mock.calls[0][1]?.headers).get("Authorization")).toBe("Bearer token")
  })
})

describe("Viewing display helpers", () => {
  it("labels statuses and owner-decides requests in Vietnamese", () => {
    expect(viewingStatusDisplay({ status: "Pending" }).label).toBe("Chờ xác nhận")
    expect(viewingStatusDisplay({ status: "Approved" }).label).toBe("Đã chấp thuận")
    expect(viewingStatusDisplay({ status: "Rejected" }).label).toBe("Đã từ chối")
    expect(viewingTimeLabel({ scheduledAt: null })).toBe("Chủ trọ sắp xếp")
  })

  it("converts between the local datetime input and UTC without shifting the moment", () => {
    const local = "2030-01-01T09:30"
    expect(Date.parse(localInputToIso(local))).toBe(new Date(local).getTime())
    expect(toLocalInput(new Date(localInputToIso(local)))).toBe(local)
  })
})
