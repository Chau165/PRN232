# payOS B2: invoice payment request model

PaymentRequests describes a checkout attempt, not a received payment. It belongs
to an invoice and customer. Amount/Currency snapshot the intended full payment.
Provider defaults to PayOS. OrderCode is a long; PaymentLinkId/CheckoutUrl may be
null until link creation is confirmed. No credentials are stored in this entity.

Statuses: Creating, Pending, Succeeded, Cancelled, Expired, **Create-failed**,
RequiresReview. `Failed` is not a supported request status. Create-failed describes
a confirmed creation failure; a timeout alone must not be mapped to it. These
constants do not yet implement or enforce workflow transitions.

CreatedAtUtc and UpdatedAtUtc must be supplied by application TimeProvider.
ExpiresAtUtc is nullable while expiry is not yet known. ConfirmedAtUtc records
confirmation time; WebhookReceivedAtUtc records webhook reception separately.
Actual settlement time stays on Payments.PaidAtUtc. ProviderReference and sanitized
LastErrorCode/LastErrorMessage support later reconciliation. Never store raw
credential-bearing responses in diagnostic fields.

PaymentId is an optional one-to-one link to a recorded settlement. Existing/cash
Payments remain valid without a request. Invoice and Customer have request
collections. Relationships restrict cascading deletion. The entity does not
change invoice state, create payments, send notifications or call payOS.

B2 adds the entity, DbSet, basic EF relationships and status constants. **No
migration is generated or applied in this step.** Existing database schema is
unchanged. Do not query the new table against that database until B3 is applied.

B3 must generate/review the migration and finalize database constraints: unique
OrderCode; unique non-null provider link/reference as appropriate; at most one
active request per invoice with an explicit policy for RequiresReview; valid
status/amount/currency/timestamps. Do not use this B2 model alone to claim
idempotency or concurrent-payment protection. API, signature verification,
transition validation and reconciliation are later steps. No checkout lifetime
or automatic refund policy is implemented here.
