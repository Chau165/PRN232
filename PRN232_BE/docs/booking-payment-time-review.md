# Booking payOS: timestamp đầu phút và đối soát lại có kiểm soát

## Nguyên nhân

Với request #4 trong ảnh: CreatedAtUtc=2026-11-13T14:57:19.548336Z, timestamp giao dịch=2026-11-13T21:57:00+07:00, tức 14:57:00Z. Điều kiện cũ paidAtUtc >= CreatedAtUtc không đạt dù số tiền/link khớp. Chưa có bằng chứng mọi timestamp payOS đều bị làm tròn theo phút; dữ liệu này chỉ cho thấy một timestamp ở đầu phút.

## Chính sách mới chỉ áp dụng booking

BookingPaymentTimePolicy.IsAfterCreation chấp nhận thời gian sau/tại CreatedAtUtc như trước; ngoài ra chấp nhận timestamp nằm chính xác ở đầu cùng phút tạo request (ticks chia hết cho phút, sai số dưới 60 giây). Đây là dung sai nghiệp vụ hẹp của ứng dụng, không phải khẳng định độ chính xác do payOS bảo đảm.

Không chấp nhận timestamp đầu phút trước đó hoặc timestamp có giây khác 00 nhưng vẫn trước lúc tạo request. Không nới hạn ExpiresAtUtc/ExpiryDate hoặc điều kiện timestamp <= thời điểm nhận bằng chứng. Giữ nguyên PaidAtUtc gốc do provider trả về, không sửa thành thời gian tạo order hay thời gian server nhận webhook. Cập nhật tiếp theo: hóa đơn cũng đã dùng chung policy thời gian; xem invoice-payment-time-review.md cho recheckInvoice và xử lý trả trùng.

Vẫn kiểm tra provider, VND, code 00, số tiền, order/link, customer, reference, booking Pending, không hợp đồng, không payment Success, tình trạng phòng và khóa transaction. Lỗi thời gian mới ghi BOOKING_PAYMENT_TIME_REQUIRES_REVIEW; mã cũ BOOKING_PAYMENT_REQUIRES_REVIEW vẫn được nhận diện cho xử lý thủ công do trước đây gom nhiều nguyên nhân.

## Phục hồi yêu cầu cũ

POST /api/payment-requests/{id}/reconcile?recheckBooking=true

- Chỉ Admin; Customer/Manager không được dùng tùy chọn này. Reconcile thông thường và worker không tự mở lại các review trên.
- Chỉ xét booking RequiresReview với mã lỗi thời gian mới hoặc mã chung cũ; các lỗi reference conflict, reconciliation mismatch và những review khác vẫn không được tự mở lại.
- Hỏi lại payOS bằng gateway hiện có; phải PAID, một giao dịch đúng amount/reference và dữ liệu order/link/customer/booking nhất quán.
- Giữ RequiresReview trong bước kiểm tra; không đổi thành Pending hoặc mở lại checkout để thử chữa lỗi.
- Chỉ replay receipt cũ có fingerprint/dữ liệu tương đương, Outcome RequiresReview và không có ConflictingFingerprint. Không dùng bằng chứng khác để tự giải phóng đơn cũ.
- Trong transaction ghi nhận, kiểm tra lại toàn bộ điều kiện booking và tình trạng phòng. Nếu đã có người khác giữ/thuê, booking đã hủy/lập hợp đồng hoặc dữ liệu không khớp thì vẫn review, không ghi nhận cọc/nhả phòng bằng cách ép status.
- Nếu hợp lệ: Payments Success, PaymentRequests Succeeded, booking Deposited, receipt Settled, thông báo theo luồng hiện có. Gọi lại trả AlreadyCompleted, không tạo payment thứ hai.
- Nếu yêu cầu vừa cập nhật, kết quả TooRecent (202); giữ điều kiện thời gian chờ đối soát hiện có.

Với khoản #4: sau khi build/restart BE mới và xác nhận đang dùng DB/kênh thu đúng, Admin có thể gọi API trên với id=4 trong Swagger. Đọc GET /api/payment-requests/4 sau đó. Không sửa tay DB, không tạo order mới và không thanh toán lại. Cập nhật trang payment-result để đọc kết quả. Tính năng này chưa có nút riêng trên FE; nút kiểm tra trạng thái của Customer chỉ GET.

## Kiểm thử / triển khai

Có test timestamp UTC/+07:00, phút trước, giây trước lúc tạo, mốc hết hạn, webhook gửi lặp, đối soát lại receipt legacy, phân quyền Admin-only, phòng Maintenance và kiểm tra giữ nguyên PaidAtUtc. Gateway test giả, DB test riêng, không gọi payOS hoặc thay đổi khoản #4 thật. Không có thay đổi entity/schema, không cần migration.

Nguồn đối chiếu định dạng timestamp, chữ ký webhook và GET payment request: [payOS API](https://payos.vn/docs/api/). Tài liệu không cam kết độ chính xác theo phút; dung sai nêu trên là chính sách của dự án.

Kết quả: bộ trọng tâm ban đầu 57/57 pass; sau bổ sung ca biên, hồi quy BE theo bộ lọc CI 1.401 pass, 14 skip, 0 fail. Các ca PostgreSQL/FE E2E opt-in chưa chạy trong lượt này. Không gọi payOS thật, không thay đổi payment request #4 trên DB ứng dụng.
