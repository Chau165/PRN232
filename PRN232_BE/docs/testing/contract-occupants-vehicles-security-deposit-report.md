# Báo cáo triển khai và kiểm thử hợp đồng

Ngày kiểm tra: 15/11/2026

## Thay đổi chính

- Tạo hợp đồng ghi CCCD người đứng tên và danh sách người ở cùng; CCCD được mã hóa khi lưu. Booking vẫn là nguồn giá thuê và tiền cọc ban đầu.
- Số xe đăng ký bị giới hạn ở tổng số người trong hợp đồng: 3 người thì tối đa 3 xe. Backend kiểm tra lại giới hạn khi tạo hợp đồng; quản lý có thể xác nhận số xe cho hợp đồng cũ.
- Ghi nhận riêng tiền bảo đảm nhận trực tiếp và xác nhận bàn giao; cập nhật phép tính hóa đơn, chuyển hợp đồng và cấu hình dịch vụ theo loại phòng.
- Thêm migration `20261115160353_ContractOccupantsVehiclesSecurityDeposit`.
- Bổ sung hướng dẫn cấu hình Supabase bằng .NET User Secrets, không ghi password vào source hoặc `appsettings.json`.

## Kết quả kiểm thử

| Hạng mục | Kết quả |
| --- | --- |
| Frontend `npm run typecheck` | Pass |
| Frontend `npm test` | 484 Vitest + 2 Node tests pass |
| Frontend `npm run build` | Pass; Vite in cảnh báo cấu hình native loader có sẵn |
| Backend `dotnet build PRN232_BE.sln` bằng SDK 10.0.301 | Pass, 0 warning, 0 error |
| Backend suite bằng SDK 8.0.428 | 1.754 pass, 9 skip, 0 fail |
| Kiểm thử PostgreSQL 17.12 cô lập | 32 test case pass, 0 fail |
| `git diff --check` | Pass |
| Khởi động API bằng `dotnet run`/runtime .NET 10 | Swagger trả HTTP 200 |

Backend test dùng PostgreSQL 17.12 tại `127.0.0.1:55432`, trong cluster tạm riêng; migration/concurrency tests chỉ tạo và xóa database kiểm thử. Cluster đã dừng sau khi kiểm tra. API smoke test dùng một database cục bộ riêng rồi đã xóa. Không có test hay migration nào chạy trên Supabase.

Backend suite chạy tuần tự để tránh các fixture SQLite dùng chung một connection gây race khi cả suite chạy song song; các bài kiểm tra cạnh tranh giao dịch PostgreSQL vẫn chạy song song bên trong từng test.

Chín test bị skip là các test cần tích hợp/connection riêng hoặc đã được đánh dấu lỗi thời; trong đó có bài kiểm tra Supabase chỉ đọc. Bài này được đặt skip khi không có connection rõ ràng.

## Cấu hình và việc còn lại trước khi dùng Supabase

Máy thiếu ASP.NET Core 8 runtime nhưng có runtime 10. Bản Debug được đặt `RollForward=Major`; smoke test xác nhận ứng dụng vẫn khởi động bằng SDK/runtime 10 và trả Swagger HTTP 200.

Connection string thực tế chưa được lưu vì password đã xuất hiện trong chat. Hãy reset password trong Supabase, sau đó đặt password mới bằng lệnh User Secrets trong [README](../../README.md). Migration mới cũng chưa áp dụng lên Supabase; chỉ cập nhật schema sau khi đã kiểm tra backup và chủ động chạy migration trên database đó.

Smoke test dùng database local chưa migrate nên worker ghi log truy vấn các bảng chưa tồn tại; ứng dụng vẫn khởi động và trả HTTP 200. Khi chạy với database dùng thật, cần áp dụng migrations hiện có trước khi dùng API nghiệp vụ.
