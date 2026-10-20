# Phase 3.1B — Preflight live chỉ đọc và độ lệch EF

**Thời điểm:** 2026-10-19. Các con số dưới đây là kết quả preflight đã chạy lại trong transaction READ ONLY; không chứa hàng dữ liệu cá nhân hay thông tin kết nối.

## Bảo đảm chỉ đọc

Kết nối bằng cấu hình an toàn đã có trong môi trường. Transaction dùng REPEATABLE READ READ ONLY, đặt statement_timeout 10 giây, đọc current_setting('transaction_read_only') = on và kết thúc ROLLBACK. Không chạy migration/DDL/DML trên Supabase; không thử cài extension và không ghi __EFMigrationsHistory.

Database trả PostgreSQL 17.11. Có 24 bảng public; RLS bật trên 24/24 và không có policy ở các bảng được kiểm tra. Không liệt kê nội dung nhạy cảm.

## History và catalog

Các migration ID quan sát được trong live history:

- 20261003142421_InitialCreate
- 20261004073837_RenameUserToCustomerInMaintenance
- 20261013140609_SeedRoles
- 20261019034905_AddPropertyPrimaryImageUrl

20261013192052_ReconcileMaintenanceReportsAndLogNullability có trong source nhưng không có trong live history. Migration AddPropertyPrimaryImageUrl mới hơn đã được ghi nhận ở live, vì vậy EF có thể xem migration Maintenance cũ là pending trước migration mới.

Bảng và row count aggregate đã kiểm tra: Properties 3; InvoiceDetails 25; MaintenanceReports 4; MaintenanceReportImages 5. Live chưa có Properties.Description, PropertyAmenities, PropertyServiceRates hoặc InvoiceDetails.ServiceId.

## Drift Maintenance đã xác nhận

MaintenanceLogs.OldStatus đang nullable như mục tiêu migration. Hai bảng MaintenanceReports và MaintenanceReportImages tồn tại và có dữ liệu. Hai index dự kiến IX_MaintenanceReports_ReportedBy và IX_MaintenanceReportImages_ReportId không có theo tên/định nghĩa đã đối chiếu. MaintenanceReports.CreatedAt có default CURRENT_TIMESTAMP ở live nhưng migration/snapshot nguồn không khai báo default. RLS đang bật, không có policy.

Không thể coi đây là migration đã reconcile: khác biệt cần đối chiếu toàn bộ PK/FK/check/index/RLS và quyết định owner về default trước bất kỳ thay đổi nào. Down của migration sẽ xóa bảng Maintenance có dữ liệu.

## Extension PostgreSQL

btree_gist chưa được cài live. pg_available_extension_versions cho thấy extension hiện có trong server và trusted; role kiểm tra có database CREATE. Đây chỉ là catalog/permission evidence. Không xác nhận CREATE EXTENSION thành công trên Supabase vì tuyệt đối không thử ghi. Khả năng vận hành live vẫn cần xác minh trong cửa sổ được phê duyệt.

## Model và source

EF Core/Npgsql 8.0.10. dotnet-ef migrations has-pending-model-changes trả về “No changes have been made to the model since the last migration.” Điều này xác nhận snapshot khớp runtime model tại thời điểm chạy, không xác nhận migration history live đã đồng bộ.

Migration scripts cục bộ được tạo theo range sau AddPropertyPrimaryImageUrl, tránh đưa migration Maintenance cũ vào SQL Phase 3.1B. Replay trên fixture live-like xác nhận EF Migrate tổng thể vẫn gặp SQLSTATE 42P07 khi migration Maintenance pending cố CREATE bảng đã tồn tại. Không chạy lệnh đó trên live.

## Bằng chứng chi tiết

SQL preflight không ghi: SQL_REVIEW/00-Maintenance-Reconciliation-Preflight-DRAFT-READ-ONLY.sql.  
So sánh migration: 03-Maintenance-History-Reconciliation-Plan.md.  
Snapshot Phase 3.1A gốc: outputs/phase3-1a-supabase-audit/.