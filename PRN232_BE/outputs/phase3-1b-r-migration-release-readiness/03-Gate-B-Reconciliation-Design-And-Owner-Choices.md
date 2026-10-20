# Gate B — Phương án reconcile Maintenance

## Mục tiêu

Đưa EF migration history về trạng thái nhất quán với schema live đã có, đồng thời chỉ bổ sung đúng hai index bị thiếu. Không chạy lại `Up()` vì các bảng đã tồn tại; không dùng `Down()` vì migration có thể từ chối do `OldStatus NULL` và sau đó drop dữ liệu report/image.

## Trình tự ứng viên

File review-only: `SQL_REVIEW/01-Gate-B-Maintenance-Reconciliation-REVIEW-ONLY.sql`.

1. Chạy precondition read-only trong cùng database/schema, so history, toàn bộ cột/identity, PK/FK/CHECK, default, ownership, RLS và index definitions với snapshot đã review.
2. Nếu có bất kỳ khác biệt nào, abort trước DDL. Không tự dọn index thay thế, constraint khác tên hoặc index invalid.
3. Tạo riêng `IX_MaintenanceReports_ReportedBy` và `IX_MaintenanceReportImages_ReportId` bằng `CREATE INDEX CONCURRENTLY` ở autocommit, ngoài transaction.
4. Xác minh hai index có định nghĩa chính xác, `indisvalid` và `indisready`.
5. Chỉ sau khi mọi guard đúng, thêm history marker `20261013192052_ReconcileMaintenanceReportsAndLogNullability` trong transaction cuối.

Script mặc định fail-closed. Nó yêu cầu `psql -v phase31br_gate_b_approved=true`; biến này không thay thế phê duyệt bên ngoài. Nếu một build `CONCURRENTLY` thất bại, PostgreSQL có thể để lại index invalid; dừng, kiểm tra catalog và xin quyết định cleanup riêng. Không tự drop index.

## Quyết định owner/DBA cần ký

### 1. `CreatedAt DEFAULT CURRENT_TIMESTAMP`

- **Khuyến nghị cho reconcile:** giữ nguyên default live. `MaintenanceService` hiện gán `CreatedAt` rõ ràng trước insert; giữ default tương thích với writer khác và không cần rewrite migration lịch sử.
- Migration/snapshot không khai báo default. Nếu quyết định bỏ default, phải có bằng chứng tất cả writer luôn cung cấp timestamp và phải dùng thay đổi forward-only được review; không sửa migration cũ.
- Candidate hiện chỉ hỗ trợ lựa chọn **KEEP** và sẽ abort nếu default không còn khớp.

### 2. Hai CHECK status trên `MaintenanceLogs`

Migration `InitialCreate` và `AppDbContextModelSnapshot` không chứa CHECK này; live có hai constraint đã validated. Nguồn tạo chưa xác định. Khuyến nghị giữ nguyên để tránh nới lỏng dữ liệu hợp lệ; owner/DBA phải xác nhận đây là rule được chấp nhận. Candidate yêu cầu đúng tên/định nghĩa và đúng tổng số constraint; nó không sửa hai constraint.

### 3. RLS của `MaintenanceLogs`

Live bật RLS, không FORCE và không có policy; source migration không tạo cấu hình này. Candidate kiểm tra/bảo toàn trạng thái hiện có. Trước khi duyệt, xác nhận vai trò ứng dụng và quyền đọc/ghi kỳ vọng; không thêm policy trong phase này.

## Không được làm

- Không chạy migration Up/Down hoặc `dotnet ef database update` để reconcile live.
- Không ghi history marker chỉ để khiến EF bỏ qua lỗi `42P07` nếu các precondition không khớp.
- Không xóa/backfill `OldStatus NULL`; không update/drop report, image, check constraint hay RLS.
- Không chạy script review trên Supabase trước khi owner/DBA phê duyệt đúng hash, backup/restore evidence, cửa sổ triển khai và lệnh đích.
