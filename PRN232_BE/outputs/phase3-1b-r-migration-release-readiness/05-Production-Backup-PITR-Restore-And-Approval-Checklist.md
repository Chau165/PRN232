# Backup, PITR, restore và approvals

## Trạng thái project cụ thể

**CHƯA XÁC MINH.** PostgreSQL catalog không cung cấp Supabase plan, danh sách restore points, backup retention thực tế hay trạng thái PITR. Trong phiên này không có bằng chứng account Dashboard hoặc một restore drill của project. Không suy luận từ phiên bản PostgreSQL hoặc trạng thái role.

Supabase hiện mô tả daily backup cho Pro/Team/Enterprise với retention theo plan (Pro 7 ngày, Team 14 ngày, Enterprise tối đa 30 ngày); PITR là add-on trên các plan trả phí và yêu cầu tối thiểu Small compute. Dashboard hiển thị recovery points thực tế; restore làm project không truy cập được trong quá trình chạy. Các chi tiết account-level này phải được xác nhận tại project của người dùng, không thể coi tài liệu chung là bằng chứng backup đang bật. [Supabase Database Backups](https://supabase.com/docs/guides/platform/backups)

## Bằng chứng bắt buộc trước live

- [ ] Plan hiện tại và project ref/region được xác nhận bởi project owner.
- [ ] Ảnh/export Dashboard thể hiện backup gần nhất hoặc PITR earliest/latest recovery point, timezone và retention.
- [ ] Recovery point nằm trước cửa sổ thay đổi và RPO được owner chấp nhận.
- [ ] Restore drill thành công vào project độc lập hoặc môi trường phục hồi; xác minh schema, dữ liệu, auth/roles và extension cần thiết.
- [ ] Có người phụ trách restore, người trực triển khai, cửa sổ downtime và kênh liên lạc.
- [ ] Có phương án riêng cho Storage objects: Supabase ghi rõ daily database backup không bao gồm object trong Storage API. Nếu ứng dụng phụ thuộc ảnh/đính kèm, kiểm tra backup/restore riêng. [Backup scope and restoration](https://supabase.com/docs/guides/platform/backups)

## Approval checklist

- [ ] Owner chấp nhận giữ `CreatedAt DEFAULT CURRENT_TIMESTAMP`.
- [ ] Owner/DBA xác nhận giữ hai status CHECK live extra và MaintenanceLogs RLS.
- [ ] Security owner chấp nhận RLS không có policy và đường API/role dùng để đọc/ghi các bảng mới.
- [ ] DBA xác nhận cài `btree_gist` theo quy trình Supabase; quyền catalog đơn thuần không đủ.
- [ ] Owner/DBA review và ký SHA-256 trong SQL manifest cho Gate B, C, D, E.
- [ ] Preflight mới chạy trong transaction READ ONLY ngay trước triển khai và khớp chính xác snapshot; nếu drift thì dừng.
- [ ] DBA xác nhận không có ứng dụng/writer chạy thay đổi Maintenance/Invoice trong cửa sổ migration.
- [ ] Có phê duyệt riêng cho mọi cleanup nếu `CREATE INDEX CONCURRENTLY` để lại invalid index.

## Restore / rollback posture

Không dùng `Down()` làm rollback: Down có guard gặp 1 NULL `OldStatus` và còn drop báo cáo/ảnh. Nếu C/D/E đã bắt đầu, dừng đúng tại migration hoàn tất cuối cùng, giữ database ở trạng thái đó, thu thập postflight và chọn forward-fix có review. Restore từ backup/PITR có thể loại bỏ giao dịch hợp lệ phát sinh sau recovery point và gây downtime; chỉ người có thẩm quyền mới quyết định. Kiểm thử restore tách biệt trước khi release.
