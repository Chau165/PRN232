# 03 — Fresh live preflight và exact hashes

**Preflight read-only: PASS. Migration eligibility: BLOCKED.**

Các statement trong `00-Live-READ-ONLY-Preflight.sql` chạy qua Npgsql, transaction REPEATABLE READ READ ONLY, kết thúc ROLLBACK. Metadata regclass được cast sang text khi đọc; không lấy giá trị nghiệp vụ/PII.

## Snapshot

- Host/project username khớp project ref `qvdrjyecaprfpgvbfyfe`; DB `postgres`; role `postgres`; PostgreSQL 17.11.
- EF history có 4 dòng: `20261003142421_InitialCreate`, `20261004073837_RenameKhuTroDichVuToPropertyService`, `20261013140609_SeedRolesAndAuthSetup`, `20261019034905_AddPropertyPrimaryImageUrl`.
- `Properties.Description`, `PropertyAmenities`, `PropertyServiceRates` chưa có; 6 snapshot columns của `InvoiceDetails` chưa có.
- Exact counts: Properties 3; InvoiceDetails 25; MaintenanceLogs 9; MaintenanceReports 4; MaintenanceReportImages 5.
- `MaintenanceReports.CreatedAt` default CURRENT_TIMESTAMP; MaintenanceLogs có CHECK OldStatus và NewStatus.
- RLS bật, không có policy trên 5 bảng trong preflight. Indexes hiện có: MaintenanceLogs PK/ChangedBy/RequestId; MaintenanceReports PK/unique RequestId; MaintenanceReportImages PK. Hai Gate B index chưa có.
- `btree_gist` chưa cài; versions 1.2–1.7 báo trusted. Role không superuser, có BYPASSRLS và database CREATE privilege; chưa thử CREATE EXTENSION.
- `reltuples=-1` nên bỏ qua estimated counts. Booking/Contract/Invoice aggregate chưa được truy vấn.

## SHA-256 — khớp manifest

| Gate | SHA-256 |
|---|---|
| B | `d3dad4a20ab58aad4fbc374aa52a0508bd977f13ee5530475165d3104a1c0701` |
| C | `7390649de04ed4e2b2220ac3b8ae0ac6a6e9f414feb41d3911ba51727d87d33a` |
| D | `8247c5208f02095cd288e4aa1c271b6e20f041a6c92712ccba3791ae759a509e` |
| E | `225902a76a91ed7f548318fc60efeaf14204ed634d91266d6155992c9d6cbc85` |

SQL không bị sửa và migration history không bị ghi.

## Tình trạng sau thay đổi credential — 2026-10-20

Preflight và SHA-256 ở trên được thu trước lần cập nhật credential. Kết nối VerifyFull mới từ Windows User Environment hiện bị từ chối với SQLSTATE `28P01`; vì vậy không thể tái xác nhận migration history/schema sau lần đổi password. Các số liệu preflight là snapshot lịch sử, không phải post-change verification.

## Fresh connection sau cập nhật — 2026-10-20

Xác minh lại bằng Npgsql builder từ Windows User Environment: target và TLS VerifyFull hợp lệ, nhưng `Open()` nhận SQLSTATE `28P01`. Không có transaction/query nào bắt đầu trong lần kiểm tra này. Snapshot schema/history bên trên không được refresh; vẫn không có backup, restore, rehearsal hay migration live.

## Fresh live preflight sau khi xác thực thành công — 2026-10-20

Preflight chạy trong REPEATABLE READ READ ONLY, với các kiểu `regclass` cast sang text trong bộ nhớ; statement cuối ROLLBACK. Kết quả:

- Database `postgres`, schema `public`, PostgreSQL `17.11`, role `postgres`, timezone `UTC`.
- EF history vẫn có 4 migrations: `20261003142421_InitialCreate`, `20261004073837_RenameKhuTroDichVuToPropertyService`, `20261013140609_SeedRolesAndAuthSetup`, `20261019034905_AddPropertyPrimaryImageUrl`.
- Counts: Properties 3; InvoiceDetails 25; MaintenanceLogs 9; MaintenanceReports 4; MaintenanceReportImages 5.
- `Properties.Description`, `PropertyAmenities`, `PropertyServiceRates` chưa có; invoice snapshot column count là 0.
- RLS bật, policy count 0 trên năm bảng được kiểm tra. Maintenance indexes/constraints khớp snapshot trước: indexes hiện có cho Logs/Reports/Images; MaintenanceLogs có hai status CHECK và các FK/PK được validate.
- `btree_gist` chưa cài. Role postgres không superuser, có BYPASSRLS và CREATE database privilege.
- Không lấy dữ liệu nghiệp vụ; không có write.

Gate SQL SHA-256 được tính lại và khớp manifest: B `d3dad4a20ab58aad4fbc374aa52a0508bd977f13ee5530475165d3104a1c0701`; C `7390649de04ed4e2b2220ac3b8ae0ac6a6e9f414feb41d3911ba51727d87d33a`; D `8247c5208f02095cd288e4aa1c271b6e20f041a6c92712ccba3791ae759a509e`; E `225902a76a91ed7f548318fc60efeaf14204ed634d91266d6155992c9d6cbc85`.
