# P5 — Kênh chi hộ payOS

Kênh chi dùng SDK payOS .NET 2.1.0 đã có trong dự án, độc lập với kênh thu `PayOS`.
`IPayOSPayoutGateway` chỉ đọc số dư và ước tính chi phí; chưa có API công khai,
chưa gửi payout, chưa cập nhật yêu cầu hoàn cọc hay lần payout trong DB.
P5 không thay đổi schema và không cần migration.

## Cấu hình

Mặc định `PayOSPayout:Enabled=false`. Cấu hình riêng cả ba khóa của kênh chi hộ đã
kích hoạt trên payOS bằng user-secrets cho local hoặc environment/secrets của deployment:

| Khóa environment | Giá trị |
| --- | --- |
| `PayOSPayout__Enabled` | `true` khi sẵn sàng dùng kênh chi |
| `PayOSPayout__ClientId` | Client ID kênh chi |
| `PayOSPayout__ApiKey` | API key kênh chi |
| `PayOSPayout__ChecksumKey` | Checksum key kênh chi |
| `PayOSPayout__TimeoutMilliseconds` | Mặc định `15000`, cho phép `1000..60000` |
| `PayOSPayout__ValidateDestinationOnEstimate` | Mặc định `true` |

Với user-secrets, dùng tên khóa dấu `:` thay cho `__`, project API có
UserSecretsId `prn232-be-api-secrets`. Không đưa khóa thật vào Git. Không tự dùng
khóa từ section `PayOS` hoặc biến môi trường mặc định của SDK khi thiếu khóa chi.
Bật kênh chi mà thiếu khóa, khóa mẫu hoặc timeout sai sẽ làm startup báo lỗi cấu hình.
Resolve gateway không gọi mạng. Kênh chi và thu có thể bật/tắt độc lập.
SDK logger của client chi bị tắt để tránh ghi khóa, dữ liệu tài khoản hay payload.

## Khả năng gateway

| Phương thức | API payOS | Kết quả |
| --- | --- | --- |
| `GetBalanceAsync` | `GET /v1/payouts-account/balance` | Số dư VND, thời điểm kiểm tra UTC |
| `EstimateAsync` | `POST /v1/payouts/estimate-credit` | `EstimatedCredit`, cờ yêu cầu kiểm tra đích, thời điểm UTC |
| `PreflightAsync` | Hai API trên | Số dư, ước tính và `HasSufficientPrincipalBalance` |

Ước tính sử dụng batch một khoản để truyền `validateDestination` theo SDK.
Đây là estimate-credit, không phải API tạo batch/payout. SDK ký request bằng
checksum key kênh chi. Mọi request có timeout, cancellation và không tự retry.
Lỗi provider được đổi thành mã nội bộ, không đưa raw response/thông tin nhạy cảm ra ngoài.

Request nội bộ gồm `ReferenceId`, `Amount` nguyên dương VND, `RecipientBankBin`
(6 chữ số), `RecipientAccountNumber` (chuỗi chữ số, giữ số 0 đầu), `Description`.
P5 hỗ trợ số tài khoản ngân hàng, chưa hỗ trợ alias tài khoản. Input sai bị chặn
trước khi gọi mạng. Validation này kiểm tra định dạng, không chứng minh tài khoản tồn tại.

## Giới hạn của kết quả

API estimate trả `estimateCredit`, không trả tên chủ tài khoản hoặc một kết quả
xác minh danh tính riêng. Vì vậy `DestinationValidationRequested=true` chỉ mô tả
tùy chọn đã gửi; `AccountNameVerified=false` và `RecipientAccountName=null`.
Không coi estimate thành công là đã khớp tên người nhận trong đơn hoàn.

Giữ nguyên đơn vị credit do provider trả về, không tự quy đổi thành phí VND hoặc
cộng vào tiền gốc. `HasSufficientPrincipalBalance` chỉ so sánh số dư VND với tiền
gốc cần hoàn; không khẳng định đủ credit, không giữ chỗ số dư và không phải quyền
phê duyệt chuyển tiền. Số dư có thể thay đổi ngay sau khi kiểm tra.

Các bước gửi payout tiếp theo cần xác minh quyền Admin, trạng thái đơn hoàn,
giao dịch gốc, số tiền được duyệt và tài khoản đích; kiểm tra lại điều kiện thực tế
trước khi gửi và lưu dữ liệu phí theo ý nghĩa provider xác nhận. Không sao chép
`EstimatedCredit` vào `PayoutAttempts.EstimatedFee` với giả định đó là phí VND.

## Kiểm thử

`PayOSPayoutChannelTests` dùng SDK thật với HTTP handler giả, chỉ cho phép hai route
đọc/ước tính trên; không gọi payOS thật hoặc chuyển tiền. Kiểm tra cấu hình độc lập,
startup validation, payload tài khoản và chữ ký header, số dư/credit, input sai,
cancellation/timeout, lỗi provider và số lần gọi.

Nguồn đối chiếu: [API payOS](https://payos.vn/docs/api/),
[SDK .NET chính thức](https://github.com/payOSHQ/payos-lib-dotnet).
