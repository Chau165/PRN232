# P3 — Đặt cọc booking qua payOS

## Cấu hình và phạm vi

P3 triển khai BE: tạo link, webhook ghi nhận cọc, API trạng thái và đối soát booking.
Cần schema P2 (`AddBookingPaymentRequestTarget`) trước khi chạy BE mới; P3 không thêm migration.

```json
{
  "PayOS": {
    "Enabled": true,
    "BookingPaymentsEnabled": true,
    "ReconciliationEnabled": true
  }
}
```

BookingPaymentsEnabled mặc định false để triển khai BE trước khi thay giao diện DepositPage đang dùng cổng giả lập.
Khi bật cờ, POST /bookings/{id}/pay giả lập trả 409. Không fallback sang mock khi payOS lỗi.
Nếu tắt cờ sau khi đã có yêu cầu booking, webhook/đối soát vẫn xử lý các yêu cầu đã tạo;
repository vẫn chặn trả giả lập/hủy trực tiếp khi còn checkout đang hoạt động hoặc chưa rõ kết quả.
Enabled, khóa merchant, ReturnUrl/CancelUrl và webhook public dùng cấu hình kênh thu đã có.
Không cần SDK/khóa mới cho booking; Chi hộ/Payout không được gọi ở bước này.

P3 chưa thay nút trả cọc trên FE. Phần FE booking/refund ở P11 phải gọi API mới, lưu PaymentRequestId trước
khi mở checkout và phân biệt Booking với Invoice ở trang kết quả dùng chung. Chỉ bật cờ cho người dùng web
khi phần FE đó sẵn sàng; hiện có thể kiểm tra API bằng Swagger/HTTP client.

## Tạo link

`POST /api/bookings/{id}/payment-link` — chỉ Customer sở hữu booking, không có body bắt buộc.
Amount/CustomerId do client gửi không được sử dụng. BookingId phải dương.

BE kiểm tra trong transaction dưới khóa room → booking:

- Booking Pending, chưa có hợp đồng, còn hạn và chưa có payment thành công.
- Phòng còn Available, không có booking khác giữ phòng hoặc hợp đồng; trạng thái lạ chặn giống policy phòng hiện tại.
- DepositAmount lấy từ booking: số nguyên VND dương, trong miền hỗ trợ.
- Chỉ một request Creating/Pending/RequiresReview cho booking. RequiresReview phải được xử lý trước.
- Request Creating được dùng lại, không gọi POST tạo đơn lần hai sau timeout.
- Request Pending được dùng lại cùng link/mã đơn khi còn hạn.

Hạn request bằng hạn 15 phút của booking, không thêm 30 phút từ lúc bấm trả cọc.
ExpiredAt gửi SDK được quy đổi UTC và làm tròn xuống giây Unix, nên không vượt hạn booking.
Provider description là COC PHONG; không đưa dữ liệu cá nhân vào nội dung chuyển tiền.

Response:

```json
{
  "paymentRequestId": 12,
  "bookingId": 8,
  "status": "Pending",
  "amount": 3000000,
  "currency": "VND",
  "checkoutUrl": "https://pay.payos.vn/web/<link-id>",
  "expiresAtUtc": "2026-11-11T08:15:00Z",
  "reused": false
}
```

201: mới tạo link; 200: dùng lại link hoặc cọc đã xác nhận ngay trong lúc tạo;
202: còn Creating/chưa có URL mở được. 202 không có nghĩa đã nhận tiền.
400: ID/số tiền không hợp lệ; 401/403: quyền; 404: không có booking; 409: hết hạn/đã cọc/đã có hợp đồng/
phòng không còn khả dụng/cần đối soát; 502: payOS từ chối tạo; 503: chưa bật cấu hình.

Lỗi tạo dứt điểm → Create-failed, cho phép attempt mới nếu booking còn hợp lệ.
Timeout/mất phản hồi → Creating + CREATE_RESULT_UNKNOWN; không kết luận thất bại và không lặp POST.
Response sai mã đơn/số tiền/VND/link/URL → RequiresReview.

## Webhook và ghi nhận tiền

Dùng `POST /api/payments/payos/webhook` hiện có và bộ xác minh chữ ký SDK thật.
OrderCode xác định PaymentRequest; FK phân biệt Invoice và Booking, không dùng loại đối tượng từ client/webhook.

Booking chỉ Deposited khi:

1. Provider/VND/code/link khớp; số tiền đúng request và DepositAmount, customer khớp booking.
2. Request Creating/Pending chưa có Payment; booking Pending chưa có hợp đồng và chưa có khoản cọc thành công khác.
3. Thời gian giao dịch thực nằm từ lúc tạo request đến trước hạn request/booking và không ở tương lai.
4. Dưới khóa room → booking, phòng chưa bị booking/hợp đồng khác chiếm hoặc chuyển sang trạng thái không cho thuê.

Nếu webhook đến trước response tạo link, request Creating có thể nhận link ID từ dữ liệu đã xác minh của đúng OrderCode.
Response tạo link đến sau không được ghi đè Succeeded; API trả trạng thái thực tế đã xác nhận.

Cùng transaction lưu: Payment/Success/PayOS/BookingId/PaidAtUtc, request Succeeded + PaymentId,
receipt Settled, booking Deposited, thông báo Customer và Manager hiện tại.
Không điền InvoiceId vào payment cọc. Báo cáo doanh thu hóa đơn hiện có không tự cộng tiền cọc vào doanh thu hóa đơn.
Nếu lỗi SaveChanges/notification, tất cả cập nhật rollback; webhook trả 503 để được gửi lại.

Sau Deposited, giữ phòng không giới hạn đến khi lập hợp đồng hoặc hoàn tất hủy/hoàn tiền.
Không kéo dài/ghi đè ExpiryDate; trường này là hạn chờ trả cọc của Pending, không phải ngày hết giữ phòng sau cọc.

Webhook đến chậm nhưng giao dịch thực trả đúng hạn có thể được nhận khi phòng chưa được cấp lại.
Tiền trả sau hạn, sai dữ liệu, booking bị hủy/lập hợp đồng, phòng đã được giữ bởi người khác hoặc reference bổ sung
được lưu receipt RequiresReview và thông báo Admin; không tự Deposited, không tạo thêm payment.
Các case này chưa tạo PaymentReviewCases của hóa đơn vì bảng đó là invoice-specific; xử lý hoàn cọc theo P4–P10 sau này.
Gửi lại cùng reference/fingerprint là no-op; không tạo payment/thông báo trùng. Trạng thái terminal không bị mở lại.

## Trạng thái và đối soát

`GET /api/payment-requests/{id}` hỗ trợ cả Invoice/Booking:

- Invoice giữ các giá trị InvoiceId/InvoiceStatus như cũ, BookingId/BookingStatus=null.
- Booking có InvoiceId/InvoiceStatus=null, BookingId và BookingStatus tương ứng.
- Customer chỉ xem request của mình; Manager theo owner hiện tại của khu trọ; Admin xem tất cả.
- CanOpenCheckout chỉ true khi Pending, chưa hết hạn và đối tượng còn chờ trả tiền/chưa có hợp đồng.
- GET chỉ đọc DB; không tin status/cancel trong URL quay về của payOS làm bằng chứng nhận tiền.

`POST /api/payment-requests/{id}/reconcile` và worker B9 hỗ trợ cả hai loại.
Creating/Pending được tra cứu bằng OrderCode cũ, kiểm tra mã đơn/link/tổng tiền và tài khoản sở hữu booking.
Kết quả PENDING: khôi phục link, vẫn chờ. CANCELLED/EXPIRED chắc chắn chưa nhận tiền/giao dịch: đóng request.
PAID phải có đúng một giao dịch đủ tiền, reference và thời điểm thật; dùng lại transaction settlement booking ở trên.
Thanh toán thiếu/thừa hoặc nhiều giao dịch cần kiểm tra, không tự cộng dồn và ghi nhận cọc.
Timeout/không tìm thấy không tự tạo đơn khác. Worker vẫn có cooldown/batch như B9, mặc định 60 giây/20 yêu cầu.
Manager cũ không được kích hoạt đối soát sau khi khu trọ đổi owner.

## Phối hợp với luồng hiện có

Khóa thao tác booking theo room → booking, đồng nhất với giữ phòng; lập hợp đồng cũng dùng thứ tự này để tránh đảo khóa.
Webhook có thêm khóa reference ở ngoài để chống trùng, như luồng hóa đơn.
Direct cancel bị chặn cho mọi role nếu còn request hoạt động/chưa rõ hoặc đã có payment PayOS thành công.
Booking chưa cọc được cancel sau khi đối soát xác nhận request đã đóng chưa có tiền.
P6–P10 sẽ thay nhánh booking đã cọc bằng yêu cầu hoàn và payout; P3 không cho Admin nhả phòng bằng cancel để bỏ qua hoàn tiền.

## Kiểm thử

BookingPayOSApiTests: quyền, giá/hạn từ server, reuse/timeout, flag rollout, webhook sai/chữ ký sai,
webhook trước response tạo, trả đúng hạn nhưng callback trễ, tiền đến muộn/phòng đã được giữ lại,
chống trùng, khoản thêm, rollback toàn bộ, SDK merchant transport, đối soát/worker và owner mới/cũ.
Tests dùng SQLite + merchant/gateway kiểm thử; chưa tạo link/chuyển tiền trên merchant thật hoặc kiểm tra concurrency PostgreSQL.
Không có migration mới ở P3; cần áp dụng P2 trên DB triển khai trước khi bật booking checkout.

Kết quả P3: build không warning/error; 773 test BE đạt, 9 bỏ qua (loại bài Supabase cần kết nối riêng),
3/3 bài liên thông hóa đơn B12 đạt. Trong bộ BE có 30 trường hợp BookingPayOSApiTests, gồm SDK thật với merchant kiểm thử.
