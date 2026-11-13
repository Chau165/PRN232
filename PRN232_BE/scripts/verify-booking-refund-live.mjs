import { pathToFileURL } from 'node:url'

export async function verifyRefund({ baseUrl, customerToken, adminToken, bookingId, paymentRequestId, refundRequestId, month }, request = fetch) {
  const base = new URL(baseUrl)
  if (base.username || base.password || base.search || base.hash || (base.protocol !== 'https:' && !(base.protocol === 'http:' && ['localhost', '127.0.0.1', '[::1]'].includes(base.hostname)))) throw new Error('Use HTTPS or local HTTP API base URL without credentials/query.')
  const ids = [bookingId, paymentRequestId, refundRequestId].map(Number)
  if (ids.some(id => !Number.isSafeInteger(id) || id < 1) || !/^\d{4}-(0[1-9]|1[0-2])$/.test(month) || !customerToken || !adminToken) throw new Error('Provide IDs, month and both role tokens.')
  async function get(path, token) {
    const response = await request(`${base.href.replace(/\/$/, '')}${path}`, { method: 'GET', headers: { Authorization: `Bearer ${token}` }, cache: 'no-store', redirect: 'error', signal: AbortSignal.timeout(15000) })
    if (!response.ok) throw new Error(`Read failed: HTTP ${response.status}`)
    return response.json()
  }
  const payment = await get(`/payment-requests/${ids[1]}`, customerToken)
  const refunds = await get(`/bookings/${ids[0]}/refund-requests`, customerToken)
  const refund = refunds.find(r => r.bookingRefundRequestId === ids[2])
  if (payment.paymentRequestId !== ids[1] || payment.bookingId !== ids[0] || payment.invoiceId != null || payment.status !== 'Succeeded' || payment.bookingStatus !== 'Cancelled' || !payment.paymentId || payment.currency !== 'VND') throw new Error('Incoming payment / cancelled booking not confirmed.')
  if (!refund || refund.bookingId !== ids[0] || refund.originalPaymentId !== payment.paymentId || refund.status !== 'Refunded' || refund.currency !== 'VND' || !(refund.approvedAmount > 0) || refund.approvedAmount > payment.amount || !refund.refundedAtUtc) throw new Error('Matching completed refund not confirmed.')
  const report = await get(`/reports/booking-deposits?${new URLSearchParams({ fromMonth: `${month}-01`, toMonth: `${month}-01`, propertyId: String(payment.propertyId) })}`, adminToken)
  if (!report.totals || !report.unknownDate || report.propertyId !== payment.propertyId || report.fromMonth !== `${month}-01`) throw new Error('Report scope mismatch.')
  // The aggregate is not individual bank evidence; compare ledger and provider dashboard as described in the runbook.
  return { bookingId: ids[0], paymentRequestId: ids[1], refundRequestId: ids[2], amountCollected: payment.amount, amountRefunded: refund.approvedAmount, reportMonth: month, totals: report.totals, unknownDate: report.unknownDate }
}
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  verifyRefund({ baseUrl: process.env.P12_API_BASE_URL, customerToken: process.env.P12_CUSTOMER_TOKEN, adminToken: process.env.P12_ADMIN_TOKEN, bookingId: process.env.P12_BOOKING_ID, paymentRequestId: process.env.P12_PAYMENT_REQUEST_ID, refundRequestId: process.env.P12_REFUND_REQUEST_ID, month: process.env.P12_MONTH })
    .then(result => console.log(JSON.stringify({ verifiedReadOnly: true, ...result }, null, 2)))
    .catch(() => { console.error('Verification failed. Check role tokens, IDs, confirmed payment/refund and report scope. No writes were sent.'); process.exitCode = 1 })
}
