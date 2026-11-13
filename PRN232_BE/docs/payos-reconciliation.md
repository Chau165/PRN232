# B9 — Đối soát và phục hồi payOS

## API thủ công

`POST /api/payment-requests/{id}/reconcile`, không có request body.

Admin được đối soát mọi yêu cầu; Manager chỉ được đối soát yêu cầu thuộc khu trọ mình đang quản lý.
Customer tiếp tục dùng `GET /api/payment-requests/{id}` để đọc trạng thái, không gọi API đối soát.
GET không phát sinh cuộc gọi payOS. POST dùng BookingPolicy để giới hạn tần suất.

Ví dụ kết quả:

```json
{
  "paymentRequestId": 12,
  "result": "ReconciledPayment",
  "status": "Succeeded",
  "checkedAtUtc": "2026-11-11T03:00:00Z"
}
```

`status` là trạng thái đã đọc lại từ DB sau khi xử lý; chỉ `Succeeded` xác nhận đã ghi nhận thanh toán.

| Result | Ý nghĩa |
| --- | --- |
| RecoveredPending | Tìm thấy đơn chưa trả; phục hồi link trên đúng mã đơn cũ |
| ReconciledPayment | Đã đưa giao dịch qua luồng ghi nhận tiền B8 |
| ReplayedReceipt | Xử lý lại receipt Verified đã lưu từ B7, không cần gọi payOS |
| AlreadyCompleted | Đã thành công, không gọi lại payOS |
| ClosedUnpaid | payOS xác nhận CANCELLED/EXPIRED và không có tiền/giao dịch |
| AwaitingProviderClosure | Hết hạn cục bộ nhưng payOS vẫn PENDING; chưa nhả yêu cầu |
| AwaitingProviderConfirmation | payOS đang PROCESSING, tiếp tục chờ |
| TooRecent | Chưa đến thời điểm kiểm tra tiếp, HTTP 202 |
| ProviderUnavailable | Timeout/lỗi khi tra cứu, HTTP 202; lần sau thử lại |
| NotFoundAtProvider | Chưa tìm thấy đơn, chưa hết hạn, HTTP 202 |
| RequiresReview | Dữ liệu không khớp/không rõ, dừng tự động ghi nhận tiền |
| ManualReviewOrClosed | Yêu cầu đã đóng hoặc cần con người kiểm tra |

ID không hợp lệ: 400; chưa đăng nhập: 401; không có quyền: 403; không có yêu cầu payOS: 404;
payOS tắt: 503. HTTP 200 không đồng nghĩa hóa đơn đã thanh toán: phải xem `status`.

## Worker

Cấu hình mặc định trong `PayOS`:

```json
{
  "ReconciliationEnabled": false,
  "ReconciliationIntervalSeconds": 60,
  "ReconciliationMinAgeSeconds": 60,
  "ReconciliationBatchSize": 20
}
```

Sau khi áp dụng migration, bật `PayOS:Enabled` và `PayOS:ReconciliationEnabled` cùng bộ khóa payOS hợp lệ.
Có thể dùng biến môi trường `PayOS__ReconciliationEnabled=true` rồi khởi động lại BE.
Không tự bật worker hoặc gọi tài khoản thật khi chạy test.

Mỗi chu kỳ chọn tối đa 20 yêu cầu Creating/Pending chưa được cập nhật 60 giây,
ưu tiên yêu cầu cũ nhất. Bao gồm RequiresReview với mã EXPIRED_CHECKOUT_UNRESOLVED của B5;
những RequiresReview khác dành cho kiểm tra thủ công, không bị tự động mở lại.
Mỗi lần tra cứu cập nhật UpdatedAtUtc để timeout không chiếm mãi vị trí đầu hàng đợi.
Tra cứu SDK chỉ GET, timeout 15 giây, không tự retry trong cùng lần gọi.
Lỗi DB làm dừng batch hiện tại; worker ghi log không chứa khóa và thử lại ở chu kỳ sau.

## Quy tắc an toàn và khôi phục

- Không gọi lại POST tạo link khi kẹt Creating. Hỏi bằng OrderCode đã lưu.
- PENDING hợp lệ: khôi phục PaymentLinkId và URL thanh toán payOS từ ID đã xác thực.
  URL khôi phục theo dạng hosted checkout `https://pay.payos.vn/web/{id}`; ID được giới hạn ký tự an toàn.
- Lỗi mạng/404 không lập tức chuyển Create-failed hoặc cho tạo đơn khác.
  404 sau hạn cục bộ chuyển RequiresReview để tránh tạo lại khi kết quả tạo đơn cũ chưa rõ.
- PAID phải khớp mã đơn/link/tổng hóa đơn, AmountPaid bằng toàn bộ Amount, AmountRemaining=0,
  đúng một giao dịch đủ tiền với reference và thời điểm thực tế. Thiếu/thừa tiền, nhiều giao dịch,
  timestamp không hợp lệ hoặc ngoài hạn chuyển RequiresReview, không tự cộng doanh thu.
- GET V2 payOS không trả currency. Đối soát dùng kênh VND và yêu cầu gốc bắt buộc Currency=VND;
  không tuyên bố đã xác minh currency độc lập từ response GET.
- Thời gian giao dịch dùng policy B8: timestamp không offset được hiểu theo giờ Việt Nam;
  timestamp có offset được quy đổi UTC. Không dùng thời gian tra cứu làm PaidAtUtc.
- Khôi phục thông tin link được lưu trước; ghi tiền dùng lại transaction B8:
  invoice Paid, payment/PaidAtUtc, request Succeeded, receipt, thông báo cùng commit/rollback.
  Nếu dừng giữa hai bước, yêu cầu Pending còn trong hàng đợi để đối soát lại.
- Khóa invoice và khóa reference cùng thứ tự như B8; webhook/đối soát dùng chung chống trùng.
  Cùng giao dịch với timestamp khác định dạng nhưng cùng thời điểm không bị coi là giao dịch mới.
- Receipt có Source=Webhook hoặc Reconciliation. Không gán WebhookReceivedAtUtc từ một lần GET;
  webhook thật đến sau mới cập nhật trường này. Receipt gốc vẫn giữ nguồn xác nhận ban đầu.

Migration `AddPaymentReceiptSource` thêm cột Source, mặc định Webhook cho receipt cũ.
Migration được tạo bằng EF; chưa áp dụng lên DB triển khai trong bước B9.
Áp dụng bằng `dotnet ef database update --project PRN232.Infrastructure --startup-project PRN232.Api`
từ thư mục PRN232_BE với đúng connection string của môi trường cần cập nhật.

Các test dùng SQLite và gateway giả lập, không chuyển tiền thật; chưa xác minh tranh chấp đồng thời
trên PostgreSQL hoặc phản hồi trực tiếp của tài khoản payOS triển khai.
