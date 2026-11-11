# P2 — PaymentRequests thuộc Invoice hoặc Booking

## Entity và ràng buộc

PaymentRequests có InvoiceId nullable và BookingId nullable, nhưng DB bắt buộc **đúng một** trường có giá trị:

| InvoiceId | BookingId | Kết quả |
| --- | --- | --- |
| Có | Null | Yêu cầu thanh toán hóa đơn |
| Null | Có | Yêu cầu đặt cọc booking |
| Null | Null | Bị chặn bởi CK_PaymentRequests_ExactlyOneTarget |
| Có | Có | Bị chặn bởi CK_PaymentRequests_ExactlyOneTarget |

BookingId là FK tới Bookings.BookingId, DeleteBehavior.Restrict. Bookings có collection PaymentRequests.
Invoice, Customer và Payment tiếp tục dùng FK restrict. Đây là checkout attempt, không phải tiền đã nhận.
Tạo bản ghi Booking target không tự tạo Payment, không đổi booking thành Deposited.

Unique index riêng cho mỗi đối tượng:

- UX_PaymentRequests_InvoiceId_Active: InvoiceId khác null và Status thuộc Creating/Pending/RequiresReview.
- UX_PaymentRequests_BookingId_Active: BookingId khác null và Status thuộc Creating/Pending/RequiresReview.

Nhờ đó mỗi hóa đơn hoặc booking có tối đa một yêu cầu đang hoạt động/chưa rõ kết quả.
Một InvoiceId và BookingId trùng số vẫn là hai đối tượng độc lập.
Các attempt cũ Cancelled/Expired/Create-failed/Succeeded không chiếm slot active.
Trước khi tạo checkout mới, nghiệp vụ P3 vẫn phải kiểm tra booking đã nhận cọc/hợp đồng; index không thay thế kiểm tra đó.

OrderCode, PaymentLinkId khác null và PaymentId khác null tiếp tục unique toàn bảng, không chia theo Invoice/Booking.
Không thêm một trường loại đối tượng riêng để tránh lệch giữa loại và FK. Loại được xác định từ đúng FK có giá trị.
Không cho phép đổi đối tượng của attempt sau khi đã tạo trong các API nghiệp vụ về sau.

## Giữ tương thích với thanh toán hóa đơn

DTO/API hóa đơn giữ nguyên InvoiceId bắt buộc. Các chỗ lấy InvoiceId từ entity nullable được kiểm tra rõ ràng.
Repository cập nhật link và đối soát hóa đơn từ chối Booking target trước khi xử lý.
Worker B9 hiện chỉ chọn Invoice target; GET trạng thái/POST reconcile chưa công bố Booking target ở P2 (trả 404).
P3 sẽ mở rộng DTO, scope theo chủ khu trọ của booking và bộ xử lý booking cùng lúc với API tạo link booking.

Webhook nhận một OrderCode Booking đã có trong DB vẫn lưu receipt gắn PaymentRequestId để không mất bằng chứng.
Trong P2, nó chuyển RequiresReview với mã BOOKING_CHECKOUT_NOT_SUPPORTED; không đi qua settlement hóa đơn,
không tạo Payment, không đổi booking/room và không tạo case trả trùng thuộc hóa đơn.
Đường xử lý thật cho booking thuộc P3; P2 chưa cho customer tạo link đặt cọc payOS.

## Migration

`20261111152245_AddBookingPaymentRequestTarget`, tạo bằng EF CLI, gồm:

1. Cho InvoiceId nhận null, thêm BookingId nullable.
2. Thêm FK Booking restrict.
3. Tạo lại active index hóa đơn với điều kiện InvoiceId khác null; thêm active index booking.
4. Thêm check constraint đúng một đối tượng.

Dữ liệu hóa đơn cũ giữ nguyên InvoiceId và tự có BookingId=null; Up không xóa bảng/bản ghi hoặc thay mã đơn/link/payment.
Migration đã được tạo, **chưa áp dụng lên Supabase hoặc DB triển khai**. Áp dụng migration trước khi chạy BE mới.

Từ PRN232_BE, với đúng connection string:

```powershell
dotnet ef database update --project PRN232.Infrastructure --startup-project PRN232.Api
```

Không rollback migration này sau khi đã có Booking target. Schema cũ không thể biểu diễn những bản ghi đó;
Down do EF sinh sẽ bỏ cột BookingId và yêu cầu InvoiceId bắt buộc trở lại. Khi đã có dữ liệu booking,
cần kế hoạch bảo toàn lịch sử hoặc migration sửa tiến, không dùng database update về phiên bản cũ.

## Kiểm chứng

BookingPaymentRequestTargetTests kiểm tra:

- Booking target round-trip, navigation, không tự ghi nhận tiền.
- XOR: không có/có cả hai đối tượng đều bị DB chặn.
- Booking FK tồn tại, xóa booking có lịch sử bị restrict.
- Active booking index chặn Creating/Pending/RequiresReview trùng.
- Cho phép lịch sử attempt và slot Invoice/Booking độc lập.
- Mã đơn, link, payment không được trùng giữa hai loại.
- Các đường hóa đơn không xử lý nhầm Booking target; webhook booking được giữ để kiểm tra.
- Snapshot khớp model PostgreSQL; SQL Up có constraint/index/FK và không xóa dữ liệu hóa đơn.

Các ràng buộc được chạy trên SQLite; SQL migration được sinh cho PostgreSQL.
Việc áp dụng migration và kiểm tra concurrency thực trên PostgreSQL/Supabase chưa được thực hiện trong P2.

Kết quả kiểm chứng P2: build không warning/error; 43 test model/schema đạt; hồi quy BE 743 đạt, 9 bỏ qua
(loại bài Supabase cần connection string; nhóm E2E chạy riêng); 3/3 bài liên thông hóa đơn B12 đạt.
