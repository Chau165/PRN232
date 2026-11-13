import { test } from 'node:test'
import assert from 'node:assert/strict'
import { verifyRefund } from './verify-booking-refund-live.mjs'
const input = { baseUrl: 'https://example.test/api', customerToken: 'customer', adminToken: 'admin', bookingId: 1, paymentRequestId: 2, refundRequestId: 3, month: '2026-11' }
function fake(overrides = {}) {
  const calls = []
  return { calls, request: async (url, init) => {
    calls.push({ url, init })
    const data = url.includes('/payment-requests/') ? { paymentRequestId: 2, bookingId: 1, status: 'Succeeded', bookingStatus: 'Cancelled', paymentId: 4, propertyId: 5, currency: 'VND', amount: 300, ...overrides }
      : url.includes('/refund-requests') ? [{ bookingRefundRequestId: 3, bookingId: 1, originalPaymentId: 4, status: 'Refunded', currency: 'VND', approvedAmount: 250, refundedAtUtc: '2026-11-13T00:00:00Z' }]
      : { propertyId: 5, fromMonth: '2026-11-01', totals: {}, unknownDate: {} }
    return { ok: true, json: async () => data }
  } }
}
test('verifies matching IDs using only GET without disclosing tokens or bank account', async () => {
  const api = fake(); const result = await verifyRefund(input, api.request)
  assert.equal(result.amountRefunded, 250); assert.equal(api.calls.length, 3)
  assert.ok(api.calls.every(c => c.init.method === 'GET' && c.init.cache === 'no-store' && c.init.redirect === 'error'))
  assert.ok(!JSON.stringify(result).includes('customer'))
})
test('does not accept unpaid requests as evidence or follow with report lookup', async () => {
  const api = fake({ status: 'Pending' }); await assert.rejects(verifyRefund(input, api.request))
  assert.equal(api.calls.length, 2)
})
test('rejects mismatched booking and nonlocal insecure destinations', async () => {
  await assert.rejects(verifyRefund(input, fake({ bookingId: 99 }).request))
  await assert.rejects(verifyRefund({ ...input, baseUrl: 'http://example.test/api' }, fake().request))
})
