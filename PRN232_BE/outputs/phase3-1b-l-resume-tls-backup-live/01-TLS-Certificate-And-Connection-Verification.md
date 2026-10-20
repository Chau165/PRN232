# 01 — TLS certificate và connection

**TLS VerifyFull: PASS. Live DDL: BLOCKED bởi các gate khác.**  
Preflight: 2026-10-20 00:02 Asia/Ho_Chi_Minh (2026-10-19 17:02 UTC).

- Certificate: `C:\Users\Surface\Downloads\prod-ca-2021.crt`; Subject/Issuer `CN=Supabase Root 2021 CA`; hiệu lực 2021-04-28 đến 2031-04-26.
- SHA-256 certificate fingerprint (DER): `807025ad50d4ed219d2c9c7d299c004f824eb00cf7f65afef607d07b72e6cafa`.
- SHA-256 file PEM: `700723581420dd1ac98fd7e9ac529f0ef210edcaf87fc868a3add7d114c2f3b7`.
- Dùng lại Windows User Environment `ConnectionStrings__DefaultConnection`; process value khớp. Không tạo key trùng, không sửa biến đã lưu. Artifact cũ ghi .NET User Secrets không có connection key.
- Host/port/database khớp `aws-0-ap-southeast-1.pooler.supabase.com:5432/postgres`; username cấu hình có project ref `qvdrjyecaprfpgvbfyfe`.
- Chuỗi lưu hiện có `SslMode=Require`, `TrustServerCertificate=true`; VerifyFull chạy bằng builder trong bộ nhớ, `TrustServerCertificate=false`, chỉ định CA ở trên.
- Kết nối thành công, xác minh CA + hostname; Npgsql 8.0.5, PostgreSQL 17.11; database `postgres`; session/effective role `postgres`; `transaction_read_only=on`, sau đó ROLLBACK.
- Không in/lưu password hoặc connection string.

Owner xác nhận password chưa rotate sau khi chia sẻ trong chat. Điều kiện prompt yêu cầu rotate và cập nhật Windows User Environment trước live DDL; chưa đạt.

## Tiếp tục xác minh — 2026-10-20

- Owner báo đã đổi password và cập nhật Windows User Environment. Metadata của cấu hình User khớp host/port/database/username của dự án; không in connection string hay password.
- Giá trị User khác với giá trị môi trường của tiến trình hiện tại. Kết nối thử trực tiếp từ giá trị User, dùng builder trong bộ nhớ với `SslMode=VerifyFull`, `TrustServerCertificate=false` và CA nêu trên, bị PostgreSQL từ chối với SQLSTATE `28P01` (password authentication failed). Do đó TLS VerifyFull mới chỉ đạt tới PostgreSQL authentication; credential hiện tại chưa được chấp nhận và chưa có kết nối DB thành công trong lần tiếp tục này.
- Giá trị lưu trong User Environment vẫn có `TrustServerCertificate=true` và không có `SslMode`; cấu hình bền vững chưa đáp ứng yêu cầu TLS. Không sửa cấu hình credential trong lượt này.
- Kết quả VerifyFull PASS ở snapshot ban đầu chỉ áp dụng cho credential trước đó; không dùng nó làm bằng chứng kết nối hiện tại.

## Kết quả kiểm tra mới nhất — 2026-10-20

- `NpgsqlConnectionStringBuilder` 8.0.5 constructor parse thành công; giá trị Windows User là Npgsql key/value trực tiếp, không có wrapper `ConnectionString=`.
- Target: `aws-0-ap-southeast-1.pooler.supabase.com:5432/postgres`, username scoped đúng project `qvdrjyecaprfpgvbfyfe`.
- Windows User đã có `SslMode=VerifyFull`, `TrustServerCertificate=false`, `RootCertificate=C:\Users\Surface\Downloads\prod-ca-2021.crt`; file CA tồn tại và parse được, subject `CN=Supabase Root 2021 CA`.
- Windows Process scope không có biến tương ứng; phép thử dùng trực tiếp User scope, không dùng giá trị process cũ.
- Kết nối Npgsql với VerifyFull trả SQLSTATE `28P01`, `password authentication failed for user "postgres"`. TLS/CA/hostname đã qua tới bước PostgreSQL authentication; không mở được session, nên truy vấn read-only chưa chạy. Không có database write.
- Kết quả mới này thay thế ghi chú cũ về SSL fields còn thiếu. Các SSL fields hiện đã đúng; blocker còn lại là xác thực password.

## Kết nối PASS — 2026-10-20

Sau khi owner cập nhật credential, Npgsql 8.0.5 parse trực tiếp giá trị Windows User; User và Process hiện khớp nhau. Host/port/database/username đúng dự án. `SslMode=VerifyFull`, `TrustServerCertificate=false`, CA `C:\Users\Surface\Downloads\prod-ca-2021.crt` tồn tại và hợp lệ.

Npgsql mở kết nối thành công; transaction REPEATABLE READ READ ONLY trả database `postgres`, role `postgres`, server `17.11`, `transaction_read_only=on`, sau đó ROLLBACK. Đây là kết quả mới nhất và thay thế lần auth `28P01` trước đó. Password/connection string không được ghi vào báo cáo.
