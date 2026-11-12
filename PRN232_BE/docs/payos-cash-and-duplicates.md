# B10 — Tiền mặt và online

## Thu tiền mặt

Giữ nguyên `POST /api/invoices/{id}/record-cash` (Admin hoặc Manager hiện tại của khu trọ).
Client không gửi số tiền; BE tính từ các dòng hóa đơn. Hóa đơn đã Paid luôn bị chặn.

1. Kiểm tra quyền trước mọi cuộc gọi payOS.
2. Không có yêu cầu online hoạt động: ghi nhận tiền mặt như trước.
3. Có Creating/Pending: tra cứu payOS theo OrderCode cũ; không tạo link mới.
4. Nếu PENDING, chưa có tiền và không có giao dịch: gọi SDK CancelAsync rồi GET lại.
   Hủy theo mã đơn với lý do cố định, không đưa dữ liệu cá nhân vào lý do hủy.
5. Chỉ CANCELLED/EXPIRED khớp mã đơn/link/tổng tiền và AmountPaid=0, không có giao dịch mới được đóng yêu cầu cục bộ.
6. Ghi tiền mặt dưới khóa invoice: kiểm tra lại owner, khách, tổng tiền, trạng thái hóa đơn và mọi checkout hoạt động.
   Nếu checkout mới được tạo giữa bước 5 và 6 thì trả 409; không ghi tiền mặt.
7. Transaction cuối cùng ghi Payments/Cash/PaidAtUtc, hóa đơn Paid và thông báo cùng commit.

Nếu payOS đã PAID trước hoặc trong lúc hủy, BE dùng settlement B8 để ghi nhận online nếu hợp lệ,
sau đó trả **409 và không ghi tiền mặt**. FE cần tải lại hóa đơn/trạng thái thanh toán.
Nếu thiếu/thừa tiền, nhiều giao dịch hoặc thời gian không hợp lệ: RequiresReview; không ghi tiền mặt.

Timeout hủy không đồng nghĩa thất bại: GET lại có thể xác nhận đã hủy và cho phép thu tiền mặt.
Nếu GET lỗi, chưa tìm thấy, vẫn PENDING/PROCESSING hoặc dữ liệu không khớp thì trả 409 và giữ chặn thu tiền mặt.
GET và Cancel dùng timeout 15 giây mỗi lần, không tự retry SDK; một thao tác có thể gồm tối đa 3 cuộc gọi.
Yêu cầu RequiresReview thông thường phải xử lý thủ công; chỉ mã tạm EXPIRED_CHECKOUT_UNRESOLVED của B5 được tự đối soát tiếp.
Nếu payOS đang tắt mà vẫn có checkout hoạt động, cũng chặn tiền mặt; không bỏ qua kiểm tra vì thiếu cấu hình.

Việc hủy link không phải hoàn tiền và không bảo đảm chặn mọi chuyển khoản ngân hàng đến muộn.
Hợp đồng API hủy của nhà cung cấp: [payOS API — Hủy link thanh toán](https://payos.vn/docs/api/).

## Khoản nghi trả trùng

Webhook/receipt đối soát hợp lệ có reference mới, đúng link, VND, số tiền dương, thời gian hợp lệ,
đến cho một hóa đơn đã Paid và có Payment thành công sẽ:

- Lưu receipt với Outcome=DuplicatePayment.
- Tạo đúng một PaymentReviewCases cho receipt, liên kết hóa đơn và payment đã ghi trước.
- Thông báo các Admin đang hoạt động trong cùng transaction.
- Không tạo Payment thứ hai, không cộng thêm doanh thu hoặc đổi PaidAtUtc của lần trả đầu.
- Giữ trạng thái terminal của request cũ (Cancelled/Expired/Succeeded), ghi mã POSSIBLE_DUPLICATE_PAYMENT.

Timestamp của khoản thêm được phép sau hạn link, vì chuyển khoản đến muộn vẫn cần được xem xét.
Đây là hồ sơ **nghi trùng** để Admin xác minh, không phải kết luận tự động phải hoàn tiền.
Gửi lại cùng webhook chỉ là retry, không tạo thêm case/thông báo. Reference xung đột dữ liệu vẫn theo luồng kiểm tra B7/B8.
Các receipt không khớp link/tiền tệ/thời gian vẫn RequiresReview như trước, không gắn nhãn trả trùng khi chưa đủ căn cứ.

Khả năng tự phát hiện phụ thuộc vào việc nhận được receipt giao dịch bổ sung.
Worker B9 vẫn ưu tiên Creating/Pending; không tự quét vô hạn các đơn terminal để tìm thêm chuyển khoản.
Receipt nghi trùng cũ từ trước B10 không được tự động chuyển thành case mới trong migration này.

## API Admin

Chỉ Admin được xem và xử lý, kể cả Manager đang quản lý khu trọ cũng không được xem API này.

| API | Chức năng |
| --- | --- |
| GET /api/payment-reviews?status=Open&page=1&pageSize=20 | Danh sách chờ xử lý, cũ trước; pageSize tối đa 100 |
| GET /api/payment-reviews/{id} | Chi tiết khoản thêm và payment gốc |
| POST /api/payment-reviews/{id}/resolve | Ghi nhận kết quả Admin đã xác minh/xử lý |

`status` nhận Open hoặc Resolved. Danh sách là một mảng, phân trang bằng page/pageSize.
Chi tiết gồm InvoiceId, PaymentRequestId, số tiền thêm/reference/nguồn/thời gian thô của provider,
payment gốc (ID, phương thức, số tiền, PaidAtUtc), trạng thái và kết quả xử lý.

Ví dụ body resolve:

```json
{
  "resolution": "RefundedExternally",
  "note": "Đã kiểm tra và hoàn tiền cho khách qua ngân hàng",
  "evidenceReference": "BANK-REFUND-123"
}
```

- RefundedExternally: Admin đã thực hiện hoàn tiền bên ngoài hệ thống; bắt buộc mã chứng từ.
- NotDuplicate: Admin xác minh không phải khoản trả trùng cần hoàn; bắt buộc ghi lý do.
- Note dài 5–2000 ký tự; mã chứng từ tối đa 256 ký tự.
- Lưu ResolvedByAdminId, ResolvedAtUtc; không cho ghi đè kết quả đã chốt. Resolve lại trả 409.
- API chỉ lưu bằng chứng xử lý, **không gọi API chuyển tiền/hoàn tiền**, không tạo Payment âm,
  không tự phân bổ khoản thêm sang hóa đơn khác và không sửa doanh thu đã ghi nhận.

400: dữ liệu sai; 401: chưa đăng nhập; 403: không phải Admin; 404: case không tồn tại.

## Migration và kiểm chứng

Migration EF `20261111014953_AddPaymentReviewCases` thêm bảng, FK restrict, unique receipt,
index trạng thái/thời gian và ràng buộc trạng thái Open/Resolved. Chưa áp dụng DB triển khai.

Từ thư mục PRN232_BE, sau khi chọn đúng connection string:

```powershell
dotnet ef database update --project PRN232.Infrastructure --startup-project PRN232.Api
```

Test dùng SQLite và gateway giả lập: hủy thành công, mất phản hồi hủy, còn pending, lookup timeout/404,
Creating được tìm lại, online thắng trước/trong lúc hủy, tiền thiếu/sai dữ liệu, quyền Manager,
checkout mới xuất hiện trước commit cash, tiền đến sau cash, retry receipt, quyền Admin và lưu kết quả xử lý.
Chưa thử chuyển tiền thật hoặc tranh chấp đồng thời trên PostgreSQL.
