# Phase 3.1C-DEV — Tóm tắt và trạng thái

**COMPLETE — PHASE 3.1C API DEV READY; LIVE MIGRATION AND RELEASE BLOCKED**

## Đã làm

Đã đọc CODING_STANDARDS.md, toàn bộ báo cáo Phase 3.1A, 3.1B và 3.1B-R, cùng contract source BE/FE trước khi sửa.

- Mở rộng GET /api/public/properties/{id} bằng Description, commonAmenities và livingCosts; giữ nguyên toàn bộ field hiện tại, roomAmenities, status code và quy tắc phòng có thể thuê.
- Có feature flag PublicBrowsing:PropertyDetailEnrichmentEnabled, mặc định false khi thiếu hoặc sai giá trị cấu hình. Khi tắt, code dùng truy vấn cũ và không truy vấn Description hay bảng mới.
- Rates lấy theo PropertyId, ServiceId và ngày hiệu lực Việt Nam; không fallback sang Services.Price.
- Tạo kiểm thử PostgreSQL thật cho schema đã migrate và schema trước phần Description/amenities/rates. Không chỉnh FE, migrations, entity/DbContext, Auth, Maintenance, Billing hay Booking.
- Không kết nối Supabase live trong lượt này. Dựa trên snapshot read-only Phase 3.1B-R ngày 2026-10-19 15:21:03 UTC để xác định schema live cũ. Không có lệnh live nào được chạy.

## Bằng chứng

| Kiểm tra | Kết quả |
|---|---|
| dotnet build PRN232_BE.sln --no-restore | Thành công, 0 warning, 0 error |
| dotnet ef migrations has-pending-model-changes | Không có thay đổi model kể từ migration cuối |
| Full regression: dotnet test PRN232_BE.sln --no-build --no-restore | 203 passed, 0 failed, 0 skipped |
| PostgreSQL provider | PostgreSQL 17.10 loopback; migrated fixture và legacy schema fixture; API connections READ ONLY |
| Phase 3.1C public API + PostgreSQL tests | 50 passed, 0 failed, 0 skipped |
| Supabase live writes/migrations | Không thực hiện |

## Còn chặn release

1. Supabase live chưa có schema Phase 3.1B theo snapshot đã được audit; không bật enrichment trước khi Gate B/C/D/E được phê duyệt, áp dụng và hậu kiểm riêng.
2. Một số management write path tải full Property entity; trên schema cũ PostgreSQL trả SQLSTATE 42703 do thiếu Description. Không sửa contract/path này trong Phase 3.1C-DEV. Pre-migration deployment cần remediation/approval riêng.
3. Public room detail và RoomType services còn đọc Services.Price, trong khi property detail enrichment đọc PropertyServiceRates. Giữ feature tắt ngoài test cho đến khi có quyết định chuyển consumer và nhãn FE minh bạch.
4. FE chưa khai báo các field mới và vẫn hiện placeholder; code API không đồng nghĩa trang đã hiển thị dữ liệu.
5. Các gate về backup/restore, btree_gist, RLS/Auth và phê duyệt migration trong Phase 3.1B-R vẫn mở.

Chi tiết theo từng chủ đề nằm trong các báo cáo 02–09. Phase-only diff là báo cáo 10.