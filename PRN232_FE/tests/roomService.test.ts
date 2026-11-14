import { afterEach, describe, expect, it, vi } from "vitest"
import { createRoomType } from "@/services/roomService"

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  })
}

afterEach(() => {
  vi.restoreAllMocks()
})

describe("roomService.createRoomType", () => {
  it("posts a new catalog room type to /api/room-types and returns the created dto", async () => {
    const calls: Array<{ url: string; init?: RequestInit }> = []
    vi.stubGlobal(
      "fetch",
      vi.fn<typeof fetch>(async (input, init) => {
        calls.push({ url: String(input), init })
        return jsonResponse(
          { roomTypeId: 9, typeName: "Phòng VIP", price: 0 },
          201,
        )
      }),
    )

    const created = await createRoomType("admin-token", {
      typeName: "Phòng VIP",
      price: 0,
    })

    expect(created).toEqual({
      roomTypeId: 9,
      typeName: "Phòng VIP",
      price: 0,
    })
    expect(calls).toHaveLength(1)
    expect(calls[0].url).toMatch(/\/api\/room-types$/)
    expect(calls[0].init?.method).toBe("POST")
    expect(calls[0].init?.body).toBe(
      JSON.stringify({ typeName: "Phòng VIP", price: 0 }),
    )
    expect(new Headers(calls[0].init?.headers).get("Authorization")).toBe(
      "Bearer admin-token",
    )
  })
})
