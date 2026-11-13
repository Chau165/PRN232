# Khóa Data API cho bảng thanh toán / hoàn cọc

Migration: `20261113103807_SecurePaymentTables` (sau AddBookingRefundLedger).

Phạm vi: public.PaymentRequests, PaymentReviewCases, PaymentWebhookReceipts, BookingRefundRequests, PayoutAttempts, BookingRefunds.

Migration bật RLS (không FORCE), thu hồi mọi quyền bảng và sequence identity/serial liên quan từ PUBLIC và các role anon/authenticated/service_role đang tồn tại. Không tạo policy mở cho Data API, không thêm/xóa/sửa dữ liệu tài chính và không thay entity/model snapshot. Các role Supabase không tồn tại trên PostgreSQL local sẽ được bỏ qua. Nếu quyền kế thừa hoặc quyền cột tùy biến vẫn cho Data API truy cập, migration báo lỗi và rollback thay vì báo thành công giả.

BE .NET vẫn chịu trách nhiệm xác thực JWT và scope Customer/Manager/Admin. Không đưa khóa service_role vào FE. Việc revoke service_role là cần thiết vì role này có thể bypass RLS; chỉ bật RLS không đủ cho role đó.

## Trước khi áp dụng

Xác nhận đúng DB/project, migration hiện tại và role kết nối của BE (không chia sẻ connection string/mật khẩu). Role dùng để chạy migration cần sở hữu các bảng/sequence hoặc quyền quản trị tương đương. BE phải dùng role PostgreSQL tin cậy độc lập với anon/authenticated/service_role, là chủ sở hữu các bảng (không FORCE RLS) hoặc có BYPASSRLS cùng quyền bảng/sequence cần thiết. Role chỉ có GRANT thông thường nhưng không có policy sẽ bị RLS chặn. Không tự tạo policy USING(true) cho Data API để chữa lỗi BE.

Truy vấn kiểm tra chỉ đọc trong SQL Editor:

```sql
SELECT current_database(), current_user;
SELECT rolname, rolbypassrls FROM pg_roles WHERE rolname = current_user;
SELECT tablename, tableowner, rowsecurity
FROM pg_tables WHERE schemaname = 'public'
AND tablename IN ('PaymentRequests','PaymentReviewCases','PaymentWebhookReceipts',
                  'BookingRefundRequests','PayoutAttempts','BookingRefunds');
```

Nếu chạy trong SQL Editor bằng postgres thì current_user phản ánh phiên SQL Editor, chưa chứng minh role của BE. Kiểm tra role cấu hình BE riêng.

## Áp dụng

Từ PRN232_BE, với connection string đúng DB đích nằm trong environment/secrets của tiến trình EF:

```powershell
dotnet ef dbcontext info --project PRN232.Infrastructure --startup-project PRN232.Api
dotnet ef database update 20261113103807_SecurePaymentTables --project PRN232.Infrastructure --startup-project PRN232.Api
```

Không truyền mật khẩu trực tiếp trong command có thể lưu vào lịch sử shell. Railway Variables chỉ có hiệu lực cho tiến trình trên Railway, không tự truyền vào terminal local. Nếu dùng local profile Development, thêm `-- --environment Development` vào lệnh và kiểm tra lại Data source/Database name.

Đây là migration bổ sung; không sửa migration đã chạy, không cần tạo lại bảng. Migration mới chịu được thiếu role Supabase nhưng các migration cũ (ví dụ EncryptIdentityDocumentCccd) còn REVOKE role cố định; nâng DB local cũ có thể dừng ở migration cũ trước khi tới bước mới này.

## Sau khi áp dụng

```sql
SELECT t.tablename, t.rowsecurity, r.rolname,
       has_table_privilege(r.rolname, format('%I.%I', t.schemaname, t.tablename),
                           'SELECT,INSERT,UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER') AS api_has_table_access,
       has_any_column_privilege(r.rolname, format('%I.%I', t.schemaname, t.tablename),
                                'SELECT,INSERT,UPDATE,REFERENCES') AS api_has_column_access
FROM pg_tables t CROSS JOIN pg_roles r
WHERE t.schemaname='public'
AND t.tablename IN ('PaymentRequests','PaymentReviewCases','PaymentWebhookReceipts',
                    'BookingRefundRequests','PayoutAttempts','BookingRefunds')
AND r.rolname IN ('anon','authenticated','service_role')
ORDER BY t.tablename, r.rolname;
```

Kỳ vọng rowsecurity=true, hai cột quyền=false. Tải lại Table Editor để kiểm tra nhãn UNRESTRICTED. Thử API booking/payment status/refund/report bằng đúng user, xác nhận BE vẫn đọc/ghi theo nghiệp vụ. Việc xem/sửa dữ liệu bằng SQL Editor với postgres vẫn hoạt động là bình thường; không chứng minh quyền Data API còn mở.

Down cố ý không tắt RLS hoặc cấp lại quyền, để rollback ứng dụng không làm lộ dữ liệu. Nếu rollback EF history rồi chạy lại migration, thao tác bảo mật có thể áp dụng lại; khôi phục quyền khác cần script được rà soát riêng.

## Kiểm thử

PaymentTableSecurityMigrationTests kiểm tra script EF đủ 6 bảng, không sửa dữ liệu, không policy mở, không tự mở lại khi Down. Test opt-in PostgreSQL local 55432 tạo DB riêng, kiểm tra trường hợp có/không role Supabase, owner vẫn đọc, API roles bị chặn SELECT/INSERT/UPDATE/DELETE và sequence, giữ nguyên dữ liệu, chạy lại an toàn và phát hiện quyền cột còn sót. Role test tạo trong transaction và rollback, DB tạm được xóa; không dùng Supabase thật.

Nguồn: [Supabase RLS](https://supabase.com/docs/guides/database/postgres/row-level-security).

Kết quả xác minh 13/11/2026: 18 test script/API tracking/report pass, 1 test PostgreSQL opt-in skip vì server test 55432/Docker chưa chạy. Build pass; EF xác nhận không có model changes ngoài migration. Chưa áp dụng lên Supabase hoặc DB ứng dụng. Down giữ nguyên bảo mật.
