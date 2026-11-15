import { expect, it, vi } from "vitest"
import { createPropertyLocationSearch, propertyAddressQueries } from "@/services/propertyLocationService"
const address = { addressText: "121, đường Lê Văn Việt", provinceCode: "79", wardCode: "01", provinceName: "Thành phố Hồ Chí Minh", wardName: "Phường Thủ Đức" }
const feature = (properties = {}, coordinates = [106.77, 10.85]) => ({ geometry: { type: "Point", coordinates }, properties: { street: "Lê Văn Việt", housenumber: "119", countrycode: "VN", ...properties } })
const response = (features: unknown[]) => new Response(JSON.stringify({ features }))
it("normalizes punctuation and road prefix while preserving numbered roads", () => {
  const queries = propertyAddressQueries(address)
  expect(queries[0]).toContain("121 đường Lê Văn Việt")
  expect(queries[1]).toContain("121 Lê Văn Việt")
  expect(queries[2]).toMatch(/^Lê Văn Việt,/)
  expect(propertyAddressQueries({ ...address, addressText: "12 Đường số 8" })[1]).toContain("Đường số 8")
})
it("searches externally without app credentials, validates coordinates and caches suggestions", async () => {
  const fetcher = vi.fn<typeof fetch>().mockResolvedValue(response([feature(), feature(), feature({}, [1, 2]), feature({countrycode: "US"}), feature({street: "Khác"})]))
  const search = createPropertyLocationSearch({fetcher})
  const result = await search(address)
  expect(result.candidates).toHaveLength(1)
  expect(result.candidates[0].location.coordinates).toEqual({latitude: 10.85, longitude: 106.77})
  expect(result.candidates[0].location.displayAddress).toBe("119 Lê Văn Việt")
  expect(result.candidates[0].reasons).toContain("ward_unverified")
  const [url, options] = fetcher.mock.calls[0]
  expect(new URL(String(url)).host).toBe("photon.komoot.io")
  expect(new URL(String(url)).searchParams.get("q")).toContain(address.wardName)
  expect(options?.credentials).toBe("omit")
  expect(options?.headers).toEqual({Accept: "application/json"})
  await search(address)
  expect(fetcher).toHaveBeenCalledTimes(1)
})
it("limits empty-result fallbacks to three queries", async () => {
  const fetcher = vi.fn<typeof fetch>().mockImplementation(async()=>response([]))
  expect((await createPropertyLocationSearch({fetcher})(address)).status).toBe("NoResults")
  expect(fetcher).toHaveBeenCalledTimes(3)
})
it("stops immediately on rate limiting", async () => {
  const fetcher = vi.fn<typeof fetch>().mockResolvedValue(new Response("", {status:429}))
  expect((await createPropertyLocationSearch({fetcher})(address)).status).toBe("RateLimited")
  expect(fetcher).toHaveBeenCalledTimes(1)
})
it("handles malformed JSON and network failure", async () => {
  const fetcher = vi.fn<typeof fetch>().mockResolvedValueOnce(new Response("invalid")).mockRejectedValueOnce(new TypeError("network"))
  const search = createPropertyLocationSearch({fetcher})
  expect((await search(address)).status).toBe("Unavailable")
  expect((await search(address)).status).toBe("Unavailable")
})
it("cancels stale requests without issuing fallbacks", async () => {
  const controller = new AbortController()
  const fetcher = vi.fn<typeof fetch>().mockImplementation(async (_url, options) => new Promise((_resolve, reject) => {
    options?.signal?.addEventListener("abort", ()=>reject(new DOMException("Aborted", "AbortError")))
  }))
  const pending = createPropertyLocationSearch({fetcher})(address, controller.signal)
  controller.abort()
  await expect(pending).rejects.toThrow()
  expect(fetcher).toHaveBeenCalledTimes(1)
})
it("returns unavailable after the search deadline", async () => {
  const fetcher = vi.fn<typeof fetch>().mockImplementation(async (_url, options) => new Promise((_resolve, reject) => {
    options?.signal?.addEventListener("abort", ()=>reject(new DOMException("Aborted", "AbortError")))
  }))
  expect((await createPropertyLocationSearch({fetcher,timeoutMs:5})(address)).status).toBe("Unavailable")
})
