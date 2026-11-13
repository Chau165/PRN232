import { afterEach, describe, expect, it, vi } from "vitest"
import { getManagersForProperties } from "@/services/propertyManagerAssignmentService"

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  })
}

const assignment = (propertyId: number, managerId: number) => ({
  propertyId,
  propertyName: `Khu ${propertyId}`,
  managerId,
  managerName: `Manager ${managerId}`,
  managerEmail: `m${managerId}@example.test`,
  managerIsActive: true,
})

afterEach(() => vi.unstubAllGlobals())

describe("getManagersForProperties (C11)", () => {
  it("loads managers for every property with one request", async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse([
      { propertyId: 1, managers: [assignment(1, 7), assignment(1, 8)] },
      { propertyId: 3, managers: [assignment(3, 7)] },
    ]))
    vi.stubGlobal("fetch", fetchMock)

    const ids = Array.from({ length: 30 }, (_, index) => index + 1)
    const result = await getManagersForProperties("jwt", ids)

    expect(fetchMock).toHaveBeenCalledTimes(1)
    expect(decodeURIComponent(String(fetchMock.mock.calls[0][0])).endsWith(
      `/api/properties/managers?propertyIds=${ids.join(",")}`,
    )).toBe(true)
    expect(result["1"].map((row) => row.managerId)).toEqual([7, 8])
    expect(result["3"].map((row) => row.managerId)).toEqual([7])
    expect(result["2"]).toEqual([])
    expect(Object.keys(result)).toHaveLength(30)
  })

  it("does not call the API when there are no properties", async () => {
    const fetchMock = vi.fn<typeof fetch>()
    vi.stubGlobal("fetch", fetchMock)

    expect(await getManagersForProperties("jwt", [])).toEqual({})
    expect(fetchMock).not.toHaveBeenCalled()
  })
})
