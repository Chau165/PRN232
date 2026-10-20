# 06 — Live postflight and integrity

**Status: NOT RUN — no live gate completed.**

No live postflight can be compared with a fresh preflight because the preflight was blocked before SQL execution. Live row counts/fingerprints, all eight EF migration-history rows, constraints, index validity/readiness, btree_gist, RLS/policies, and preservation of Invoice/Maintenance data are NOT VERIFIED in this run.

There were no live DML or DDL statements, so this run made no change to InvoiceDetails, Maintenance*, Booking, Contract, users, or any other live table. This is not a postflight integrity claim.

The read-only verification script is delivered at SQL_REVIEW/Live-ReadOnly-Verification.sql. Run it only after a verified fresh preflight and successful owner-authorized rollout.