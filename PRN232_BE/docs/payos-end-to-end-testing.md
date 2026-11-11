# B12 — Kiểm thử liên thông thanh toán hóa đơn

## Kết quả lần chạy 11/11/2026

| Bộ kiểm tra | Kết quả |
| --- | --- |
| Liên thông FE/BE/SDK/webhook/SQLite/báo cáo | 3/3 đạt (webhook, recovery, cash) |
| Hồi quy BE | 699 đạt, 9 bỏ qua; loại bài Supabase cần connection string; E2E đã chạy riêng |
| FE Vitest + Node | 171 + 2 đạt |
| TypeScript / build FE / build BE | Đạt; build FE còn cảnh báo Vite có sẵn về config/chunk size |
| payOS merchant thật / ngân hàng / webhook public | Chưa chạy |

Log tóm tắt: `PRN232_BE/outputs/b12-e2e-final.log`, `PRN232_BE/outputs/b12-regression.log`.

## Phạm vi đã tự động hóa

Chạy từ PRN232_FE:

```powershell
npm run test:payos-e2e
```

Điều kiện: .NET SDK, Node, FE node_modules và NuGet dependencies đã restore.
Script build project test với cấu hình PayosE2E, bật RUN_PAYOS_FE_E2E riêng trong process con,
rồi chạy 3 trường hợp. Không cần khởi động BE/FE dev server hoặc nhập khóa payOS thật.

Chuỗi chạy:

```text
React UI + AuthProvider + API clients (jsdom)
 → HTTP loopback
 → Controller/Service/Repository thật của BE + JWT thật của fixture
 → SDK payOS .NET thật
 → HTTP handler merchant kiểm thử (không ra mạng)
 → webhook HMAC của fixture
 → SDK xác minh chữ ký thật
 → transaction/DB SQLite cô lập
 → API trạng thái, hóa đơn, doanh thu/công nợ
 → màn hình kết quả và màn hình Báo cáo thật của FE
```

Chỉ điều hướng trình duyệt ra website checkout được thay bằng spy; API FE không được mock trong bài này.
AuthProvider xác minh session qua /users/me. Dữ liệu booking/hợp đồng/chỉ số/hóa đơn được fixture tạo qua BE.
SDK vẫn ký yêu cầu tạo link; merchant kiểm thử kiểm tra chữ ký, client/API key thử nghiệm và số lần tạo đơn.
Response merchant có chữ ký để SDK kiểm tra. Webhook dùng bộ ký HMAC độc lập với verifier sản xuất.
Tất cả khóa/token là dữ liệu kiểm thử sinh từ fixture, không dùng cấu hình tài khoản triển khai.
Endpoints /__test chỉ tồn tại trong test assembly, bind 127.0.0.1 cổng ngẫu nhiên và dừng sau test.
Không thêm endpoint điều khiển kiểm thử vào Program.cs hoặc controller sản xuất.

## Ma trận kiểm thử liên thông

| Nhánh | Thực hiện | Điều kiện đạt |
| --- | --- | --- |
| Webhook bình thường | Tạo link từ modal; provider báo có tiền; thử chữ ký sai; gửi webhook đúng hai lần | Chữ ký sai trả 400 và không ghi tiền; đúng trả 200; một Payment, PaidAtUtc thực tế, request Succeeded, hóa đơn Paid |
| Phục hồi | Merchant lưu đơn nhưng mất phản hồi tạo link; tìm lại bằng B9; provider đã trả nhưng không gửi webhook; đối soát lại | FE Creating; chỉ một lần POST tạo đơn; GET tìm lại link; một Payment PayOS, không dùng thời gian đối soát thay PaidAtUtc |
| Tiền mặt | Tạo link; Manager record-cash; SDK cancel + GET xác nhận; nhận thêm online sau đó | Một Payment Cash; request Cancelled; khoản online thêm tạo case nghi trùng, không cộng doanh thu lần hai |

Cả ba nhánh đều thử một reference online bổ sung và gửi lại nó: một review case, một Payment cho hóa đơn.
FE mở lại trang kết quả đọc trạng thái BE, không tin status/code của query URL.
Cuối mỗi nhánh, chuyển session sang Manager và render màn hình Báo cáo lấy API thật:
doanh thu 3.355.000đ, đúng một giao dịch, công nợ bằng 0, phân nhóm khu trọ Sunrise.
Tổng tiền đã bao gồm phòng + điện + nước + internet, không chỉ tiền phòng.

Các bài test B5–B11 hiện có kiểm tra thêm: quyền user/manager/admin, current owner, hết hạn,
thu đồng thời theo điều kiện DB, dữ liệu sai, timeout, ghi tiền rollback khi notification thất bại,
callback giả, chờ webhook, hạn poll, API review và không đếm lại receipt trùng.

## Lỗi/khoảng trống đã sửa khi chạy B12

1. Trang Báo cáo FE dùng doanh thu/cột biểu đồ mock. Đã thay phần báo cáo bằng API revenue và receivables,
   hiển thị tháng doanh thu, nhóm khu trọ, khoản chưa xác định thời gian và công nợ/quá hạn.
   Manager được vào trang này; BE vẫn giới hạn dữ liệu theo owner hiện tại.
2. SQLite trả DateTime UTC với Kind=Unspecified; JSON thiếu Z làm trình duyệt ở UTC+7 hiểu sai giờ hết hạn,
   không mở checkout dù request Pending. Đã chuẩn hóa timestamp UTC của DTO tạo link và DTO trạng thái.
   Không thay đổi giá trị thời gian/PaidAtUtc đã lưu hoặc schema; PostgreSQL tiếp tục dùng timestamp with time zone.

## Chạy hồi quy

```powershell
# Trong PRN232_FE
npm test
npm run typecheck
npm run build
npm run test:payos-e2e

# Từ root repo: bỏ bài Supabase cần connection string triển khai
$env:Logging__EventLog__LogLevel__Default = 'None'
dotnet test PRN232_BE/PRN232.Application.Tests/PRN232.Application.Tests.csproj --no-restore --configuration PayosE2E --filter 'FullyQualifiedName!~SupabaseReadOnlyInspectionTests'
```

Cross-process E2E opt-in để dotnet test thông thường không phụ thuộc Node/FE node_modules.
Các test PostgreSQL riêng vẫn yêu cầu database kiểm thử phù hợp và được bỏ qua nếu chưa cung cấp.
Không chạy migration hoặc ghi dữ liệu test vào DB Supabase đang cấu hình chỉ để hoàn tất các bài này.

## Kiểm thử payOS thật — chưa thực hiện

Ở lần chạy B12 này, appsettings có PayOS.Enabled=false, không có ClientId/ApiKey/ChecksumKey.
Vì vậy kết quả trên **không xác minh** kết nối merchant production, tài khoản nhận tiền,
QR/checkout trên browser thật, ngân hàng, chứng chỉ/CORS của deployment hoặc webhook qua Internet.
SQLite không xác minh khóa/advisory lock và migration đồng thời của PostgreSQL.

Trước khi nghiệm thu trực tiếp:

1. Chọn DB kiểm thử PostgreSQL và áp dụng các migration B2–B10 bằng đúng connection string.
2. Cấu hình bộ khóa payOS ở BE bằng user-secrets/biến môi trường, bật PayOS.Enabled.
   Không đưa khóa vào FE, test fixture, log hay commit.
3. ReturnUrl và CancelUrl trỏ tới /payment-result của FE. Production dùng HTTPS và SPA fallback.
4. Webhook merchant trỏ tới URL public HTTPS của BE: /api/payments/payos/webhook.
   Xác nhận payOS gọi được endpoint và BE xác minh bằng ChecksumKey của đúng merchant.
5. Chuẩn bị Customer, Manager đúng khu trọ và một hóa đơn kiểm thử có số tiền chủ tài khoản chấp nhận thanh toán.
   Ghi số doanh thu/công nợ trước khi trả.
6. Customer mở FE, bấm Thanh toán qua payOS, đối chiếu số tiền và tài khoản nhận tại checkout.
   Người dùng tự thực hiện chuyển tiền và giữ chứng từ.
7. Quay lại FE: chờ Succeeded/Paid từ BE. Kiểm tra một Payments/Success/PayOS, PaidAtUtc là thời gian giao dịch,
   request.PaymentId đúng Payment, receipt Settled, thông báo customer + manager.
8. Kiểm tra báo cáo tháng theo giờ Việt Nam và khu trọ: doanh thu tăng đúng tổng hóa đơn,
   số giao dịch tăng một, công nợ giảm đúng số tiền. Manager cũ không thấy dữ liệu sau khi đổi owner.
9. Retry webhook/refresh FE không làm tăng thêm payment, thông báo hoặc doanh thu.
10. Thử một hóa đơn chưa trả khác: mở link rồi chuyển sang tiền mặt; chỉ thu khi B10 xác nhận hủy.
    Trường hợp khoản thêm thật cần Admin kiểm tra case/chứng từ, không tự thử chuyển tiền lần hai nếu không cần thiết.

Bằng chứng nghiệm thu cần ghi: invoiceId, paymentRequestId, orderCode, paymentLinkId, provider reference,
PaidAtUtc, PaymentId và chênh lệch báo cáo trước/sau. Không cần ghi khóa, token hoặc thông tin tài khoản cá nhân vào repo.
Chỉ đánh dấu PASS cho kết nối payOS/ngân hàng/webhook public sau khi quan sát đủ các bước thật này.
