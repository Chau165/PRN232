# Supabase property upload diagnostic — 2026-11-11

Root cause: the live `property-images` bucket allowed only `image/png`, while
`PropertyService.ValidateImage` accepts PNG, JPEG and WebP. Storage rejected
JPEG/WebP with `InvalidMimeType`; `SupabasePropertyImageStorage.UploadAsync`
translated that response into the Vietnamese storage-unavailable message.

Changed the live bucket through the authenticated Storage API to allow:

- `image/png`
- `image/jpeg` (`.jpg` and `.jpeg`)
- `image/webp`

Preserved public visibility and the 5 MiB size limit. No application source,
credentials, database schema or business records were changed.
`bucket-before.json` records the previous nonsecret settings.

## Opt-in live regression probe

This debug harness reads the same development JSON, User Secrets and environment
providers as the local API. It uses the real `SupabasePropertyImageStorage`,
uploads temporary image objects, checks their public bytes, then deletes them.
It must only be run against an authorized project. It prints no credentials.

Run from `C:\PRN232` (the JPEG fixture is included in this debug directory):

```powershell
dotnet run --project outputs/debug-supabase-upload/UploadProbe.csproj -- --db --jpeg --webp
```

Before the fix: PNG passed; JPEG and WebP failed with `InvalidMimeType` and
`Dịch vụ lưu trữ ảnh hiện không khả dụng.` JPEG failed on a second run too.

After the fix: database connection through the configured session pooler on port
5432 passed; upload, byte comparison and cleanup passed for all three MIME types.
No property creation or update was needed to reproduce the Storage failure.

Other image formats are still subject to the application's existing PNG/JPEG/WebP
validation. This probe protects the actual Storage integration; mocked unit tests
would not detect a drift in the hosted bucket's MIME restrictions.

Prevention: verify allowed bucket MIME types against application validation when
configuring Storage. API reference:
https://supabase.com/docs/reference/javascript/storage-updatebucket
