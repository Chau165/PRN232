# P8 — Gửi lệnh hoàn cọc

Worker chạy theo chu kỳ, chỉ nhận đơn Approved. Cấu hình kênh thu PayOS không được dùng cho kênh chi.

```json
{
  "PayOSPayout": {
    "Enabled": true,
    "DispatchEnabled": true,
    "DispatchIntervalSeconds": 30,
    "DispatchBatchSize": 10
  }
}
```

`DispatchEnabled` mặc định false. Cần cấu hình riêng ClientId, ApiKey, ChecksumKey của kênh chi bằng secrets/environment. Không commit khóa. Chỉ bật dispatch khi sẵn sàng gửi tiền thật: các đơn Approved đủ điều kiện sẽ được worker tự xử lý. Bật Enabled riêng chỉ bật gateway, chưa bật worker gửi tiền. Interval 5–3600 giây, batch 1–100; tick đầu sau một interval. Không cần migration và không có API gửi tiền công khai.

## Trình tự

1. Lấy một batch Approved chưa có attempt hoặc chỉ có attempt đã thất bại dứt khoát. Trong transaction khóa phòng rồi booking theo thứ tự dùng chung P6/P7/hợp đồng.
2. Kiểm tra lại booking Deposited, chưa có bất kỳ hợp đồng nào, người duyệt vẫn là Admin hoạt động, chứng từ cọc PayOS Success/Succeeded đúng Customer, tiền VND và số tiền được duyệt không vượt khoản đã nhận; không có checkout đang chờ/đối soát hoặc yêu cầu hoàn khác chiếm tiền gốc.
3. Chuyển đơn sang Refunding có điều kiện; tạo PayoutAttempts Creating. Lưu số tiền được duyệt, BIN/số tài khoản/tên tài khoản, mã tham chiếu có GUID và UUID idempotency, người duyệt, số thứ tự attempt, mô tả. Commit trước mọi HTTP. Unique index có sẵn cùng khóa DB và cập nhật có điều kiện chặn nhiều worker gửi một đơn.
4. Kiểm tra số dư gốc VND và estimate-credit qua gateway P5. Credit là đơn vị riêng, không ghi thành EstimatedFee VND, không coi đây là giữ chỗ số dư hay xác minh tên người nhận. Hai worker cho hai đơn vẫn có thể gặp thay đổi số dư, provider quyết định chấp nhận cuối cùng.
5. Dùng SDK 2.1.0 `Payouts.CreateAsync` để gửi một lệnh, với referenceId và `x-idempotency-key` đã lưu. SDK MaxRetries=0. Không tự sinh lại khóa khi HTTP lỗi. Không dùng thông tin tài khoản từ request FE tại bước này.
6. Đối chiếu phản hồi với reference, số tiền, BIN, số tài khoản và đúng một transaction; lưu provider payout/transaction ID và trạng thái thô theo enum. Đây chỉ là tiếp nhận lệnh: attempt Processing, đơn Refunding, chưa hủy booking hay nhả phòng, kể cả provider đã trả Succeeded. P9/P10 xác nhận và hoàn tất nghiệp vụ.

## Lỗi, dừng ứng dụng và gửi lại

| Tình huống | Attempt | Đơn hoàn |
| --- | --- | --- |
| Chứng từ/booking không còn hợp lệ khi nhận xử lý | Không tạo attempt | RequiresReview |
| Không đủ tiền gốc hoặc preflight lỗi, chưa gọi gửi | Create-failed, có CompletedAtUtc | RequiresReview |
| Đã gọi gửi nhưng timeout/cancel/lỗi/response không khớp | RequiresReview | RequiresReview |
| Nhận phản hồi khớp | Processing | Refunding |
| Process chết sau commit claim | Creating giữ nguyên và giữ khóa claim | Refunding |

Không tự chuyển từ RequiresReview về Approved hoặc gửi lại attempt Creating/Processing/RequiresReview/Succeeded. P9 phải đối soát với mã gốc trước khi quyết định phục hồi. Chỉ khi đã có bằng chứng thất bại dứt khoát và P9 cho phép đơn về Approved, P8 mới tạo attempt kế tiếp với khóa mới, giữ nguyên lịch sử cũ. Việc ghi kết quả có điều kiện tránh ghi đè kết quả do handler khác đã xử lý; khi DB lỗi sau HTTP, attempt Creating còn giữ claim và lần chạy sau không gửi lại.

Khi shutdown trong HTTP, service cố ghi trạng thái chưa xác định bằng timeout DB độc lập 10 giây. Nếu process bị kill hoặc DB không ghi được, P9 phải xử lý Creating. Không ghi exception/provider payload/tài khoản/credentials vào log. Worker không gọi provider trong transaction DB.

## Kiểm thử

Các test P8 dùng transport HTTP in-memory cho SDK và SQLite cho repository; không truy cập payOS thật, tài khoản ngân hàng hay Supabase. Kiểm tra khóa HTTP và chữ ký, snapshot đã commit trước khi gửi, nhiều worker, timeout/cancel, số dư thiếu, thay đổi chứng từ và crash sau claim. PostgreSQL opt-in kiểm tra claim đồng thời bằng các DbContext riêng trong DB tạm loopback; không dùng DB ứng dụng.

Đã chạy 24 test worker/gateway P8 và thêm 5 case kiểm tra cấu hình dispatch. Lượt hồi quy BE: 1124 pass, 4 skip (E2E và PostgreSQL opt-in). EF `has-pending-model-changes` xác nhận không đổi schema. Chưa chạy thử chuyển tiền thật; PostgreSQL test ở loopback:55432 chưa có dịch vụ nên chưa xác minh cạnh tranh trên PostgreSQL tại máy này. Bài test PostgreSQL mở rộng nằm trong `BookingRefundRequestApiTests.PostgreSQL...`, đã được filter CI postgres-tests hiện tại bao gồm.

Khi chạy hồi quy, tắt kênh thật qua biến môi trường test `PayOS__Enabled=false`, `PayOS__BookingPaymentsEnabled=false`, `PayOSPayout__Enabled=false`, `PayOSPayout__DispatchEnabled=false`; các test SDK dùng cấu hình giả riêng. Không sửa appsettings/credentials của ứng dụng để chạy test.

Tài liệu SDK chính thức: https://github.com/payOSHQ/payos-lib-dotnet và https://payos.vn/docs/sdks/back-end/net/ .
