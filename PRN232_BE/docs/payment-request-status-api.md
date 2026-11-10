# payOS B6: read payment request status

`GET /api/payment-requests/{id}` with a bearer token. No body or owner/customer
query filter is needed. Reads remain available when payOS configuration is disabled.

- Customer: only requests whose CustomerId equals their authenticated user ID.
- Manager: requests attached to invoices in properties currently owned by that
  Manager. Ownership transfer gives the new Manager access and removes the old
  Manager's access to history.
- Admin: all requests.

Response example (illustrative):

```json
{
  "paymentRequestId": 123,
  "invoiceId": 5,
  "customerId": 10,
  "propertyId": 1,
  "status": "Pending",
  "invoiceStatus": "Unpaid",
  "amount": 3355000,
  "currency": "VND",
  "createdAtUtc": "2026-11-11T10:00:00Z",
  "updatedAtUtc": "2026-11-11T10:00:01Z",
  "expiresAtUtc": "2026-11-11T10:30:00Z",
  "confirmedAtUtc": null,
  "paymentId": null,
  "paidAtUtc": null,
  "isExpired": false,
  "canOpenCheckout": true,
  "checkoutUrl": "https://pay.payos.vn/web/example"
}
```

Status is the stored application status, including exactly Create-failed. The GET
does not contact payOS, verify receipt, update status, reconcile a timeout, write
Payments or mark an invoice Paid. Webhook/settlement are implemented separately
in B7/B8; see payos-settlement.md.

IsExpired is true for stored Expired, or Creating/Pending with a local deadline
at or before the server's current UTC instant. It is a UI hint, not a provider
confirmation. A locally expired Pending request therefore still returns Pending.
CanOpenCheckout requires Pending, a known future deadline, a stored URL, and an
Unpaid/Overdue invoice. CheckoutUrl is null otherwise. InvoiceStatus and request
status remain separate; callers must not interpret an elapsed deadline as proof
that no money was received. ConfirmedAtUtc is confirmation time, while PaidAtUtc
comes from the linked Payments record and can be null for legacy data.

Internal provider identifiers, raw errors, ProviderReference, LastErrorCode and
LastErrorMessage are deliberately excluded. All successful responses use no-store
cache headers to avoid caching sensitive/stale payment state.

Responses: 200 snapshot; 400 nonpositive integer ID; 401 missing authentication;
403 existing request outside authorized scope; 404 nonexistent request (or a
non-integer route). Caller-supplied customerId/ownerId cannot override access.

No migration, SDK calls, financial changes or frontend changes are part of B6.
