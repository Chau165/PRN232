# Hóa đơn payOS — thời gian đầu phút và đối soát lại

## Phạm vi sửa

PayOSPaymentTimePolicy.IsAfterCreation dùng chung cho booking và hóa đơn. BookingPaymentTimePolicy giữ API cũ và chuyển tiếp tới policy chung. Quy tắc hẹp: timestamp sau/tại CreatedAtUtc, hoặc chính xác đầu cùng phút tạo request. Không nới mốc hết hạn, không chấp nhận thời gian tương lai so với bằng chứng nhận được. Không khẳng định payOS luôn làm tròn timestamp; đây là dung sai nghiệp vụ của dự án cho dữ liệu đã quan sát.

Hóa đơn áp dụng quy tắc ở cả validTime (ghi nhận khoản đầu) và authenticTransfer (nhận diện khoản trả trùng). Giữ nguyên PaidAtUtc provider, xác minh SDK/chữ ký hoặc authenticated lookup và các ràng buộc order/link/customer/amount/currency/reference. Mã lỗi thời gian vẫn là WEBHOOK_TRANSACTION_TIME_INVALID. Không thay entity/schema, không cần migration.

## Đối soát lại hóa đơn cũ

```http
POST /api/payment-requests/{paymentRequestId}/reconcile?recheckInvoice=true
```

Chỉ Admin được bật recheckInvoice. Customer/Manager không có quyền với tùy chọn này. Luồng reconcile thông thường và worker giữ nguyên, không tự mở lại review do thời gian. `recheckBooking=true` vẫn dành cho booking như trước.

Hóa đơn RequiresReview chỉ được xét lại nếu LastErrorCode=WEBHOOK_TRANSACTION_TIME_INVALID; các review do sai số tiền, sai link, conflict reference hoặc nghi trả trùng không được bỏ qua.

Đối soát hỏi lại payOS, yêu cầu PAID với đúng một transaction đúng số tiền/order/link; sau đó replay receipt cũ có dữ liệu/fingerprint tương đương và không có ConflictingFingerprint. Không dùng một giao dịch khác để ép ghi nhận yêu cầu đã bị review. Trạng thái review được giữ trong bước lookup, không tự mở lại checkout nếu provider còn PENDING, timeout hoặc dữ liệu sai.

Transaction ghi nhận khóa và kiểm tra lại invoice hiện tại:

- Unpaid/Overdue + toàn bộ điều kiện hợp lệ: một Payments Success, PaidAtUtc gốc, Invoice Paid, request Succeeded, receipt Settled và thông báo như luồng hiện có.
- Invoice đã Paid, ví dụ thu tiền mặt trong thời gian chưa xác định khoản online: không tạo payment online thứ hai, giữ khoản thu gốc, tạo PaymentReviewCases và receipt DuplicatePayment để Admin xử lý. Không tăng doanh thu lần hai, không tự hoàn tiền.
- Hóa đơn không còn đủ điều kiện hoặc bằng chứng không hợp lệ: vẫn review; không sửa trạng thái bằng tay.

Gọi lại sau thành công trả AlreadyCompleted. Gọi lại sau phát hiện trả trùng không tạo review case lặp. Điều kiện TooRecent/thời gian chờ đối soát hiện có vẫn áp dụng.

## Cách kiểm tra trên môi trường thật

Sau khi build/restart BE mới, đăng nhập Admin tại Swagger, lấy đúng PaymentRequestId của hóa đơn (không phải InvoiceId/BookingId), gọi endpoint phía trên. Sau đó đọc GET /api/payment-requests/{id} và tải lại payment-result/My Bills. Không gửi token/key vào chat. Kiểm tra payOS và chứng từ ngân hàng, không tạo order mới hoặc sửa SQL status để chữa đơn đã nhận tiền.

Chưa gọi API phục hồi trên DB thật trong lượt triển khai này; chỉ thêm khả năng xử lý có kiểm soát. Không đổi cấu hình appsettings.json riêng của người dùng.

## Kiểm thử

Bộ trọng tâm 91/91 pass: webhook chữ ký SDK, đối soát hóa đơn/booking, thời gian đầu phút +07:00, giây trước lúc tạo, phút trước, sát hạn, gửi lại webhook, giao dịch thứ hai, Admin-only replay invoice review, timeout/PENDING/mismatch/reference conflict, thu tiền mặt trước khi replay và không tạo payment/review trùng. Test dùng gateway giả và DB riêng, không chuyển tiền thật.

Nguồn về timestamp và payment request lookup: [payOS API](https://payos.vn/docs/api/). Dung sai đầu phút là chính sách ứng dụng, không phải độ chính xác timestamp được tài liệu bảo đảm.

Kết quả hồi quy BE theo bộ lọc CI: 1.411 pass, 14 skip, 0 fail. Các ca cần PostgreSQL/FE E2E opt-in chưa chạy trong lượt này. Build và diff check pass. Chưa đối soát hay sửa dữ liệu hóa đơn thật.
