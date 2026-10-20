# Phase 3.1B — PostgreSQL migration tests và regression

## Test target và an toàn

PostgreSQL 17.11 riêng trên loopback 127.0.0.1:55432; không phải Supabase. Migration test tạo database ngẫu nhiên cho clean upgrade và live-like fixture, kiểm tra server/version/read-write mode, sau đó DROP DATABASE WITH (FORCE) đúng các DB test của nó. Public browsing integration dùng DB fixture riêng với interceptor ép session READ ONLY. Không dùng live connection cho test.

## Lệnh và kết quả

- dotnet restore PRN232_BE.sln --verbosity minimal — thành công, packages up-to-date.
- dotnet build PRN232_BE.sln --no-restore --verbosity minimal — thành công, 1 warning CS1998 tại PRN232.Infrastructure/Repositories/RoleRepository.cs:35 (file không thuộc Phase 3.1B), 0 error.
- dotnet-ef migrations has-pending-model-changes --project PRN232.Infrastructure --startup-project PRN232.Api --context AppDbContext --no-build — “No changes have been made to the model since the last migration.”
- dotnet test PRN232_BE.sln --no-restore --verbosity quiet, có cấu hình hai connection string loopback test-only — Passed 200, Failed 0, Skipped 0. TRX: %TEMP%/prn232-phase31b-final-postsnapshot/phase31b-final-postsnapshot.trx. Trước khi cấp fixture variables, suite có 5 PostgreSQL integration tests skipped; lượt cuối đã cấp cả hai DB test và tất cả 200 test chạy.

## Coverage Phase 3.1B quan sát được

- Clean upgrade đến migration rates; property cũ Description NULL.
- PropertyAmenities: hai property dùng chung Amenity; duplicate và orphan bị chặn.
- PropertyServiceRates: cùng Service có giá riêng theo Property; kiểm thử 5 code/basis pairs cho sáu chế độ; zero cho phép, giá âm/mã sai/ngày giữa tháng/range sai bị CHECK chặn.
- Period liền nhau và property khác nhau hợp lệ; overlap/open-ended và race ghi đồng thời bị exclusion constraint chặn SQLSTATE 23P01.
- Migration 3 giữ 25 legacy invoice lines tổng hợp với Description/Amount nguyên vẹn, cột snapshot NULL; dòng mới lưu Quantity phân số và snapshot; thay rate không làm đổi snapshot. FK chặn mismatch và xóa rate đang tham chiếu.
- RLS bật trên bảng mới; không có policy mới.
- Live-like fixture xác nhận EF Migrate tổng thể dừng với 42P07 do Maintenance pending migration tạo bảng đã tồn tại; range scripts Phase 3.1B áp được vào fixture mà không reconcile Maintenance.

## Regression và giới hạn

Toàn bộ solution test xanh; regression gồm Auth/Maintenance/public browsing hiện có. Không có API/FE/Billing implementation trong diff Phase 3.1B. Test chứng minh behavior ở PostgreSQL test cluster; không chứng minh migration chạy trên Supabase, quyền CREATE EXTENSION thực tế ở live, restore production hay runtime deployment.

SQL diff review: SQL_REVIEW/. Phase-only source diff: 12-Phase3-1B-Only-Diff.patch.