# payOS B5: create invoice checkout

`POST /api/invoices/{id}/payment-link` requires a Customer bearer token. No request
body is needed. The authenticated customer must own the invoice. The server reads
the total from stored invoice lines; client-supplied amounts/URLs are not accepted.
Both Unpaid and Overdue are payable. Zero/negative/fractional VND totals and totals
outside the supported safe integer range are rejected. PaymentRequests migration
and B4 payOS configuration must be installed before using this endpoint.

```json
{
  "paymentRequestId": 123,
  "invoiceId": 5,
  "status": "Pending",
  "amount": 3355000,
  "currency": "VND",
  "checkoutUrl": "https://pay.payos.vn/web/example",
  "expiresAtUtc": "2026-11-11T10:30:00Z",
  "reused": false
}
```

Response example is illustrative. A new checkout returns 201; a reusable unexpired
Pending checkout returns 200 with reused=true. The default checkout lifetime is
30 minutes. All timestamps are UTC. Amount, currency, provider order code and HTTPS
checkout URL are checked before saving the link. Creating a link never writes a
successful Payment, changes the invoice to Paid, or increases revenue.

## Reservation and failure handling

The repository locks the invoice row in a PostgreSQL transaction, rechecks owner,
status and total, then saves Creating before calling payOS outside the transaction.
Database unique indexes provide a second guard. A second click while Creating
returns 202 without sending another create request. SDK creation retries are zero.

A confirmed creation rejection is saved as Create-failed and returns 502 with a
sanitized message; another attempt may create a new request. Timeout, client
cancellation, transport errors or other uncertain outcomes remain Creating and
return 202 with checkoutUrl=null. Cleanup persistence uses a bounded independent
cancellation token so browser disconnection does not erase the reservation.
Neither raw provider errors nor credentials are returned or logged here.

Creating is not automatically failed/expired by elapsed time: a provider link may
already exist. Recovery from this uncertain state belongs to the later
reconciliation step. Repeated requests do not issue another payOS create call.

An expired local Pending link is checked with payOS. Only CANCELLED/EXPIRED with
zero received amount releases the active request and permits replacement. PAID,
partial receipt or an unresolved provider status requires review (409). Failed
status lookup returns 202 without exposing the expired URL or issuing another
create. A changed invoice/customer/amount during creation requires review.

Other responses: 400 invalid amount/id; 401 missing authentication; 403 wrong
customer or role; 404 missing invoice; 409 paid invoice or unresolved checkout;
503 payOS disabled; 429 rate limit (same per-user policy as Booking).

## Compatibility and remaining steps

When PayOS:Enabled=true, legacy POST /api/invoices/{id}/pay is blocked (409), so it
cannot mark real checkouts paid through the simulated gateway. MarkPaidAsync
serializes on the same invoice row and refuses settlement when an active Creating,
Pending or RequiresReview request exists. This also temporarily blocks recording
cash for that invoice until the later cash/cancel/reconciliation workflow exists.
With payOS disabled and no active checkout, existing demo/cash behavior remains.
Booking deposits remain simulated and are outside B5.

B6 status API, B7 webhook verification and B8 settlement are NOT implemented by
this change. Do not treat browser return URLs or link creation as payment success.
Do not open real customer checkouts for production use before those steps and
reconciliation are implemented. No real payOS calls or money movements are made
by automated tests, which use a fake gateway and SQLite. PostgreSQL concurrency
behavior still requires validation in an isolated PostgreSQL integration setup.
No new migration or frontend change is needed for this endpoint itself.

SDK/API references: https://payos.vn/docs/api/ and https://payos.vn/docs/sdks/back-end/net/.
