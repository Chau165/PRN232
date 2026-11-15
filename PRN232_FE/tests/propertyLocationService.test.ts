import { expect, it, vi } from "vitest"
import { createPropertyAreaLocator, createPropertyLocationSearch, propertyAddressQueries } from "@/services/propertyLocationService"
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
const area = (name: string, coordinates: number[], properties = {}) => ({ geometry: { type: "Point", coordinates }, properties: { name, countrycode: "VN", ...properties } })
it("locates the ward inside the selected province and uses its extent", async () => {
  const fetcher = vi.fn<typeof fetch>().mockResolvedValue(response([
    area("Phường Long Bình", [106.8, 10.95], { state: "Đồng Nai" }),
    area("Long Bình", [106.83, 10.88], { city: "Thành phố Hồ Chí Minh", extent: [106.8, 10.9, 106.86, 10.86] }),
  ]))
  const focus = await createPropertyAreaLocator({ fetcher })({ wardName: "Phường Long Bình", provinceName: "Thành phố Hồ Chí Minh" })
  expect(focus).toEqual({ latitude: 10.88, longitude: 106.83, zoom: 15, bounds: [[10.86, 106.8], [10.9, 106.86]] })
  const url = new URL(String(fetcher.mock.calls[0][0]))
  expect(url.searchParams.get("q")).toBe("Phường Long Bình, Thành phố Hồ Chí Minh")
  expect(url.searchParams.getAll("layer")).toContain("district")
  expect(fetcher.mock.calls[0][1]?.credentials).toBe("omit")
})
it("falls back to the province when the merged ward is not on the map yet", async () => {
  const fetcher = vi.fn<typeof fetch>()
    .mockResolvedValueOnce(response([area("Phường Bến Thành", [106.7, 10.77], { city: "Thành phố Hồ Chí Minh" })]))
    .mockResolvedValueOnce(response([area("Thành phố Hồ Chí Minh", [106.66, 10.78])]))
  const locate = createPropertyAreaLocator({ fetcher })
  expect(await locate({ wardName: "Phường Long Bình", provinceName: "Thành phố Hồ Chí Minh" })).toEqual({ latitude: 10.78, longitude: 106.66, zoom: 11 })
  expect(new URL(String(fetcher.mock.calls[1][0])).searchParams.get("q")).toBe("Thành phố Hồ Chí Minh")
  await locate({ wardName: "Phường Long Bình", provinceName: "Thành phố Hồ Chí Minh" })
  expect(fetcher).toHaveBeenCalledTimes(2)
})
it("ignores an ambiguous ward name outside the province and gives up quietly on errors", async () => {
  const fetcher = vi.fn<typeof fetch>()
    .mockResolvedValueOnce(response([area("Phường An Lạc", [105, 21], { state: "Hà Nội" }), area("Phường An Lạc", [106, 11], { state: "Bình Dương" })]))
    .mockResolvedValueOnce(new Response("", { status: 503 }))
  expect(await createPropertyAreaLocator({ fetcher })({ wardName: "Phường An Lạc", provinceName: "Thành phố Hồ Chí Minh" })).toBeNull()
  expect(await createPropertyAreaLocator({ fetcher: vi.fn() })({ wardName: "Phường An Lạc" })).toBeNull()
})
it("searches with composed Vietnamese text even when typed in Unicode to hop", async () => {
  const fetcher = vi.fn<typeof fetch>().mockResolvedValue(response([feature({ street: "Nguyễn Xiển", housenumber: "520" })]))
  const typed = { ...address, addressText: "520  Nguyễn  Xiển", wardName: "Phường Long Bình" }
  const result = await createPropertyLocationSearch({ fetcher })(typed)
  const q = new URL(String(fetcher.mock.calls[0][0])).searchParams.get("q")!
  expect(q).toBe("520 Nguyễn Xiển, Phường Long Bình, Thành phố Hồ Chí Minh, Việt Nam")
  expect(q.normalize("NFC")).toBe(q)
  expect(result.candidates).toHaveLength(1)
})
