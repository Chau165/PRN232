# Room image upload

## Configuration

The backend uses the existing server-only `Supabase:Url` and
`Supabase:ServiceRoleKey` settings. `Supabase:RoomBucket` defaults to
`room-images`. Never expose the service role key in the frontend.

Bucket `room-images` was created in PRN232_DB on 2026-11-11 with:

- Public reads for rental listing photos.
- Maximum file size: 5 MiB (5,242,880 bytes).
- MIME types: `image/jpeg`, `image/png`, `image/webp`.
- No anonymous or authenticated write policy; writes go through the backend.

For another environment, create a bucket with the same configuration using
the [Supabase bucket setup documentation](https://supabase.com/docs/guides/storage/buckets/creating-buckets).
No change to the existing `RoomImages` table is required.

## API

| Method | Route | Body / behavior |
| --- | --- | --- |
| POST | `/api/rooms/{id}/images/upload` | Multipart fields `file` and optional `isPrimary` |
| GET | `/api/rooms/{id}/images` | Existing image gallery |
| PUT | `/api/rooms/{id}/images/{imageId}/primary` | Set the primary image; no request body |
| DELETE | `/api/rooms/{id}/images/{imageId}` | Remove the image and its storage object if it is not reused |

The existing JSON `POST /api/rooms/{id}/images` remains available for URL-based
clients. All image writes require Admin or a Manager assigned to the property's
rooms. The first uploaded image becomes primary automatically. Deleting the
primary image promotes the oldest remaining image.

Uploads validate size, filename extension, MIME type and file signature before
contacting Storage. A database save failure triggers compensating object cleanup.
Transient connection failures, timeouts and HTTP 408/429/500/502/503/504 responses
are retried up to three total attempts with a short backoff. Retries reuse the
same random object path and content, including when the first write succeeded
but its response was lost. Permission and validation failures are not retried.
Storage logs record HTTP status or connection error type without credentials.
Storage failures return HTTP 502; validation errors return 400, out-of-scope
Manager writes return 403, and missing rooms/images return 404.

Public room cards and details expose only canonical public URLs from the
configured project's room bucket with server-generated image filenames. Signed
URLs and legacy unverified URLs are omitted. Property cards keep using their
existing property images.

## UI

Open **Quản lý → Khu trọ → Phòng → Xem → Ảnh phòng**. Select a file, inspect the
preview, optionally choose **Đặt làm ảnh đại diện**, then select **Tải ảnh lên**.
The gallery also supports choosing a primary image and deleting an image.
For a new room, create it first, then open **Xem** to add its photos.

## Verification

- `RoomImagesApiTests`: multipart binding, JWT roles, assigned Manager scope,
  validation, storage failure handling, primary selection, deletion, reused
  URLs and public image responses, using an isolated test database.
- `SupabaseRoomImageStorageTests`: storage requests, random object names,
  foreign/private/noncanonical URL rejection and storage errors.
- FE `tests/roomImagesManager.test.tsx`: authenticated multipart upload,
  preview, validation, primary selection, deletion and retry after failure.

The local app was also checked against the live bucket by uploading a temporary
PNG, reading it publicly and verifying the public room card/detail responses.
Temporary verification images were removed afterward.
