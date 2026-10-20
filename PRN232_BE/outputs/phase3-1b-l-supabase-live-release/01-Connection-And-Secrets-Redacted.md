# 01 — Connection and secrets (redacted)

**Status: BLOCKED — connection metadata matches the intended project, but server identity was not verified.**

## Configuration found

- PRN232.Api/Program.cs reads ConnectionStrings:DefaultConnection.
- API UserSecretsId is prn232-be-api-secrets. Its secrets file exists, but it has no ConnectionStrings:DefaultConnection key.
- ConnectionStrings__DefaultConnection exists in the Windows User environment and matches the current process value. Environment configuration takes precedence. No second key was created and no existing value was changed.
- Base appsettings.json contains a local template. appsettings.Development.json exists; its value is not reproduced here.
- The environment value was parsed in memory only. Host, port, database, and username match the supplied PRN232 shared session pooler: aws-0-ap-southeast-1.pooler.supabase.com:5432, database postgres, project-scoped username for qvdrjyecaprfpgvbfyfe. Password value is not printed or saved.
- Connection mode found: shared session pooler, port 5432. Direct endpoint support was not tested.

## TLS and identity

The stored connection uses SslMode=Require and TrustServerCertificate=true. This encrypts traffic but bypasses certificate and hostname verification. An in-memory read-only connection attempt changed only its temporary builder to VerifyFull / TrustServerCertificate=false; TLS certificate validation failed before a database query ran. The local setting was not modified.

Supabase documents that require does not verify the server certificate; verify-full requires the project CA certificate from Database Settings. The Dashboard redirected to sign-in before that certificate could be obtained. See [Supabase SSL enforcement](https://supabase.com/docs/guides/platform/ssl-enforcement) and [connecting to Postgres](https://supabase.com/docs/guides/database/connecting-to-postgres).

**Consequences:** current_database(), current_user, live schema/history, and project identity have not been verified by SQL in this run. Host/user metadata is configuration evidence only. Password rotation status is also unverified.

## Gate

**BLOCKED.** Before any live write, obtain the project CA certificate through the owner's authenticated Dashboard flow (do not send a password), then verify a read-only VerifyFull connection. Confirm separately that the credential in the existing User environment was rotated as required by the attached runbook.