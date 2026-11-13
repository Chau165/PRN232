# Revenue (invoice collections)

`GET /api/reports/revenue?fromMonth=2026-01-01&toMonth=2026-12-01&propertyId=1`

Bearer token required. Admin sees all properties. Manager sees only properties
currently owned by their user ID; changing OwnerId transfers access to all history.
Customer receives 403. An explicit foreign property gives 403; a missing one 404.

Dates must be month starts in ascending order, at most 24 months inclusive. Omit
fromMonth to default to January of the current Vietnam year; omit toMonth to use
the current Vietnam month. Invalid filters give 400. Future months are allowed
and normally contain zeros. Omit propertyId to report the caller's entire scope.

Count only Success payments with InvoiceId. Sum Payment.Amount (all invoice fees,
including utilities and services); do not join invoice detail lines, multiply
totals, or use invoice month as payment month. Cash and online payments both count;
booking deposits, unsuccessful payments and uncollected invoices do not. This is
cash collected, not profit; online payments are still simulated by the gateway.

PaidAtUtc is grouped by Vietnam calendar month. The date range starts at local
midnight of fromMonth and ends exclusively at local midnight after toMonth.
Payments with null PaidAtUtc are counted separately for the authorized property
scope, independently of the date filter. They are never assigned to an invented
month and never added to totalCollected.

Response shape (illustrative):

```json
{
  "fromMonth": "2026-01-01",
  "toMonth": "2026-12-01",
  "propertyId": null,
  "totalCollected": 3000000,
  "paymentCount": 1,
  "unknownDateAmount": 500000,
  "unknownDatePaymentCount": 1,
  "monthly": [
    { "month": "2026-11-01", "collectedAmount": 3000000, "paymentCount": 1 }
  ],
  "byProperty": [
    { "propertyId": 1, "propertyName": "An Nhiên", "collectedAmount": 3000000,
      "paymentCount": 1, "unknownDateAmount": 500000, "unknownDatePaymentCount": 1 }
  ],
  "monthlyByProperty": [
    { "month": "2026-11-01", "propertyId": 1, "propertyName": "An Nhiên",
      "collectedAmount": 3000000, "paymentCount": 1 }
  ]
}
```

Example arrays are abbreviated. Actual monthly contains every requested month;
byProperty includes all properties in scope even without collections;
monthlyByProperty contains each month/property combination, zero-filled.
Unknown totals also appear per property. No PII or payment tokens are returned.

PostgreSQL performs numeric SUM/COUNT and timezone grouping in a single scoped
query. SQLite tests use a portable decimal aggregation fallback. No migration,
payment writes, FE integration, or refund workflow is added by this endpoint.
