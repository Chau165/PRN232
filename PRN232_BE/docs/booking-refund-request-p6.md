# P6 Customer gửi yêu cầu hoàn cọc booking

## API

`POST /api/bookings/{bookingId}/refund-requests`

Bearer token Customer. Admin và Manager không gửi đơn thay Customer qua API này.

```json
{
  "reason": "Không còn nhu cầu thuê",
  "recipientBankBin": "970422",
  "recipientAccountNumber": "001234567890",
  "recipientAccountName": "TEST CUSTOMER"
}
```

Tên người nhận có thể bỏ trống bằng cách không gửi trường hoặc gửi null. Đây là
thông tin Customer khai báo, chưa được xác minh. BIN cần 6 chữ số, tài khoản chỉ
gồm chữ số và tối đa 64 ký tự; giữ số 0 đầu. Lý do bắt buộc tối đa 2000 ký tự.
Tên tài khoản nếu có tối đa 256 ký tự. Các trường được trim và không nhận control
characters. Chưa hỗ trợ alias ngân hàng.

Customer không truyền CustomerId, OriginalPaymentId, Status hoặc số tiền.
BE xác định RequestedAmount bằng toàn bộ khoản cọc payOS gốc đã xác nhận.
Số tiền được duyệt thực tế thuộc P7, không được phê duyệt hoặc chuyển trong P6.

Thành công trả `201 Created`, header Location và dữ liệu:

```json
{
  "bookingRefundRequestId": 1,
  "bookingId": 10,
  "originalPaymentId": 20,
  "requestedAmount": 3000000,
  "currency": "VND",
  "status": "PendingReview",
  "reason": "Không còn nhu cầu thuê",
  "recipientBankBin": "970422",
  "maskedRecipientAccountNumber": "****7890",
  "recipientAccountName": "TEST CUSTOMER",
  "accountNameVerified": false,
  "createdAtUtc": "2026-11-13T03:00:00Z",
  "updatedAtUtc": "2026-11-13T03:00:00Z"
}
```

`GET /api/bookings/{bookingId}/refund-requests/{requestId}` cho Customer đọc lại
đơn của mình. Sai booking hoặc không thuộc Customer trả 404. Response không cache
và không trả số tài khoản đầy đủ. Dữ liệu tài khoản đầy đủ chỉ lưu trong DB cho
nghiệp vụ duyệt sau này; không ghi vào log hoặc nội dung thông báo.

## Điều kiện tạo

- Booking tồn tại, thuộc Customer, đang Deposited và chưa có bất kỳ hợp đồng nào.
- Chỉ có một payment Success gắn booking; payment đúng Customer, chỉ thuộc booking,
  phương thức PayOS, số tiền nguyên VND dương và khớp tiền cọc booking.
- Có PaymentRequest PayOS Succeeded liên kết đúng payment, booking, Customer,
  số tiền và tiền tệ VND. Không có checkout Creating, Pending hoặc RequiresReview.
- Không có yêu cầu hoàn còn giữ claim hoặc đã Refunded cho booking hay payment.
  Yêu cầu từng bị Rejected có thể gửi mới; lịch sử cũ được giữ nguyên.
- Cọc giả lập hoặc dữ liệu thiếu/không rõ bằng chứng thu không đủ điều kiện qua
  API này; cần xử lý dữ liệu/đối soát, không giả định đã nhận tiền thật.

Các điều kiện được kiểm tra lại trong transaction, khóa room rồi booking trên
PostgreSQL. Unique index P4 tiếp tục là lớp chặn trùng cuối. Hai lần bấm đồng thời
cho một đơn hợp lệ chỉ có một lần 201, lần còn lại 409. SQLite test dùng gate ghi
trong process; không dùng kết quả này để khẳng định mọi tình huống nhiều process.

Khi tạo đơn, lưu PendingReview và thông báo nội bộ cho Admin đang hoạt động trong
cùng transaction. Chưa phát realtime event, email hoặc payout. Không đổi booking
thành Cancelled, không nhả phòng, không thay payment đã thu và không tạo PayoutAttempt.

Lập hợp đồng kiểm tra lại dưới khóa chung và bị chặn nếu booking có đơn hoàn khác
Rejected. Nếu hợp đồng được tạo trước thì gửi đơn bị chặn. Khoản booking đã đưa
vào hợp đồng/khấu trừ tiền phòng không được hoàn lại qua endpoint này.

## Mã HTTP

| Mã | Ý nghĩa |
| --- | --- |
| 201 | Đã lưu yêu cầu PendingReview |
| 400 | Input không hợp lệ |
| 401 | Chưa đăng nhập |
| 403 | Sai vai trò hoặc booking thuộc Customer khác khi gửi đơn |
| 404 | Booking không tồn tại; hoặc không thấy đơn trong phạm vi Customer khi đọc |
| 409 | Trạng thái không cho phép, đã có claim, có hợp đồng hoặc thiếu bằng chứng thu |

Không gọi gateway payout hoặc kiểm tra số dư lúc Customer nộp đơn. Chưa cần bật
PayOSPayout để tạo yêu cầu. P6 không đổi schema; DB phải có migration P4
`AddBookingRefundRequestsAndPayoutAttempts` trước khi sử dụng.

## Kiểm thử

`BookingRefundRequestApiTests` kiểm tra quyền, input, bằng chứng thanh toán, số
tiền do server tính, đơn trùng, gửi đồng thời, gửi lại sau từ chối, đọc đơn theo
Customer và chặn chéo giữa lập hợp đồng với yêu cầu hoàn. Bộ test sử dụng SQLite
riêng, không gửi tiền và không truy cập Supabase.

Test `BookingRefundRequestApiTests.PostgreSQL_serializes_duplicate_claims_and_contract_refund_race_under_shared_locks`
chạy khi có `PRN232_MIGRATION_TEST_POSTGRES_CONNECTION` trỏ đến server test loopback
PostgreSQL 17 tại cổng 55432, database `postgres`, Application Name
`PRN232-Phase31B-Isolated-Test`. Test tạo DB tên `prn232_refund_p6_<GUID>`, tạo schema
theo model trong DB đó, chạy race tạo đơn/lập hợp đồng và xóa đúng DB tạm ở finally.
Job `postgres-tests` trong CI đã bao gồm test này. Không trỏ cấu hình test tới Supabase.
