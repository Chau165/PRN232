import { expect, it, vi } from "vitest"
import { createBookingRefund, getBookingRefunds, getRefundReviews, reviewBookingRefund } from "@/services/bookingRefundService"

function response(data: unknown) { return new Response(JSON.stringify(data), { headers: { "Content-Type": "application/json" } }) }
it("sends only customer request fields, never forged amounts or customer IDs, and preserves the account string", async () => {
  const fetch = vi.spyOn(globalThis, "fetch").mockResolvedValue(response({ bookingRefundRequestId: 12 }))
  const input = { reason: "Đổi kế hoạch", recipientBankBin: "970422", recipientAccountNumber: "001234567890", recipientAccountName: "NGUYEN AN", requestedAmount: 99999999, customerId: 20, status: "Refunded" }
  await createBookingRefund("customer-token", 7, input)
  expect(String(fetch.mock.calls[0][0])).toMatch(/\/api\/bookings\/7\/refund-requests$/)
  const options = fetch.mock.calls[0][1]!
  expect(options.method).toBe("POST"); expect(options.cache).toBe("no-store")
  expect(new Headers(options.headers).get("Authorization")).toBe("Bearer customer-token")
  expect(JSON.parse(String(options.body))).toEqual({ reason: input.reason, recipientBankBin: input.recipientBankBin, recipientAccountNumber: "001234567890", recipientAccountName: input.recipientAccountName })
})
it("sends an explicit decision without choosing the reviewer identity", async () => {
  const fetch = vi.spyOn(globalThis, "fetch").mockResolvedValue(response({ bookingRefundRequestId: 12 }))
  await reviewBookingRefund("admin-token", 12, { approve: false, approvedAmount: null, reviewNote: "Tài khoản cần kiểm tra" })
  expect(String(fetch.mock.calls[0][0])).toMatch(/\/booking-refund-requests\/12\/review$/)
  expect(JSON.parse(String(fetch.mock.calls[0][1]?.body))).toEqual({ approve: false, approvedAmount: null, reviewNote: "Tài khoản cần kiểm tra" })
})
it("reads scoped history and paged admin results without caching destination information", async () => {
  const fetch = vi.spyOn(globalThis, "fetch").mockImplementation(async () => response([]))
  const controller = new AbortController()
  await getBookingRefunds("manager-token", 7, controller.signal)
  await getRefundReviews("admin-token", "Approved", 2, controller.signal)
  expect(String(fetch.mock.calls[0][0])).toMatch(/\/bookings\/7\/refund-requests$/)
  const url = new URL(String(fetch.mock.calls[1][0]), "https://test.local")
  expect(url.searchParams.get("status")).toBe("Approved"); expect(url.searchParams.get("page")).toBe("2")
  for (const [, options] of fetch.mock.calls) { expect(options?.cache).toBe("no-store"); expect(options?.signal).toBe(controller.signal) }
})
