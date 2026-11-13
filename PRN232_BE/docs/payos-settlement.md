# payOS B8: atomic invoice settlement

The existing signed webhook endpoint now settles eligible invoice payments.
There is no public endpoint for a customer to assert success. Signature validation
and all B7 identity/amount/currency checks remain in force.

## One transaction

After the provider/reference advisory lock and invoice row lock, recheck that:

- The signed notice matches OrderCode, PaymentLinkId, VND and the request amount.
- The amount equals the current invoice lines and the customer still matches.
- The invoice is Unpaid/Overdue, the request is Creating/Pending and has no PaymentId.
- No different provider reference is already associated with the request.
- Provider transaction time can be validated against the request/receipt timeline.

Then atomically:

1. Conditionally set the invoice Paid.
2. Create a Success payment with PaymentMethod=PayOS, InvoiceId, CustomerId as
   UserId, the verified amount and actual transaction PaidAtUtc.
3. Link that payment to PaymentRequests, set Succeeded, ProviderReference and
   ConfirmedAtUtc; clear old transient errors.
4. Mark the receipt Settled and retain the first WebhookReceivedAtUtc.
5. Add unread Invoice notifications for the customer and current property owner
   (distinct recipients).

Only acknowledge 200 after commit. A storage/notification failure rolls back
the invoice update, payment, request changes, receipt and notifications. It returns
503 so delivery may be retried. Creation time and first receipt time are preserved.

## Time policy

The provider example uses `yyyy-MM-dd HH:mm:ss` without an offset. The application
explicitly interprets this format (optionally fractional seconds) in
Asia/Ho_Chi_Minh; this is an application convention, not an assertion that all
provider timestamp formats carry a timezone. Explicit ISO-8601 offsets or Z are
also accepted. Host-local timezone and locale are never used to infer PaidAtUtc.

Require CreatedAtUtc <= PaidAtUtc < ExpiresAtUtc and PaidAtUtc <= the original
receipt ReceivedAtUtc. Missing, ambiguous, malformed, future, pre-request or late
transaction times require review; never substitute webhook receipt time.
A delayed webhook can settle an on-time payment even after the local deadline
if the request is still Creating/Pending and has not been replaced/closed.
Cancelled/Expired/RequiresReview requests do not auto-settle.

ConfirmedAtUtc is processing time; Payments.PaidAtUtc is the transaction instant
and therefore drives the existing monthly revenue report. Request/receipt times
remain separate. No clock-skew tolerance is silently applied.

## Replay and additional transfers

An identical already-Settled reference does nothing: one payment, one notification
per recipient. An identical legacy B7 Verified event rechecks and settles once on
authenticated replay. There is no automatic backlog worker in B8; B9 will handle
reconciliation/recovery of events that are not replayed.

Wrong amounts, changed invoices, already-paid invoices, closed requests, conflicting
data under the same reference and additional transfer references are retained as
RequiresReview receipts. A legitimate Succeeded request is not reopened or its
payment overwritten by a later conflicting event. No refunds or double-crediting
are attempted. Existing cash and simulated booking behavior are unchanged.

## Schema and validation

No new migration: B8 uses Payments.PaidAtUtc, PaymentRequests and the B7
PaymentWebhookReceipts table. Apply outstanding B7 migration before deployment.
No application DB or real provider account was changed/tested by this step.

Tests use real SDK signature verification with test HMAC payloads and isolated
SQLite. Coverage includes one-time settlement, timestamp conversion, invalid/late
times, delayed delivery, legacy-event replay, additional transfers, status API,
revenue/debt effects and forced transactional rollback with successful retry.
PostgreSQL concurrency still requires an isolated provider integration test.
