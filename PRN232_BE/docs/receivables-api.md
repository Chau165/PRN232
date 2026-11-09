# Current receivables

`GET /api/reports/receivables?propertyId=1`

Requires bearer authentication. Admin sees all properties by default; Manager
sees only properties whose OwnerId is their authenticated user ID. Customer is
forbidden. An optional positive propertyId narrows the authorized scope.

```json
{
  "asOfDate": "2026-11-09",
  "propertyId": 1,
  "outstandingAmount": 12500000,
  "outstandingInvoiceCount": 5,
  "notYetDueAmount": 5000000,
  "notYetDueInvoiceCount": 2,
  "overdueAmount": 7500000,
  "overdueInvoiceCount": 3
}
```

This example is illustrative. The report is computed at request time using the
Vietnam calendar date. Optional asOfDate is accepted only when equal to today's
Vietnam date; historical and future dates return 400. Omitting propertyId returns
propertyId=null, meaning the authenticated caller's entire authorized scope.

Current billing supports full settlement only. For Unpaid/Overdue invoices,
remaining debt is the sum of InvoiceDetails.Amount. Paid invoices are excluded.
Only positive balances count; invoices with zero/no lines do not create debt.
Unknown status values are not interpreted as debt. Booking deposits and other
non-invoice payments are excluded. An ended contract does not erase invoice debt.

Overdue means DueDate < asOfDate, independently of a stored legacy Overdue label.
DueDate == asOfDate is not overdue. Total amount/count equals the sum of the
overdue and not-yet-due buckets. No matching invoices returns all numeric zeros.

Responses: 200 report; 400 invalid filters; 401 unauthenticated; 403 Customer or
Manager requesting another owner's property; 404 missing property. Caller-supplied
ownerId cannot change the authenticated scope.

No migrations or writes are performed. Frontend integration and invoice-list
filters are outside this change. Invoice lines are projected in one scoped SQL
query and summed with decimal arithmetic in memory for provider compatibility;
large datasets should move monetary aggregation to PostgreSQL. If partial
payments/refunds are introduced, replace the full-settlement balance calculation
with a payment-ledger balance before using this report with that workflow.
