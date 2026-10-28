import { describe, expect, it, vi } from "vitest"
import { createContract, getContracts } from "@/api/contractsApi"

function response(body: unknown) {
  return new Response(JSON.stringify(body), { status: 200, headers: { "Content-Type": "application/json" } })
}

describe("Contract API client", () => {
  it("lists contracts and creates one from a booking with calendar dates", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch").mockImplementation(async () => response({ contractId: 12 }))
    await getContracts("token")
    await createContract("token", { bookingId: 9, startDate: "2026-11-01", endDate: "2027-11-01" })

    expect(fetchMock.mock.calls.map(([url, init]) => `${init?.method ?? "GET"} ${String(url).replace(/^.*\/api/, "/api")}`))
      .toEqual(["GET /api/contracts", "POST /api/contracts"])
    expect(JSON.parse(String(fetchMock.mock.calls[1][1]?.body)))
      .toEqual({ bookingId: 9, startDate: "2026-11-01", endDate: "2027-11-01" })
    expect(new Headers(fetchMock.mock.calls[0][1]?.headers).get("Authorization")).toBe("Bearer token")
  })
})
