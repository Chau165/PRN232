-- P12: read-only audit. Run as the DB administrator, after selecting the correct project/database.
-- No recipient names, bank accounts, credentials or idempotency keys are returned.
-- Expected: zero rows. Each confirmed refund must agree with its incoming receipt and final booking state.
SELECT f."BookingRefundId", f."BookingId", f."BookingRefundRequestId", f."PayoutAttemptId",
       f."Amount", f."TransferredAtUtc", f."RecordedAtUtc"
FROM "BookingRefunds" f
LEFT JOIN "Payments" p ON p."PaymentId" = f."OriginalPaymentId"
LEFT JOIN "BookingRefundRequests" q ON q."BookingRefundRequestId" = f."BookingRefundRequestId"
LEFT JOIN "PayoutAttempts" a ON a."PayoutAttemptId" = f."PayoutAttemptId"
LEFT JOIN "Bookings" b ON b."BookingId" = f."BookingId"
WHERE p."PaymentId" IS NULL OR q."BookingRefundRequestId" IS NULL OR a."PayoutAttemptId" IS NULL
   OR b."BookingId" IS NULL OR p."Status" <> 'Success' OR a."Status" <> 'Succeeded'
   OR q."Status" <> 'Refunded' OR b."Status" <> 'Cancelled'
   OR f."Amount" <> q."ApprovedAmount" OR f."Amount" <> a."Amount" OR f."Amount" > p."Amount"
   OR p."BookingId" IS DISTINCT FROM f."BookingId" OR q."BookingId" <> f."BookingId"
   OR q."OriginalPaymentId" <> f."OriginalPaymentId" OR a."BookingRefundRequestId" <> f."BookingRefundRequestId"
   OR f."Currency" <> 'VND';

-- Expected: zero rows. Unique DB constraints also enforce these invariants.
SELECT "OriginalPaymentId", COUNT(*) FROM "BookingRefunds" GROUP BY "OriginalPaymentId" HAVING COUNT(*) > 1;
SELECT "BookingRefundRequestId", COUNT(*) FROM "BookingRefunds" GROUP BY "BookingRefundRequestId" HAVING COUNT(*) > 1;

-- Bank time unknown: these are reported separately, not allocated using local confirmation time.
SELECT "BookingRefundId", "BookingId", "Amount", "RecordedAtUtc"
FROM "BookingRefunds" WHERE "TransferredAtUtc" IS NULL;
