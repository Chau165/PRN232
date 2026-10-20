# Runbook triển khai có approval — Gate B/C/D/E

**Không thực hiện trong Phase 3.1B-R.** Chỉ dùng sau khi tất cả approval trong report 05 có bằng chứng và owner đã duyệt đúng file/hash trong manifest.

## Trước cửa sổ

1. Chọn người chịu trách nhiệm/rollback và cửa sổ downtime; thông báo backend/API consumers và pause các writer ảnh hưởng Maintenance/Invoice.
2. Xác minh plan, restore point/PITR, restore drill và backup Storage riêng nếu có ảnh.
3. Chốt ba owner decisions: giữ CreatedAt default; giữ CHECK constraints; giữ RLS MaintenanceLogs. Xác nhận security owner chấp nhận zero-policy RLS và cách backend truy cập.
4. DBA xác nhận `btree_gist` có thể được bật trên project Supabase và có quyền đúng. Không lấy kết quả local superuser làm bằng chứng quyền live.
5. Lưu output preflight READ ONLY, thời gian UTC, history IDs, row counts và index/constraint definitions. Nếu khác reviewed state, dừng.

## Triển khai tuần tự

1. **Gate B:** chạy candidate SQL đã phê duyệt đúng SHA-256 bằng `psql` autocommit; không thêm option single-transaction. Guard phải pass. Hai `CREATE INDEX CONCURRENTLY` chạy riêng; kiểm tra valid/ready. History marker chỉ được ghi bởi transaction cuối script.
2. **Gate C:** áp dụng đúng `01-AddPropertyDescriptionAndAmenities.sql` đã duyệt. Xác nhận commit và history row.
3. **Gate D:** áp dụng đúng `02-AddPropertyServiceRates.sql`. Xác nhận extension tồn tại, exclusion constraint và history row.
4. **Gate E:** áp dụng đúng `03-AddInvoiceDetailBillingSnapshot.sql`. Xác nhận columns/FKs/indexes và history row.
5. Chạy `02-Postflight-READ-ONLY.sql` trong READ ONLY transaction; đối chiếu counts với preflight, history IDs, hashes/backups, RLS, extension và index validity.
6. Sau khi DBAs chấp nhận postflight mới tiếp tục khởi động lại backend/canary phù hợp; kiểm tra health/API logs. Đây không nằm trong scope hiện tại và cần approval/release plan riêng.

## Dừng / phục hồi

- Bất kỳ precondition nào sai: dừng trước DDL; không sửa live cho khớp script.
- Gate B index concurrent thất bại: dừng toàn bộ; kiểm tra invalid indexes trong catalog, không auto-drop, xin DBA quyết định.
- C/D/E transaction thất bại: giữ nguyên migration vừa commit trước đó; không chạy lại file đã commit, không chạy `Down()`, không ghi history tay. Thu thập trạng thái và soạn forward recovery SQL riêng để review.
- Nếu recovery cần backup/PITR: restore drill/approval và downtime owner phải được xác nhận. Restore có thể bỏ các write hợp lệ sau điểm khôi phục.
- Chỉ đóng release khi postflight, health check, API smoke tests được người có quyền chấp nhận. Smoke/visual test không thể được thay bằng EF history.
