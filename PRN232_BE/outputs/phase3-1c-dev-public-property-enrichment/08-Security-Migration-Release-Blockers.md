# Security, migration và release blockers

## Schema và migration

Phase 3.1B-R snapshot read-only 2026-10-19 15:21:03 UTC ghi nhận live chưa có Description, PropertyAmenities, PropertyServiceRates và invoice snapshot; Gate B/C/D/E chưa được phê duyệt/chạy live. Phase 3.1C-DEV không kết nối live và không chạy SQL/migration trên live. Giữ feature false cho đến khi migration được duyệt, áp dụng và có postflight riêng.

Gate B còn quyết định CreatedAt default CURRENT_TIMESTAMP, hai status CHECK bổ sung và MaintenanceLogs RLS. C/D/E cần review đúng hash, preflight READ ONLY, postflight, cửa sổ writer và owner/DBA approval. Không dùng replay local làm bằng chứng release live.

## Backup và extension

Báo cáo B-R chưa có bằng chứng account-level về Supabase plan, backup/PITR retention/recovery point hoặc restore drill. Cần backup evidence và restore drill độc lập. btree_gist chưa cài trên live; local PostgreSQL superuser cài được không xác nhận quyền hoặc quy trình Supabase.

## Security/RLS

B-R ghi nhận RLS bật nhưng chưa có policies trên các bảng public, quyền effective của runtime/API chưa được xác minh; security audit còn anonymous POST /api/roles, nhiều GET ngoài allow-list và thiếu owner scoping. Không thay đổi Auth/RLS trong phase này. Trước bật API enriched phải xác minh principal API có quyền đọc tables trong khi không mở public write hoặc bỏ RLS.

## Compatibility blocker trên schema cũ

Public GET matrix với feature false qua provider test. Tuy vậy PropertyRepository.GetEntityByIdAsync materialize entity đầy đủ và phát SELECT cột Description không tồn tại trên legacy schema (SQLSTATE 42703). API write/update/delete path cần được kiểm tra/remediate và duyệt riêng trước khi triển khai source mới lên môi trường cũ. Không tự đổi contract hoặc entity trong phase này.

## Service price và FE

Public room detail/management consumers tiếp tục dùng Services.Price; property detail mới dùng PropertyServiceRates. Do có thể khác giá, không bật enrichment ngoài test cho đến khi có consumer-transition/FE labeling approval. FE vẫn chưa đọc field mới.

Các chi tiết migration và release runbook tiếp tục theo reports Phase 3.1B-R; không có approval gate nào được đóng bởi các test local trong lượt này.