# Phase 3.1B-R — Quyết định và trạng thái

**Trạng thái: BLOCKED — cần owner/DBA chấp nhận drift Maintenance, chứng cứ backup/restore và phê duyệt triển khai.**

## Phạm vi đã thực hiện

- Đọc `CODING_STANDARDS.md`, 9 báo cáo Phase 3.1A, 10 báo cáo Phase 3.1B và rà soát inventory của gói Phase 3.1B. Patch Phase 3.1B được giữ nguyên.
- Kết nối Supabase bằng cấu hình có sẵn, chỉ chạy truy vấn catalog/count trong transaction `READ ONLY`, sau đó `ROLLBACK`. Snapshot lúc `2026-10-19 15:21:03 UTC`, PostgreSQL `17.11`, schema `public`.
- Không chạy `ALTER`, `CREATE`, `INSERT`, `UPDATE`, `DELETE`, extension installation, migration-history write hay EF migration trên Supabase live.
- Tạo PostgreSQL `17.10` cô lập trên loopback, dựng dữ liệu tổng hợp và replay Gate B cùng ba SQL migration C/D/E. Không dùng Docker và không dùng dữ liệu live trong test.
- Không sửa API, entity, DbContext, migration source, frontend hay lịch sử Git. Không stage/commit/push/deploy.

## Điều kiện đạt được

| Điều kiện | Kết quả |
|---|---|
| Live database chỉ đọc | Đạt; transaction xác nhận `transaction_read_only=on`, kết thúc bằng rollback |
| Reconcile Maintenance | Candidate Gate B qua trên clone đúng schema live-like; history marker chỉ thêm sau khi đủ precondition |
| Ba migration Phase 3.1B | C/D/E replay thành công tuần tự trên PostgreSQL cô lập |
| Bảo toàn dữ liệu cũ | Đạt trên fixture: 25 InvoiceDetails, 9 MaintenanceLogs, 4 MaintenanceReports, 5 MaintenanceReportImages; fingerprint các cột cũ không đổi |
| EF migration drift | Không có pending model changes; local EF nhận đủ 8 migration sau replay |
| Backend regression | `dotnet test PRN232_BE.sln --no-restore`: 200 passed, 0 failed, 0 skipped trên fixture local |
| Live release readiness | Chưa đạt; các approval/blocker bên dưới chưa được xác nhận |

## Blocker trước bất kỳ thay đổi live nào

1. Owner/DBA phải xác nhận giữ `MaintenanceReports.CreatedAt DEFAULT CURRENT_TIMESTAMP`, giữ hai CHECK status trên `MaintenanceLogs`, và giữ trạng thái RLS hiện có của `MaintenanceLogs`.
2. Cần bằng chứng từ Supabase Dashboard về plan, backup/recovery point mới nhất, retention/PITR và một restore drill độc lập. Catalog PostgreSQL không xác nhận được các thông tin account-level này.
3. `btree_gist` chưa được cài trên live. Catalog cho thấy extension có phiên bản trusted và role có quyền CREATE ở database, nhưng quyền vận hành trên Supabase chưa được thử; không cài trên live trong phase này.
4. Cần phê duyệt hash chính xác của SQL Gate B và ba migration SQL C/D/E trước khi áp dụng. Các SQL C/D/E hiện là review artifacts, không phải lệnh được phép chạy.
5. Security review chưa thông qua: API có endpoint anonymous và `POST /api/roles` chưa có authorization; bảng mới dự kiến bật RLS nhưng không có policy.

## Kết luận

Gói review và replay độc lập đã hoàn thành. Supabase live không đổi. **Chưa đủ điều kiện để áp dụng migration live** cho đến khi owner/DBA giải quyết các quyết định và bằng chứng ở trên. Danh sách đầy đủ nằm tại `05-Production-Backup-PITR-Restore-And-Approval-Checklist.md` và `07-Release-Runbook-Gate-B-C-D-E.md`.
