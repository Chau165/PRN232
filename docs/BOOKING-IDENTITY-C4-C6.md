# C4–C6: Hồ sơ CCCD và đặt cọc

## Luồng sử dụng

1. Customer mở trang đặt cọc, chọn ngày và số người (bao gồm Bạn).
2. Bấm **Chuẩn bị hồ sơ CCCD**. Bước này chưa tạo booking, chưa giữ phòng và chưa bắt đầu 15 phút.
3. Lưu họ tên, CCCD 12 số và xác nhận đồng ý của từng người. Sau khi lưu xác nhận mới upload được ảnh hai mặt.
4. Nhóm đầu là **Bạn — Người đứng tên dự kiến**, nhóm tiếp theo là **Người ở cùng 1, 2…**. Người ở cùng được phép thiếu hồ sơ ở giai đoạn này; việc bổ sung sau cọc thuộc C8.
5. Khi Bạn có đủ thông tin và hai mặt, không có thay đổi chưa lưu, bấm giữ phòng. FE chuyển sang `?bookingId=...` và dùng checkout payOS hiện có.

FE có trạng thái đang xử lý, báo lỗi, thay/xóa từng mặt và xem ảnh private tạm thời. Giảm số người đã nhập dữ liệu cần xác nhận. SessionStorage chỉ lưu applicationId theo user/phòng, không lưu số CCCD hoặc ảnh. Đổi phiên người dùng không mang hồ sơ của phiên cũ sang. Tải lại hồ sơ cảnh báo trước khi bỏ thay đổi chưa lưu.

Điều khoản cọc hiển thị theo cấu hình BE trả về, thay cho đoạn mô tả cố định cọc là tiền bảo đảm.

## API

Tất cả endpoint dưới thuộc `/api/booking-applications`, chỉ Customer. Chủ hồ sơ lấy từ token. Response NoStore, không trả số CCCD đầy đủ hoặc object path.

| Method/path | Dữ liệu |
| --- | --- |
| POST / | `{ roomId, moveInDate, occupantCount }`, tạo/lấy lại nháp cùng phòng |
| GET /{id} | Hồ sơ của mình, số CCCD đã che và cờ có từng mặt ảnh |
| PATCH /{id} | `{ moveInDate, occupantCount, version, confirmRemovePeople }` |
| PUT /{id}/people/{personId} | `{ fullName, identityNumber, consentConfirmed, version }`; số CCCD null giữ nguyên số cũ |
| POST /{id}/people/{personId}/images/{Front hoặc Back}?version=... | Multipart field `file`, tối đa 5 MiB |
| DELETE /{id}/people/{personId}/images/{Front hoặc Back}?version=... | Bỏ ảnh hiện hành |
| POST /{id}/people/{personId}/images/{Front hoặc Back}/read-url | Link private 120 giây; quyền được kiểm tra trước khi ký và thao tác được audit |
| POST /{id}/submit | `{ version, viewingId? }`, trả BookingDto; gọi lại trả booking đã liên kết |

404 được dùng cho cả hồ sơ không tồn tại và không thuộc Customer. 400 cho dữ liệu thiếu/sai. 409 cho version cũ, hồ sơ đã gửi, giảm người chưa xác nhận, phòng không trống hoặc Customer có booking Pending khác (có pendingBookingId). 502 khi Storage không khả dụng.

## Transaction, quyền và ảnh

- Service thực hiện quy tắc, Repository ghi DB; Infrastructure không gọi Service/API.
- Tạo nháp khóa Room rồi Customer trên PostgreSQL để tránh nháp trùng và giữ thứ tự khóa nhất quán với booking (kể cả khóa FK khi insert).
- Ghi thay đổi hồ sơ khóa Application trước các bản ghi con; mã Version vẫn được so sánh ở lệnh UPDATE để từ chối snapshot cũ.
- Submit dùng lại booking writer trong transaction ngoài và thứ tự khóa Viewing → Room → Customer. Phòng trống, lịch xem, giá thuê, sức chứa và booking chờ thanh toán được kiểm tra lại.
- Booking, thông báo và liên kết hồ sơ commit cùng nhau. Version/capacity lỗi thì rollback booking mới. Push realtime thực hiện sau commit và không làm giao dịch thành công bị báo thất bại nếu push lỗi.
- Các thay đổi dùng Version để chống ghi đè. Nếu upload xong nhưng lưu metadata xung đột/hủy, cố gắng dọn object vừa upload bằng timeout riêng.
- File được kiểm tra MIME/chữ ký và giới hạn dung lượng tại Storage theo C3. Chưa có OCR, xác thực giấy tờ thật hoặc full image decoder.
- Bỏ mặt ảnh giữ metadata inactive để C10 dọn sau. Giảm thành viên xóa metadata người bị bỏ rồi cố gắng dọn Storage; nếu Storage lỗi, C10 cần inventory prefix để xử lý orphan.
- Phòng có bản nháp bị chặn xóa bằng kiểm tra dependents.
- Không log CCCD, byte ảnh, signed URL hoặc key. Link ký đã cấp còn hiệu lực đến khi hết hạn, không lập tức vô hiệu bằng logout.

## Triển khai và phần còn lại

Không có migration mới ở C4–C6. Cần áp dụng migration C1–C3 `AddBookingIdentityPreparation` trước khi dùng API mới, rồi cấu hình bucket CCCD private theo tài liệu C1–C3. Chưa chạy migration, upload hoặc payOS thật trên Supabase.

API `POST /api/bookings` cũ vẫn hoạt động để tương thích mobile/client cũ và chưa bắt buộc hồ sơ CCCD mới. Muốn bắt buộc trên mọi client cần chuyển mobile và quyết định khóa đường tạo booking cũ ở C11. FE web hiện dùng luồng hồ sơ mới.

C7 xét duyệt, C8 chỉnh/bổ sung sau cọc, C9 chuyển hồ sơ sang hợp đồng và C10 cleanup chưa thuộc đợt này. Upload/đặt cọc không có nghĩa hồ sơ đã được duyệt. Hồ sơ Submitted không sửa qua API chuẩn bị.

## Kiểm thử

BookingApplicationApiTests dùng TestServer, SQLite và Storage mô phỏng: không giữ phòng sớm, thiếu hồ sơ, thay ảnh, submit/replay, quyền owner/role, version/cleanup, phòng bị người khác giữ, giảm người cần xác nhận và lịch xem không hợp lệ.

bookingIdentityForm.test.tsx kiểm tra đủ hai mặt, lưu trước upload, lỗi upload, giảm người (kể cả thông tin chưa lưu) và đổi phiên. bookingPages.test.tsx đi qua chuẩn bị rồi submit, sau đó tiếp tục payOS.

Kết quả: 51 test BE trọng tâm/hồi quy pass, bao gồm Swagger và 6 test API mới; toàn bộ 490 Vitest FE pass, 2 Node test pass. Sau chỉnh sửa cuối, chạy lại 21 test FE hồ sơ/booking đều pass. TypeScript và production build FE pass. Storage/SQLite được mô phỏng/cô lập, chưa kiểm chứng cạnh tranh PostgreSQL hoặc upload Supabase thực tế trong đợt này.
