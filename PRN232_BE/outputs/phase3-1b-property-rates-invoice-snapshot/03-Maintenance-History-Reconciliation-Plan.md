# Phase 3.1B — Kế hoạch reconcile Maintenance migration (chỉ kế hoạch)

## Trạng thái

**BLOCK LIVE APPLY.** Source migration 20261013192052_ReconcileMaintenanceReportsAndLogNullability chưa có trong live __EFMigrationsHistory, còn migration 20261019034905_AddPropertyPrimaryImageUrl đã có. Live đã có MaintenanceReports (4 rows) và MaintenanceReportImages (5 rows). Không INSERT migration history và không chạy Up/Down trên live.

## Up/Down trong source và tình trạng live

Up của migration Maintenance thay đổi nullability OldStatus trên MaintenanceLogs; tạo MaintenanceReports và MaintenanceReportImages cùng khóa/index/foreign key/RLS như source. Catalog live xác nhận OldStatus nullable và hai bảng cùng dữ liệu tồn tại, nhưng thiếu index IX_MaintenanceReports_ReportedBy và IX_MaintenanceReportImages_ReportId. MaintenanceReports.CreatedAt có CURRENT_TIMESTAMP live nhưng không có default trong source migration/model snapshot. Cần so sánh từng cột, kiểu, nullability, default, PK, FK, unique/check constraint, index definition và RLS trước kết luận tương đương.

Down xóa hai bảng Maintenance. Vì chúng có 4 và 5 dòng live, Down có nguy cơ phá dữ liệu trực tiếp; không dùng làm rollback.

## Reproduction an toàn trên PostgreSQL test

Fixture riêng tạo schema/history tương tự: migration Maintenance bị bỏ khỏi history, bảng Maintenance đã tồn tại (với live-like default/index drift), và migration ảnh property mới hơn đã được áp. Gọi EF Migrate chuẩn thất bại SQLSTATE 42P07 do migration cũ thử CREATE TABLE đã tồn tại. Test DB riêng được xóa sau test. Không kết nối Supabase trong bước này.

## Kế hoạch forward-only cần duyệt riêng

1. Lập backup/PITR và thử restore có xác nhận trước thay đổi.
2. Chạy lại preflight chỉ đọc, lưu định nghĩa đầy đủ từng object và row count aggregate.
3. Xác nhận hai index thực sự thiếu về định nghĩa, không chỉ khác tên; nếu cần, review riêng lệnh CREATE INDEX CONCURRENTLY. Lệnh này phải chạy ngoài transaction và có kế hoạch kiểm tra index INVALID/cleanup.
4. Quyết định có giữ default CURRENT_TIMESTAMP hay thay đổi bằng migration forward-only; kiểm tra ảnh hưởng ghi Maintenance.
5. Chỉ sau khi schema khớp về mọi object, DBA/owner duyệt quy trình reconcile metadata có điều kiện và ghi nhận chính xác version. Không cung cấp hoặc thực thi INSERT vào __EFMigrationsHistory trong gói này.
6. Xác minh bằng preflight sau thay đổi và test restore; sau đó mới đánh giá riêng Gate C.

## Điều kiện STOP

Dừng nếu có sai khác không giải thích được về dữ liệu/constraints/RLS, index trùng hoặc invalid, default chưa được owner quyết định, backup/restore chưa đạt, migration history khác dự kiến, hay preflight không còn READ ONLY. Không chạy EF database update trên live. Không chỉnh lịch sử migration để “làm xanh” khi chưa chứng minh schema tương đương.

SQL_REVIEW/00 chỉ là SELECT trong transaction READ ONLY; các lệnh index xuất hiện dưới dạng comment để review, không được chạy bởi file. Kế hoạch này không phải phê duyệt triển khai.