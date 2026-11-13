# P10 — Hoàn tất hoàn cọc và hủy booking

Khi P9 xác minh payout thành công, BE hoàn tất nghiệp vụ ngay trong transaction đang ghi nhận kết quả đối soát. P10 không gọi API chi hộ và không gửi tiền lần nữa.

## Một transaction DB

1. Giữ khóa phòng rồi booking theo thứ tự dùng chung P6–P9 và tạo hợp đồng.
2. Kiểm tra payout Succeeded có provider payout/transaction ID, trạng thái provider Completed/Succeeded và thời điểm hệ thống xác nhận; đối chiếu số tiền và tài khoản nhận với đơn đã duyệt.
3. Kiểm tra booking Deposited chưa có bất kỳ hợp đồng nào (kể cả hợp đồng đã chấm dứt); đúng Customer, đúng khoản cọc gốc PayOS Success/Succeeded, không có chứng từ thu thành công trùng hoặc payout khác chưa rõ/thành công. Không cho một khoản gốc hoặc booking có hai bản ghi hoàn.
4. Thêm BookingRefunds với đúng số tiền đã chi, liên kết đơn hoàn, attempt, payment gốc, booking và Customer. Không tạo payment âm, không sửa payment gốc thành thất bại và không xóa lịch sử thu.
5. Chuyển đơn sang Refunded, lưu RefundedAtUtc; chuyển booking sang Cancelled.
6. Bỏ giữ chỗ của booking theo RoomRentabilityPolicy. Phòng Available trở lại có thể thuê nếu không còn booking/hợp đồng khác giữ. Phòng Reserved cũ chỉ chuyển Available khi cùng policy xác nhận không còn giữ chỗ khác; giữ nguyên Maintenance/Occupied. Không hủy booking hay hợp đồng của người khác.
7. Tạo Notifications cho Customer, các Manager đang được phân công và còn hoạt động, cùng Admin đang hoạt động; mỗi người một thông báo, không chứa số tài khoản. Khóa chia sẻ phân công trên PostgreSQL bảo vệ danh sách Manager khi chốt transaction.
8. Commit toàn bộ. Lỗi tạo ledger/thông báo hay cập nhật DB làm rollback tất cả thay đổi trong transaction, kể cả xác nhận Succeeded vừa ghi ở P9. Lần kiểm tra sau dùng lại mã lệnh cũ; không chuyển tiền lại.

## Dữ liệu khoản hoàn

Bảng BookingRefunds mới gồm:

| Thuộc tính | Ý nghĩa |
| --- | --- |
| BookingRefundId | Khóa chính |
| BookingRefundRequestId, PayoutAttemptId | Đơn hoàn và lần chi thành công |
| OriginalPaymentId | Chứng từ cọc gốc được hoàn |
| BookingId, CustomerId | Booking và khách nhận hoàn |
| Amount, Currency | Khoản chi thực tế, VND nguyên dương |
| RecordedAtUtc | Thời điểm transaction hoàn tất nghiệp vụ |
| TransferredAtUtc | Thời điểm ngân hàng chuyển, có thể null khi provider không cung cấp |

Unique index riêng trên BookingRefundRequestId, PayoutAttemptId, OriginalPaymentId, BookingId chống ghi hoàn trùng. FK Restrict giữ liên kết chứng từ; check constraint kiểm tra số tiền, tiền tệ và thời gian. Không có API sửa/xóa bản ghi này. P12 có thể dùng bảng này làm nguồn báo cáo tiền hoàn.

Ví dụ cọc gốc 3.000.000, Admin duyệt 2.500.000 và payout đúng 2.500.000: ledger ghi 2.500.000; payment thu gốc vẫn 3.000.000 Success. Phần giữ lại được giải thích trong ReviewNote của quyết định P7. RefundedAtUtc/RecordedAtUtc là thời điểm ghi nhận nghiệp vụ, không thay thế thời điểm ngân hàng; TransferredAtUtc thiếu vẫn giữ null.

## Xử lý lặp và phục hồi

- Đã có ledger khớp, đơn Refunded và booking Cancelled: trả thành công, không tạo thêm ledger hoặc thông báo, không sửa thời điểm hoàn ban đầu.
- Worker P9 chạy một lượt phục hồi các payout đã Succeeded nhưng chưa có ledger trước mỗi batch đối soát. Đây là dữ liệu từ P9 trước khi triển khai P10, hoặc lần hoàn tất nghiệp vụ trước bị lỗi. Các bước nghiệp vụ vẫn trong một transaction riêng và không hỏi/gửi payout lại.
- Nhiều worker dùng cùng khóa DB, unique index và transaction. Lượt sau chỉ quan sát kết quả đã hoàn tất.
- Nếu payout thực sự thành công nhưng dữ liệu booking/hợp đồng/khoản cọc/tài khoản không còn nhất quán: giữ Succeeded làm bằng chứng tiền đã chi, đưa đơn về RequiresReview, không hủy booking/nhả phòng sai. Admin nhận một cảnh báo; worker không chuyển tiền thêm. Sau khi dữ liệu được đối chiếu đúng, lượt phục hồi có thể hoàn tất lại. Không có API bỏ qua kiểm tra hợp đồng.
- Việc sắp xếp backlog theo UpdatedAtUtc giúp một đơn không hợp lệ không chặn mãi các đơn khác.

## API và cấu hình

Không thêm API chuyển tiền hoặc hủy booking trực tiếp. Customer GET đơn của mình và Admin GET chi tiết đơn nhận thêm `refundedAtUtc` (UTC), cùng trạng thái Refunded và approvedAmount hiện có. Customer vẫn chỉ thấy số tài khoản đã che.

Dùng worker/cấu hình P9 hiện tại: PayOSPayout.Enabled=true và PayOSPayout.ReconciliationEnabled=true. Không cần bật DispatchEnabled để hoàn tất những payout đã thành công; DispatchEnabled chỉ điều khiển việc gửi mới ở P8. Các cờ mặc định không bị bật thêm bởi P10.

## Migration và triển khai

Đã sinh bằng EF CLI migration `20261113075616_AddBookingRefundLedger`. Chỉ thêm BookingRefunds; không cập nhật lại payment hoặc booking cũ trong migration. Việc hoàn tất backlog chạy qua các điều kiện nghiệp vụ của worker sau khi triển khai.

Áp dụng migration trước khi chạy BE mới với worker P9 bật. Từ thư mục gốc repo chứa PRN232_BE, kiểm tra DB đích rồi chạy:

```powershell
dotnet ef dbcontext info --project PRN232_BE/PRN232.Infrastructure --startup-project PRN232_BE/PRN232.Api
dotnet ef database update --project PRN232_BE/PRN232.Infrastructure --startup-project PRN232_BE/PRN232.Api
```

Connection string có thể bị biến môi trường/user-secrets hoặc appsettings.Development ghi đè; kiểm tra đúng host/database trước khi update. Không cần đưa password vào lệnh/commit. Migration chưa được áp dụng lên DB ứng dụng hoặc Supabase trong quá trình triển khai code này.

## Kiểm thử

Test dùng SQLite và đối soát HTTP giả lập: xác nhận ledger đúng khoản đã duyệt, hủy và mở giữ chỗ, giữ phòng bảo trì/đang ở/còn booking khác, thông báo đúng Manager hiện tại, chạy lặp/đồng thời, thiếu bằng chứng/đã có hợp đồng, phục hồi Succeeded cũ, lỗi insert thông báo rollback cả transaction, unique key DB và script migration tăng/giảm không sửa Payments.

Bài PostgreSQL opt-in `BookingRefundRequestApiTests.PostgreSQL...` kiểm tra thêm ledger/Refunded/Cancelled sau đối soát và 6 worker hoàn tất lặp; filter CI hiện tại đã bao gồm. Không dùng DB ứng dụng/Supabase hay chuyển tiền thật để test.

Kết quả: lượt hồi quy BE 1209 pass, 4 skip; sau đó thêm kiểm tra chống hết hạn sớm booking khác khi thời gian audit bị đẩy về tương lai, bộ P10 cuối cùng 28/28 pass. Build thành công, EF xác nhận snapshot khớp model sau migration. PostgreSQL cạnh tranh chưa chạy tại máy vì loopback:55432 không có dịch vụ. Migration chỉ được sinh và kiểm tra script, chưa áp dụng lên DB thật.
