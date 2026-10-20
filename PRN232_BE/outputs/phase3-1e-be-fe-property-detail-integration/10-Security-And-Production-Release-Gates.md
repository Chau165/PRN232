# Security and production release gates

## Verified in this phase

- Public property/room GET routes remain anonymous by design and return allowlisted public DTO fields; no Auth or management endpoint was changed.
- `PropertyAmenities` and `PropertyServiceRates` have RLS enabled, `FORCE ROW LEVEL SECURITY` is false, and each table currently has zero policies.
- The live API database principal is not a superuser but has `BYPASSRLS=true`. This explains why the local anonymous API smoke could read the rows despite the absence of policies. Credentials and role name are intentionally omitted.
- The API connection used TLS `VerifyFull`; server certificate trust bypass was false.

## Production gates

1. **Least privilege/RLS:** review the production API database principal and public-table exposure. Prefer a least-privilege runtime role or an explicitly reviewed policy design; this phase did not add policies, disable RLS, or change credentials.
2. **Feature flag:** enrichment is false when `PublicBrowsing:PropertyDetailEnrichmentEnabled` is missing/invalid. The local smoke enabled it process-locally. Verify the intended production environment has the flag enabled after the current schema and access path are approved.
3. **Runtime and visual sign-off:** production deployment was not performed; desktop/tablet visual UAT and browser-console diagnostics were not available in this run.

Do not interpret successful API GETs through a `BYPASSRLS` application principal as proof that anon/authenticated database roles have safe direct access. No production-ready claim is made.
