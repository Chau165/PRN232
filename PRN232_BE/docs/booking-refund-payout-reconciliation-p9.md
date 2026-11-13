# P9 — Đối soát và phục hồi payout

Worker chỉ hỏi trạng thái payOS và cập nhật DB; không gọi API tạo hoặc hủy payout. Bản P9 ban đầu không đổi schema. Sau khi tích hợp P10, transaction ghi Succeeded gọi luôn bước ghi khoản hoàn, Refunded, hủy booking, bỏ giữ phòng và tạo thông báo nếu dữ liệu hợp lệ; cần migration AddBookingRefundLedger của P10 trước khi bật worker mới. Các dòng “chờ P10” dưới đây mô tả mốc xác nhận payout trước bước hoàn tất này.

## Cấu hình

```json
{
  "PayOSPayout": {
    "Enabled": true,
    "ReconciliationEnabled": true,
    "ReconciliationIntervalSeconds": 60,
    "ReconciliationBatchSize": 20,
    "ReconciliationMinimumAgeSeconds": 180,
    "ReconciliationStuckAfterSeconds": 3600,
    "RetryFailedPayouts": false,
    "MaxPayoutAttempts": 3,
    "PayoutRetryDelaySeconds": 300
  }
}
```

Kênh chi dùng ClientId/ApiKey/ChecksumKey riêng đã cấu hình ở P5. `ReconciliationEnabled` và `RetryFailedPayouts` mặc định false. Có thể đối soát khi `DispatchEnabled=false`: bật đối soát không tự gửi tiền. Muốn tự thử lại phải bật RetryFailedPayouts; P8 chỉ thực sự gửi khi DispatchEnabled cũng bật. Thay đổi cấu hình cần khởi động lại ứng dụng.

Interval 5–3600 giây, batch 1–100, minimum age 30–86400 giây và ít nhất `2 * TimeoutMilliseconds / 1000 + 30` để tránh chen vào hai bước HTTP của P8 đang chạy. Stuck age phải >= minimum age và <= 7 ngày. Max attempts 1–5 (tính cả lần đầu), retry delay 60–86400 giây. Cấu hình không hợp lệ bị từ chối khi khởi động.

## Tra cứu và chống ghi đè

- Chọn Creating, Processing, RequiresReview đã đủ minimum age; chỉ xử lý attempt mới nhất của đơn Refunding/RequiresReview.
- Có provider payout ID: GET chi tiết theo ID. Chưa có ID do mất phản hồi tạo lệnh: GET danh sách lọc bằng ReferenceId gốc, limit=2; chỉ chấp nhận đúng một kết quả, không có trang sau/total lớn hơn 1. GET 404 hoặc danh sách rỗng không chứng minh chưa chuyển tiền.
- Dùng SDK 2.1.0, kiểm tra chữ ký phản hồi qua SDK, MaxRetries=0, timeout riêng. Đối chiếu payout/transaction ID đã biết, reference của cả payout và transaction, số tiền, BIN, tài khoản và các trạng thái được hỗ trợ. Không lấy tài khoản mới từ FE.
- Khóa phòng rồi booking trong transaction ngắn. LastCheckedAtUtc là lease để các worker không cùng hỏi một attempt; token chuẩn hóa tới microsecond để so sánh chính xác khi lưu PostgreSQL. HTTP nằm ngoài transaction. Chỉ ghi kết quả nếu token lease và trạng thái vẫn khớp, đồng thời không có attempt mới hơn. Phản hồi cũ không hạ kết quả mới hoặc ghi đè Succeeded.
- Hết hạn lease sau lỗi/process chết cho phép GET lại với mã cũ; không tạo khóa mới. Lease tối thiểu bằng max(interval, timeout + 15 giây).

## Quy tắc kết quả

| Phản hồi | Attempt | Yêu cầu hoàn |
| --- | --- | --- |
| Đủ dữ liệu khớp, Completed + Succeeded, không có error | Succeeded, CompletedAtUtc | Refunding, chờ P10 |
| Pending hợp lệ: approval Drafting/Submitted/Approved/Scheduled/Processing; transaction Received/Processing | Processing | Refunding |
| Pending quá StuckAfterSeconds | RequiresReview, PAYOUT_REMOTE_STUCK | RequiresReview |
| Không tìm thấy, timeout, sai chữ ký, dữ liệu không khớp, trạng thái lạ/mâu thuẫn/OnHold/Reversed | RequiresReview | RequiresReview |
| Failure đủ bằng chứng theo quy tắc dưới đây | Transfer-failed, CompletedAtUtc | RequiresReview |

Chính sách ứng dụng cố ý thận trọng: chỉ xác định thất bại không chi tiền khi approval là Failed/Rejected/Cancelled **và** transaction Failed/Cancelled, có error code, không có bank reference và không có transactionDatetime. Có bằng chứng chuyển tiền hoặc phản hồi mâu thuẫn thì giữ để kiểm tra, không tự thử lại. Không diễn giải Reversed thành thất bại có thể chuyển lại.

Thời điểm giao dịch thực dùng parser UTC hiện có (chuỗi không offset theo giờ Việt Nam). Nếu có thời gian nhưng sai định dạng, quá tương lai hoặc trước khi tạo attempt thì đưa vào kiểm tra. Timestamp chỉ chính xác tới giây có thể thiếu phần lẻ dưới 1 giây; khi đó không tự bịa thời gian mà giữ null. Thành công thiếu thời điểm ngân hàng vẫn ghi Succeeded với TransferredAtUtc=null. CompletedAtUtc là thời điểm hệ thống xác nhận, không phải thời điểm ngân hàng.

Với lệnh Creating được tìm lại nhưng không lưu được lúc bắt đầu HTTP, SubmittedAtUtc dùng CreatedAtUtc của attempt làm mốc dự phòng theo ràng buộc schema hiện có; đây là mốc gửi xấp xỉ, không dùng làm thời điểm chuyển tiền thực.

## Thử lại có kiểm soát

Mặc định không tự thử lại. Khi bật RetryFailedPayouts, worker chỉ xem xét attempt cuối đã thất bại dứt khoát, sau retry delay và chưa đạt MaxPayoutAttempts:

1. Create-failed phải đúng lỗi trước khi gọi gửi do P8 ghi (`PAYOUT_INSUFFICIENT_PRINCIPAL` hoặc `PAYOUT_PREFLIGHT_UNAVAILABLE`), có CompletedAtUtc, không có SubmittedAtUtc/provider ID/bằng chứng chuyển tiền.
2. Transfer-failed phải có chứng từ thất bại đã xác minh; hỏi lại payOS ngay trước khi cho phép gửi mới và vẫn phải nhận cùng bằng chứng thất bại. Nếu lần hỏi lại lỗi hoặc thay đổi, giữ RequiresReview và ghi PAYOUT_FAILURE_RECHECK_REQUIRED, không sửa trạng thái terminal cũ thành một lần chuyển mới.
3. Chỉ mở lại đơn RequiresReview có số tiền/tài khoản/currency khớp snapshot, booking Deposited chưa có hợp đồng, không có lịch sử attempt chưa rõ hoặc thành công. Attempt terminal không bị mở lại.
4. P9 chuyển yêu cầu về Approved. P8 kiểm tra lại toàn bộ chứng từ cọc, quyền Admin, hợp đồng và tạo attempt tiếp theo với mã/khóa mới. Lịch sử và khóa của lần cũ giữ nguyên.

Creating không tìm thấy trên payOS **không** đủ điều kiện thử lại, dù có vẻ ứng dụng đã chết trước lúc HTTP. Giữ claim và tiếp tục hỏi bằng mã gốc; Admin cần đối chiếu với kênh chi để giải quyết. P9 không thêm API để bỏ qua bằng chứng này.

## Theo dõi và kiểm thử

Ghi LastCheckedAtUtc, trạng thái provider, provider IDs, bank reference và lỗi nội bộ đã làm sạch; không lưu exception/provider message/tài khoản vào log. Các Admin đang hoạt động nhận tối đa một thông báo kiểm tra cho mỗi attempt (ghi cùng transaction, không có tài khoản trong nội dung). Customer vẫn đọc trạng thái đơn qua API P6; chưa nhận thông báo đã hoàn tiền ở P9.

Test dùng SDK thật với HTTP in-memory cho GET theo ID/reference và chữ ký, cùng SQLite cho kiểm tra transaction, lease, stale response, timeout, missing result, hợp lệ/sai trạng thái, retry delay/limit/opt-in. Bài PostgreSQL opt-in hiện có `BookingRefundRequestApiTests.PostgreSQL...` được mở rộng để kiểm tra 6 worker tranh lease và ghi kết quả bằng các DbContext riêng trong DB tạm loopback; filter CI postgres-tests đã bao gồm bài này.

Không gọi kênh payOS thật hay Supabase để test. Khi chạy hồi quy, tắt PayOS/PayOSPayout bằng biến môi trường test, kể cả DispatchEnabled, ReconciliationEnabled và RetryFailedPayouts. Các test SDK có cấu hình/transport giả riêng.

Kết quả kiểm tra P9: bổ sung 58 case, hồi quy BE 1182 pass và 4 skip; build thành công, EF `has-pending-model-changes` không phát hiện thay đổi schema. Test PostgreSQL cạnh tranh đã mở rộng nhưng chưa chạy tại máy vì loopback:55432 không có dịch vụ. Chưa bật worker với kênh chi thật và chưa chạy thử chuyển tiền thật.

Nguồn API chính thức: https://payos.vn/docs/api/ ; SDK: https://github.com/payOSHQ/payos-lib-dotnet .
