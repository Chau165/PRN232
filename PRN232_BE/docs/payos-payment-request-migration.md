# payOS B3: PaymentRequests migration

Migration: `20261110164307_AddInvoicePaymentRequests` (generated with dotnet ef).

Creates PaymentRequests only; existing Payments/Invoices/Users rows are unchanged.
Adds restricted foreign keys to InvoiceId, CustomerId and optional PaymentId.
Unique PaymentId prevents linking one settlement to multiple requests. Cash and
legacy payments remain valid without a request.

Unique OrderCode and non-null PaymentLinkId prevent duplicate provider identifiers.
The partial unique invoice index covers Creating, Pending and RequiresReview.
An Admin must resolve an uncertain receipt before issuing another active request.
Cancelled/Expired/Create-failed historical attempts do not block a new request.
Multiple null provider links are allowed during creation.

Checks enforce positive amount/order code, VND, supported statuses (including
exactly Create-failed), UpdatedAtUtc >= CreatedAtUtc and optional expiry later
than creation. A plain expiry timestamp does not release the active index: later
workflow code must explicitly transition an expired request before replacement.

The schema does not yet enforce invoice/customer consistency, provider signature
validation, full settlement or the entire transition workflow. Those are later
application steps. Unique identifiers do not by themselves implement webhook
idempotency or protect against all payment races.

## Apply

Configure the intended connection using User Secrets/environment without printing
credentials. Review pending migrations first:

```powershell
dotnet ef migrations list --project PRN232.Infrastructure --startup-project PRN232.Api
dotnet ef database update 20261110164307_AddInvoicePaymentRequests --project PRN232.Infrastructure --startup-project PRN232.Api
```

Run from PRN232_BE. This change does not automatically execute migrations at API
startup. No database update was performed for B3: the inspected configuration
points to Supabase, while no local connection is configured in appsettings.json.

Down drops PaymentRequests and its indexes/constraints. It does not delete old
payments, but would delete request/reconciliation history; do not roll back after
real checkouts begin without reviewing that data.

Schema tests use isolated SQLite memory databases to verify index/check/FK
behavior; the snapshot consistency check uses the Npgsql model without connecting
to PostgreSQL. The generated PostgreSQL script is supplied for review. Live
PostgreSQL migration replay is still required in the intended test environment.
