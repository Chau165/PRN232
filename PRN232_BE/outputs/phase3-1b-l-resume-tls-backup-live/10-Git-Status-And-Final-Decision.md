# 10 — Git status và quyết định cuối

## Quyết định

**BLOCKED_BACKUP_RESTORE**

TLS và fresh read-only preflight PASS; Gate B/C/D/E không chạy. Backup/restore bị chặn trước khi pg_dump chạy bởi automatic tool safety review; owner xác nhận password chưa rotate. Restore rehearsal và regression gates chưa đạt.

Tool trả “blocked by policy” cho lệnh tạo PGPASSFILE tạm từ credential hiện có. Không tìm cách né bằng command-line password hay cơ chế kém an toàn. Để tiếp tục: owner rotate credential, cập nhật đúng Windows User Environment và tự provision PGPASSFILE an toàn; chỉ gửi path/ready status, không gửi nội dung.

## Git và phạm vi

- Trước resume: 90 status entries (37 tracked changes, 53 untracked) đã tồn tại từ Phase 3.1B/3.1C và FE work.
- Lượt này không sửa source, migrations, FE hoặc Billing Engine. Chỉ thêm thư mục report mới.
- Không stage/commit/push/deploy. Không live write; không đổi Invoice/Booking/Contract/Maintenance; không phát hành hóa đơn.

## Điều kiện để tiếp tục

1. Rotate mật khẩu ngay và cập nhật biến Windows User Environment hiện tại; không gửi secret.
2. Provision PGPASSFILE có ACL chỉ user hiện tại; xác nhận bằng path/ready status.
3. Tạo backup, manifest/hash, restore độc lập; rehearsal B→C→D→E trên bản restore.
4. Chạy full regression, gồm 4 public-route PostgreSQL tests không skip; sau đó fresh preflight/hash/role checks.
5. Chỉ khi mọi hard gate PASS mới áp dụng Gate B/C/D/E. Gate F vẫn cần approval riêng cho từng dòng preview.

## Quyết định sau lần tiếp tục — 2026-10-20

**BLOCKED_AUTH_AND_BACKUP_RESTORE**

- Mặc dù owner báo đã rotate/cập nhật, kết nối thử từ chính giá trị Windows User Environment bị PostgreSQL từ chối (`28P01`). Giá trị User và tiến trình không đồng nhất; cần cập nhật/khởi động lại tiến trình sau khi xác minh lại cấu hình.
- Windows User Environment hiện vẫn đặt `TrustServerCertificate=true` và không có `SslMode`; cần cấu hình VerifyFull cùng CA đúng trước khi dùng lại.
- Backup/restore, migration rehearsal và regression vẫn chưa chạy. Automatic tool safety review trước đó chặn thao tác provision PGPASSFILE; không có backup và không chạy `pg_dump`.
- Không có Gate B/C/D/E live write; không có thay đổi dữ liệu nghiệp vụ.

Để tiếp tục an toàn: sửa Windows User Environment thành credential Supabase đang hoạt động và cấu hình TLS VerifyFull/CA; restart Codex/terminal để tiến trình nhận giá trị mới. Sau đó chỉ báo đã sẵn sàng, không gửi password. Cần owner provision PGPASSFILE an toàn và báo path/ready status để tiếp tục backup/restore; không gửi nội dung file.

## Trạng thái kết nối mới nhất — 2026-10-20

**BLOCKED_AUTH_AND_BACKUP_RESTORE — không migration.**

- Npgsql 8.0.5 parse chuỗi User thành công, không có wrapper. Host/port/database/username khớp PRN232.
- User scope đã được kiểm tra với `SslMode=VerifyFull`, `TrustServerCertificate=false`, CA tồn tại và đúng Supabase Root 2021. Process scope không có giá trị; phép thử đã dùng User scope trực tiếp.
- PostgreSQL vẫn trả `28P01` cho user `postgres`. Đây là lỗi xác thực password; TLS xác minh đã đến được server. Không chạy truy vấn read-only vì xác thực thất bại ở `Open()`.
- Không chạy backup/migration/test; không thay đổi dữ liệu live.

Supabase ghi nhận shared pooler có thể trả `28P01` ngay sau khi rotate password và password mới có thể cần vài phút để có hiệu lực. Hãy chờ vài phút sau lần reset gần nhất rồi thử lại. Nếu vẫn lỗi, vào Supabase Dashboard → dự án `PRN232_DB` → **Database → Settings**, reset database password, cập nhật riêng password trong Windows User variable `ConnectionStrings__DefaultConnection` (giữ nguyên các trường target/TLS, không thêm wrapper), rồi restart Codex/terminal. Không gửi password. Hướng dẫn chính thức: https://supabase.com/docs/guides/troubleshooting/how-do-i-reset-my-supabase-database-password-oTs5sB

Sau khi kết nối read-only PASS, mới tiếp tục backup/restore, rehearsal và các gate theo prompt. Backup hiện vẫn chưa tạo; automatic safety review trước đó chặn việc tạo PGPASSFILE tạm, vì vậy cần provision an toàn và báo path/ready status.

## Quyết định mới nhất — 2026-10-20

**CONNECTED_READ_ONLY_PASS; BLOCKED_BACKUP_RESTORE.**

- Kết nối Npgsql VerifyFull thành công; fresh live preflight PASS; migration history, schema, counts, RLS, indexes và constraints đã được đọc trong transaction read-only và rollback. Bốn Gate B/C/D/E hashes khớp manifest.
- Chưa có PGPASSFILE, backup directory có 0 file. `pg_dump`/`pg_restore` 17.10 có sẵn nhưng chưa chạy. Automatic safety review trước đó chặn việc tạo PGPASSFILE từ credential; không tìm cách né.
- Do chưa có backup + independent restore + rehearsal + full regression, Gate B/C/D/E không được chạy live. Không có thay đổi dữ liệu hoặc schema live.
- Gate B review script còn yêu cầu approval ngoài-of-band cho hash và `phase31br_gate_b_approved=true` trước thực thi.

Bước tiếp theo: owner provision PGPASSFILE an toàn và chỉ gửi path/ready status (không gửi nội dung/password). Sau khi backup, restore, rehearsal và regression đạt, mới tiếp tục các Gate live theo prompt.
