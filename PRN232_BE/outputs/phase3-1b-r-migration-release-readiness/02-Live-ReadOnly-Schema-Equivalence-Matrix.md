# Phase 3.1B-R — So khớp live schema với migration Maintenance

## Phạm vi và snapshot

Nguồn so sánh: live catalog ở PostgreSQL `17.11`, schema `public`, snapshot `2026-10-19 15:21:03 UTC`; migration nguồn `20261013192052_ReconcileMaintenanceReportsAndLogNullability`, `InitialCreate`, `AppDbContextModelSnapshot` và API/service liên quan. Toàn bộ data access chỉ đếm dòng; không đọc description, URL, invoice text hoặc thông tin cá nhân.

## History và thứ tự migration

Live có bốn history rows, tất cả `ProductVersion=8.0.10`:

1. `20261003142421_InitialCreate`
2. `20261004073837_RenameKhuTroDichVuToPropertyService`
3. `20261013140609_SeedRolesAndAuthSetup`
4. `20261019034905_AddPropertyPrimaryImageUrl`

`20261013192052_ReconcileMaintenanceReportsAndLogNullability` không có trong history dù bảng và phần lớn schema migration đó tạo đã tồn tại. Migration có ID thời gian sớm hơn chưa được ghi, trong khi migration mới hơn đã được ghi; chạy `dotnet ef database update` trực tiếp có nguy cơ tạo lại `MaintenanceReports` và dừng với `42P07 relation already exists`.

## Bảng đối chiếu

| Hạng mục | Migration/model | Live snapshot | Phân loại |
|---|---|---|---|
| `MaintenanceLogs.OldStatus` | InitialCreate yêu cầu NOT NULL; Reconcile làm nullable | `text NULL`; có 1 dòng NULL | Khớp trạng thái sau Up; dữ liệu khiến Down guard thất bại |
| Reports và Images | Reconcile tạo bảng, identity, PK/FK, cột bắt buộc | Hai bảng tồn tại, identity ownership và PK/FK khớp | Khớp cấu trúc chính |
| `MaintenanceReports.RequestId` | Unique index | Unique btree index tồn tại | Khớp |
| `IX_MaintenanceReports_ReportedBy` | Reconcile tạo index btree | Thiếu | Drift thiếu index |
| `IX_MaintenanceReportImages_ReportId` | Reconcile tạo index btree | Thiếu | Drift thiếu index |
`20261013192052_ReconcileMaintenanceReportsAndLogNullability` không có trong history dù bảng và phần lớn schema migration đó tạo đã tồn tại. Migration có ID thời gian sớm hơn chưa được ghi, trong khi migration mới hơn đã được ghi; chạy `dotnet ef database update` trực tiếp có nguy cơ tạo lại `MaintenanceReports` và dừng với `42P07 relation already exists`.
| `CK_MaintenanceLogs_OldStatus` | Không có trong InitialCreate hoặc snapshot | Có, CHECK tập `Pending/Approved/Completed` | Extra live, chưa rõ nguồn tạo |
| `CK_MaintenanceLogs_NewStatus` | Không có trong InitialCreate hoặc snapshot | Có, cùng CHECK set | Extra live, chưa rõ nguồn tạo |
| RLS Reports/Images | Reconcile bật RLS | Bật, `FORCE` off, không có policy | Khớp |
| RLS MaintenanceLogs | Migration/snapshot không quản lý RLS | Bật, `FORCE` off, không có policy | Extra live; cần bảo toàn/xác nhận |
| Secondary indexes MaintenanceLogs | Hai index trên `RequestId`, `ChangedBy` | Đúng hai btree index tồn tại | Khớp |
| Constraint validation/deferrability | PK/FK/check expected validated, non-deferrable | Tất cả 10 constraint Maintenance validated, non-deferrable | Khớp theo live, nhưng 2 check là extra |

## Số liệu tổng hợp live

| Bảng | Dòng |
|---|---:|
| Properties | 3 |
| InvoiceDetails | 25 |
| MaintenanceLogs | 9 |
| MaintenanceReports | 4 |
| MaintenanceReportImages | 5 |

RLS bật trên 24/24 bảng public, không có bảng nào `FORCE ROW LEVEL SECURITY`; số policy public là 0. Role kết nối live không phải superuser, có `BYPASSRLS` và quyền CREATE ở database, đồng thời là owner của bảng Maintenance/history cần thiết. `anon`/`authenticated` có SELECT/INSERT grants trên Reports/Images nhưng không có policy; quyền hiệu dụng còn phụ thuộc RLS-bypass của role, vì vậy không được kết luận chỉ từ grant.

`btree_gist` chưa cài. PostgreSQL liệt kê các bản 1.2–1.7 là trusted; role live không phải superuser nhưng có quyền CREATE ở database. Đây là bằng chứng catalog, không chứng minh Supabase sẽ cho phép cài trong release.

## Down migration

`Down()` kiểm tra `MaintenanceLogs.OldStatus IS NULL` rồi ném exception; live có 1 dòng NULL nên Down sẽ dừng. Nếu điều kiện đó được xử lý thủ công, Down tiếp tục drop `MaintenanceReportImages` và `MaintenanceReports`, làm mất tối thiểu 5 metadata rows và 4 report rows. Không dùng Down làm rollback.
