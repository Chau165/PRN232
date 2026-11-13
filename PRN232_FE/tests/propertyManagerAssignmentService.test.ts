import { afterEach, describe, expect, it, vi } from "vitest"
import {
  getManagersForProperties,
  getPropertiesForManagers,
} from "@/services/propertyManagerAssignmentService"

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

describe("getPropertiesForManagers (C11b)", () => {
  const property = (propertyId: number) => ({
    propertyId,
    ownerId: 1,
    ownerName: "Admin",
    propertyName: `Khu ${propertyId}`,
    addressText: "",
    ward: "",
    district: "",
    city: "",
    roomCount: 0,
  })

  it("loads properties for every manager with one request", async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse([
      { managerId: 7, properties: [property(1), property(2)] },
    ]))
    vi.stubGlobal("fetch", fetchMock)

    const result = await getPropertiesForManagers("jwt", [7, 8, 9])

    expect(fetchMock).toHaveBeenCalledTimes(1)
    expect(decodeURIComponent(String(fetchMock.mock.calls[0][0])).endsWith(
      "/api/managers/properties?managerIds=7,8,9",
    )).toBe(true)
    expect(result["7"].map((row) => row.propertyId)).toEqual([1, 2])
    expect(result["8"]).toEqual([])
    expect(result["9"]).toEqual([])
  })

  it("does not call the API when there are no managers", async () => {
    const fetchMock = vi.fn<typeof fetch>()
    vi.stubGlobal("fetch", fetchMock)

    expect(await getPropertiesForManagers("jwt", [])).toEqual({})
    expect(fetchMock).not.toHaveBeenCalled()
  })
})
