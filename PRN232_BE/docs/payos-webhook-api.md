# payOS B7: authenticated webhook ingestion

`POST /api/payments/payos/webhook`, JSON, maximum request body 64 KiB.
No application JWT is required. Authenticity is checked using the official
payOS SDK Webhooks.VerifyAsync and the configured channel's checksum key.
PayOS:Enabled must be true. Do not expose credentials or raw webhook payloads in
logs, API responses or frontend configuration.

The handler verifies signature before looking up data. Success requires signed
data.code=00 and a consistent success/code envelope. Because the signature covers
data, outer envelope fields alone can never confirm a payment. Structural errors,
missing/bad signature and inconsistent success envelope return 400 without writes.

Verified notices are durably stored in PaymentWebhookReceipts. Only fields needed
for reconciliation are stored: reference, order/link, amount/currency, signed data
code, provider timestamp string, fingerprint, received UTC time and optional
request link. Account names/numbers and full JSON payloads are not retained.

## Outcomes

- Settled (B8): order and link match a PayOS request, VND and amount match both the
  request snapshot and current invoice total, customer still matches, invoice is
  payable, request is Creating/Pending and the verified transaction time is within
  the checkout window. The receipt/payment/invoice/request/notifications commit together.
- Verified: legacy B7 event retained before B8. An identical authenticated replay
  now attempts settlement after rechecking all current data.
- RequiresReview: wrong amount/currency/link, late receipt, terminal request,
  changed/already-paid invoice, conflicting reference or an additional transfer
  reference for the same request. Terminal requests are not reactivated, to avoid
  conflicting with a newer active request. Review is retained on the receipt.
- IgnoredStatus: authenticated notice whose signed data code is not success.
- UnknownOrder: authenticated notice for an unknown order, including potential
  provider registration probes. Retained without changing another request.

An accepted business mismatch returns 200 only after durable recording; this
acknowledges receipt, not successful settlement. A valid repeat with the same
reference and monetary fingerprint returns 200 without a second row or changing
the initial receipt timestamp. Reusing a reference with different signed data
preserves the original values and records a conflicting fingerprint for review.

PostgreSQL serializes by provider/reference advisory lock, then locks the invoice
row before reading/updating its request (same invoice-first order as checkout and
settlement). The unique provider/reference index supplies an additional guard.
Storage errors return sanitized 503, never a successful acknowledgement. Disabled
payOS also returns 503. No live API calls are made to verify signatures.

## Migration and rollout

Apply `20261110182057_AddPaymentWebhookReceipts` before enabling this route against
an existing database. Migration creates only the receipt table, indexes and FK;
it does not alter monetary records. No configured local connection was available
during this implementation, so no existing database was migrated.

Configure a public HTTPS URL ending in `/api/payments/payos/webhook` on the payOS
channel when ready. localhost alone cannot receive provider requests. No webhook
was registered and no real transaction was performed by this change.

The original B7 only ingested events. B8 now settles eligible authenticated
events atomically; see `payos-settlement.md` for timestamp policy, rollback and
replay behavior. Late or inconsistent transfers still require review. Provider
registration, reconciliation, cash/online coordination and FE rollout remain
separate steps; this endpoint alone is not the complete production rollout.

Tests use independently generated test HMAC signatures and the real SDK verifier
inside an isolated SQLite API host. No production keys, outbound provider calls
or writes to the user's application data are involved. PostgreSQL lock behavior
and migration replay still need an isolated PostgreSQL integration environment.

Official references: https://payos.vn/docs/du-lieu-tra-ve/webhook/ and
https://payos.vn/docs/sdks/back-end/net/.
