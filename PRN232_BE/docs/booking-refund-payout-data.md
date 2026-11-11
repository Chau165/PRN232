# P4 — Dữ liệu yêu cầu hoàn cọc và lần payout

## Phạm vi

Thêm BookingRefundRequests và PayoutAttempts, mapping EF, FK/index/check constraint và migration.
P4 chỉ chuẩn bị dữ liệu: chưa có API gửi/duyệt đơn, chưa gọi payOS Chi hộ, không đổi trạng thái booking,
không nhả phòng hoặc tạo Payment âm khi thêm bản ghi hoàn.
Trạng thái dùng đúng bộ tên P1. Dữ liệu tiền cọc gốc/PaymentRequest/receipt không bị sửa hoặc xóa.

## BookingRefundRequests

| Nhóm | Trường |
| --- | --- |
| Định danh | BookingRefundRequestId, BookingId, CustomerId, OriginalPaymentId |
| Số tiền | RequestedAmount, ApprovedAmount nullable, Currency=VND |
| Yêu cầu | Reason, Status mặc định ở entity PendingReview |
| Tài khoản nhận | RecipientBankBin, RecipientAccountNumber, RecipientAccountName nullable trước duyệt |
| Xét duyệt | ReviewedByAdminId, ReviewNote, ReviewedAtUtc |
| Thời gian | CreatedAtUtc, UpdatedAtUtc, RefundedAtUtc |

BookingId liên kết booking; OriginalPaymentId liên kết Payments của khoản cọc gốc; CustomerId và Admin reviewer liên kết Users.
Các FK đều Restrict. Một request có nhiều PayoutAttempts để giữ cả lịch sử thất bại và lần thành công.

- RequestedAmount là VND nguyên, dương, trong miền hỗ trợ. ApprovedAmount nếu có phải nguyên, dương và không vượt RequestedAmount.
- PendingReview không có quyết định duyệt/số tiền duyệt/ngày hoàn.
- Rejected phải có Admin, thời điểm xét và lý do từ chối; chưa có ApprovedAmount/RefundedAtUtc.
- Approved/Refunding/RequiresReview phải có ApprovedAmount, Admin/thời điểm xét và tên người nhận; chưa có RefundedAtUtc.
- Refunded phải có đủ thông tin duyệt và RefundedAtUtc.
- UpdatedAt không trước CreatedAt; ReviewedAt nằm trong khoảng đó; RefundedAt không trước ReviewedAt hoặc sau UpdatedAt.

Hai unique index Claim theo BookingId và OriginalPaymentId áp dụng cho
PendingReview/Approved/Refunding/RequiresReview/**Refunded**. Vì vậy không chỉ chặn hai đơn đang mở mà còn chặn mở đơn mới
cho booking/khoản cọc đã hoàn. Rejected không chiếm slot; gửi lại phải tạo request mới, giữ request bị từ chối trong lịch sử.
Index Status/CreatedAtUtc và CustomerId/CreatedAtUtc phục vụ danh sách Admin/Customer ở P6.

## PayoutAttempts

| Nhóm | Trường |
| --- | --- |
| Định danh nội bộ | PayoutAttemptId, BookingRefundRequestId, AttemptNumber, InitiatedByAdminId |
| Chống trùng | ReferenceId, IdempotencyKey |
| Định danh provider | Provider=PayOS, ProviderPayoutId, ProviderTransactionId, BankReference |
| Snapshot lệnh chi | Amount, Currency=VND, RecipientBankBin, RecipientAccountNumber, RecipientAccountName, Description |
| Phí | EstimatedFee, FeeAmount nullable; không âm |
| Kết quả | Status mặc định ở entity Creating, ProviderApprovalState, ProviderTransactionState, LastErrorCode, LastErrorMessage |
| Thời gian | CreatedAtUtc, UpdatedAtUtc, SubmittedAtUtc, LastCheckedAtUtc, CompletedAtUtc, TransferredAtUtc |

Thông tin tài khoản và số tiền ở attempt là bản chụp riêng, không phải thuộc tính đọc động từ request.
P7/P8 phải chụp từ dữ liệu đã duyệt và không sửa snapshot/khóa của một attempt sau khi bắt đầu gửi.
Không chứa API key/checksum key hoặc raw response ngân hàng trong các bảng này; lỗi phải được lọc trước khi lưu.

Ràng buộc:

- FK request và Admin khởi tạo đều Restrict.
- ReferenceId và IdempotencyKey unique toàn bảng, kể cả attempt thất bại.
- ProviderPayoutId và ProviderTransactionId khác null unique theo Provider.
- (BookingRefundRequestId, AttemptNumber) unique, AttemptNumber > 0.
- Unique slot theo request áp dụng Creating/Processing/RequiresReview/**Succeeded**: sau thành công không được tạo lệnh khác.
  Create-failed/Transfer-failed giữ lịch sử nhưng không chiếm slot; attempt mới phải có số thứ tự/reference/key mới.
- Amount là VND nguyên, dương, trong miền hỗ trợ. Tài khoản/tên/ngân hàng/nội dung/reference/key bắt buộc không trắng.
- Processing có ProviderPayoutId và SubmittedAtUtc, chưa có CompletedAtUtc.
- Succeeded có ProviderPayoutId, ProviderTransactionId, SubmittedAtUtc và CompletedAtUtc.
- Create-failed không có ID lệnh/giao dịch provider; có CompletedAtUtc và mã lỗi dứt điểm.
- Transfer-failed có ID lệnh, SubmittedAtUtc, CompletedAtUtc và mã lỗi; không phải trạng thái dùng cho timeout.
- Creating/RequiresReview chưa có CompletedAtUtc. Thời điểm hoàn tất không trước gửi lệnh; các mốc không vượt UpdatedAtUtc.

CompletedAtUtc là thời điểm BE xác nhận kết quả cuối. TransferredAtUtc chỉ lưu thời điểm chuyển thực tế nếu đã xác minh được,
có thể null ngay cả khi Succeeded; không tự lấy thời gian polling/CreatedAt của lệnh để giả thành thời gian chuyển tiền.
API payOS phân biệt trạng thái duyệt và trạng thái transaction; lưu hai giá trị riêng để P9 xác minh, không chỉ nhìn code=00.
Xem [API Chi hộ payOS](https://payos.vn/docs/api/).

## Các kiểm tra liên bảng phải nối ở P6–P10

FK/check constraint không thay thế nghiệp vụ hoặc xác thực người thao tác. Các bước sau phải:

1. Xác nhận booking Deposited, chưa có Contract và đúng Customer; kiểm tra lại dưới khóa room → booking.
2. OriginalPayment thực sự là khoản cọc thành công của đúng booking/customer, còn số tiền được phép hoàn.
   Payment giả lập/legacy không đủ bằng chứng nhận tiền không được tự động dùng để phát lệnh chi thật.
3. Derive số tiền từ khoản gốc; không chỉ tin RequestedAmount/OriginalPaymentId do client gửi.
4. Chỉ Admin đang có quyền được duyệt/khởi tạo lệnh; FK Users chỉ giữ danh tính, không chứng minh role Admin.
5. Khóa thông tin nhận sau duyệt; đối chiếu snapshot/Amount của attempt với dữ liệu đã duyệt trước mỗi lần gửi.
6. Chỉ bắt đầu payout khi policy P1 cho phép; timeout giữ cùng attempt/key, không tạo lần mới.
7. P10 xác nhận đúng giao dịch thành công rồi cập nhật attempt, request Refunded, booking Cancelled và thông báo cùng transaction.
   Một row Status=Refunded tự nó không thay thế bằng chứng của payout thành công.

P4 chưa nối việc chặn hợp đồng theo đơn hoàn, chưa xác thực tài khoản ngân hàng hoặc gửi thông báo; chỉ có kiểm tra giữ phòng/cancel
đã triển khai ở P3. Cần hoàn tất các bước P6–P10 trước khi công bố luồng hoàn tiền cho người dùng.

Tài khoản ngân hàng là dữ liệu nhạy cảm. Không trả thẳng entity, không log nguyên số tài khoản;
P6 cần DTO che thông tin theo quyền. Trên Supabase không công khai hai bảng này qua Data API cho anon;
thiết lập quyền/RLS phù hợp với role BE trước khi đưa dữ liệu người dùng vào. P4 chưa bổ sung mã hóa ở tầng ứng dụng.

## Migration và kiểm thử

Migration EF: `20261111165922_AddBookingRefundRequestsAndPayoutAttempts`.
Up chỉ thêm hai bảng mới, FK restrict và index/check constraint; không viết lại Payments hoặc dữ liệu cọc cũ.
Migration chưa áp dụng lên Supabase/DB triển khai. Từ PRN232_BE, sau khi chọn đúng connection string:

```powershell
dotnet ef database update --project PRN232.Infrastructure --startup-project PRN232.Api
```

Không rollback sau khi đã phát sinh hồ sơ/lệnh chi cần giữ lịch sử: Down sẽ xóa hai bảng mới.
Không dùng migration này để tự backfill hoặc đánh dấu hoàn tiền cho dữ liệu cũ.

BookingRefundDataTests kiểm tra round-trip/navigation, giữ nguyên cọc gốc, snapshot, mọi slot chống trùng,
unique reference/key/ID provider, retry sau thất bại, số tiền/ngày/trạng thái không hợp lệ, FK và restrict delete.
SQL check so sánh số qua CAST AS NUMERIC để tránh thứ tự chuỗi decimal của SQLite; PostgreSQL lưu numeric(18,2).
Test SQLite và SQL migration PostgreSQL không thay thế việc áp dụng migration hoặc kiểm tra đồng thời trên PostgreSQL thật.

Kết quả P4: build không warning/error; 127 test liên quan đạt (66 trường hợp mới cho dữ liệu hoàn/payout);
hồi quy BE 839 đạt, 9 bỏ qua, loại riêng bài Supabase cần connection string; 3/3 bài liên thông hóa đơn đạt.
Không phát lệnh chi thật hoặc áp dụng migration lên DB triển khai trong các bài này.
