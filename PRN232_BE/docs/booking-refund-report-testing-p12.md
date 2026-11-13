# P12 — Báo cáo tiền cọc và kiểm thử hoàn cọc

## Báo cáo

`GET /api/reports/booking-deposits?fromMonth=2026-11-01&toMonth=2026-11-01&propertyId=1`

Admin xem toàn bộ; Manager chỉ khu trọ được phân công hiện tại, tài khoản còn hoạt động và còn role Manager. Customer không được xem. Omit propertyId để xem tổng trong phạm vi được phép; khu trọ ngoài quyền trả 403, không tồn tại trả 404. Cache-Control no-store.

fromMonth/toMonth là ngày đầu tháng, tối đa 24 tháng liên tiếp. Mặc định từ tháng 1 tới tháng hiện tại theo Asia/Ho_Chi_Minh. Sai định dạng/range trả 400. Khoảng thời gian UTC là [đầu tháng đầu, đầu tháng sau tháng cuối), chuyển từ múi giờ Việt Nam.

Response:

- fromMonth, toMonth, propertyId.
- totals: collectedAmount, collectionCount, refundedAmount, refundCount, netCashFlow.
- unknownDate: cùng cấu trúc, không phân bổ vào tháng, không bị loại bởi bộ lọc thời gian.
- monthly: mỗi tháng trong khoảng, kể cả tháng 0 giao dịch, với totals.
- byProperty: propertyId, propertyName, totals, unknownDate. Chỉ các khu có dòng tiền phù hợp hoặc giao dịch chưa rõ thời điểm; khu không có giao dịch không tạo dòng giả.

Thu: Payments Success gắn booking, không gắn invoice/contract/reading; dùng PaidAtUtc. Bao gồm khoản thu cọc cũ đã ghi nhận, kể cả khác PayOS. Hoàn: chỉ BookingRefunds, dùng TransferredAtUtc; đơn Approved/Refunding/RequiresReview hoặc payout Succeeded chưa hoàn tất nghiệp vụ không tự tính là đã ghi sổ chi. Null thời điểm ngân hàng được giữ nguyên, không thay bằng RecordedAtUtc/CompletedAtUtc.

Đây là báo cáo dòng tiền cọc thu/hoàn, không cộng vào doanh thu hóa đơn và không phải số dư nghĩa vụ cọc còn phải hoàn. Net của tháng có thể âm nếu hoàn cọc đã thu tháng trước. Chưa tính phí payout hoặc phân bổ cọc vào hợp đồng/khấu trừ khác. Các khoản trùng thu cần xử lý riêng vẫn thể hiện nếu đã có receipt Success, không khử trùng bằng số booking. Không nối payout attempts vào tổng tiền để tránh nhân số tiền theo số lần thử.

Repository đọc hai luồng trong cùng transaction RepeatableRead trên PostgreSQL để không ghép hai snapshot khi đang hoàn tiền. Các dòng đã lọc được cộng decimal ở application; phạm vi thời gian giới hạn 24 tháng nhưng dữ liệu legacy chưa rõ ngày vẫn đọc toàn bộ trong scope. Với khối lượng lớn cần bổ sung aggregation SQL/pagination cho phần unknown-date.

FE: trang Báo cáo hiện có thêm phần Tiền cọc thu / hoàn, cùng bộ chọn tháng và Tải lại báo cáo; hiện tổng, số giao dịch, theo khu, khoản chưa rõ thời điểm. Không lộ tài khoản nhận. Có timeout 15s, abort khi rời trang/đổi user/tháng, không hiển thị snapshot của tài khoản trước.

Không thay entity/schema; P12 không có migration. Cần schema P10 BookingRefunds đã được migrate và triển khai BE mới cùng FE.

## Ma trận kiểm thử tự động

| Tình huống | Bộ test / kỳ vọng |
| --- | --- |
| Gửi đơn lặp, xét duyệt lại, booking bị lập hợp đồng | BookingRefundRequestApiTests / BookingRefundReviewApiTests: conflict, không tạo chi trùng |
| Hai worker cùng claim / crash sau claim / mất response sau provider nhận | BookingRefundPayoutTests: một submit, giữ referenceId/idempotency, không gửi lại khi chưa rõ kết quả |
| Timeout, Creating bị kẹt, lease hết hạn, phản hồi cũ | PayoutReconciliationTests: tra cứu lệnh cũ, không ghi đè kết quả mới, không retry mù |
| Thành công payout nhưng xung đột payment/contract/tài khoản | BookingRefundCompletionTests: giữ chứng cứ ngân hàng, không tự nhả phòng sai |
| Retry completion, concurrent completion, insert notification lỗi | BookingRefundCompletionTests: ledger/notice một lần, rollback đồng bộ |
| Tháng Việt Nam, số tiền thực chi, ngày ngân hàng null, receipt Failed, Approved chưa chi | DepositReportApiTests: tổng/đếm đúng, không tự điền ngày |
| Đổi Manager, Customer/anonymous, bộ lọc sai | DepositReportApiTests: scope hiện tại, 401/403/400 |
| FE đổi token/tháng, timeout, tải lại, hiển thị unknown-date | depositReport.test.tsx, managementReports.test.tsx, reports.test.tsx |
| Công cụ đối chiếu thật | verify-booking-refund-live.test.mjs: GET-only, scope/ID khớp, không coi Pending là đã thu |

Test PostgreSQL cạnh tranh trong BookingRefundRequestApiTests đã bổ sung kiểm tra báo cáo sau nhiều lần completion. Chỉ chạy trên PostgreSQL 17 local cổng 55432 bằng DB tạm riêng; không dùng Supabase/app DB. Cấu hình `PRN232_MIGRATION_TEST_POSTGRES_CONNECTION` theo quy ước test hiện có (Application Name=PRN232-Phase31B-Isolated-Test). Không bật nếu connection trỏ DB thật.

## Thử tài khoản thật — chưa thực hiện

Cần người dùng cung cấp ID booking. Không cần gửi khóa payOS hoặc tài khoản ngân hàng vào chat. Nếu đã có đơn hoàn, kiểm tra số tiền duyệt và tài khoản đích của đơn đó trên UI trước khi chi.

1. Xác nhận BE đang trỏ đúng DB, schema P10, webhook HTTPS truy cập được. Bật kênh thu PayOS và BookingPaymentsEnabled. Ban đầu giữ DispatchEnabled=false để không gửi các đơn Approved ngoài ca thử. Không bật worker trên DB dùng chung chưa kiểm tra các đơn Approved tồn đọng.
2. Chọn booking thử và số tiền cọc thực tế được đồng ý; Customer tạo link qua UI, thanh toán bằng app ngân hàng. Đối chiếu payOS và tài khoản Admin nhận. Return URL không phải bằng chứng đã thu; payment request phải Succeeded, Payments Success, booking Deposited.
3. Customer nhập tài khoản nhận trên form Xin hoàn cọc, gửi một lần. Admin đối chiếu ngân hàng/tên/số tài khoản/số tiền, ghi lý do và duyệt. Không duyệt lại hoặc tạo booking giả trong DB để giảm tiền cọc.
4. Ghi nhận bookingId, paymentRequestId, refundRequestId, approvedAmount và baseline báo cáo; xác nhận số tiền và người nhận trước khi bật dispatch. Kênh chi có cấu hình riêng; đối chiếu số dư VND/credit trên payOS. API estimate không chứng minh tên người nhận đã được xác minh.
5. Cho worker P8/P9/P10 xử lý yêu cầu được duyệt. Kiểm tra provider payout/transaction reference và tài khoản khách thực nhận. Nếu timeout, để đối soát tra cứu lệnh cũ; không sửa status DB hoặc duyệt/chuyển lại để thử.
6. Yêu cầu Refunded, booking Cancelled, ledger một dòng, room hết giữ của booking này (vẫn giữ trạng thái nếu có ràng buộc khác), thông báo đúng người. Báo cáo tăng thu/hoàn đúng tháng; không rõ thời điểm ngân hàng thì tăng unknownDate.
7. Chạy công cụ đọc bên dưới và SQL audit trong DB đúng môi trường; so với baseline và dashboard payOS/bank. Đọc lại/tải lại không tạo Payments/BookingRefunds/PayoutAttempts mới. Với tài khoản thật chỉ thử happy path; các ca lỗi/chuyển trùng/timeout cưỡng bức chạy trong mock/DB test.

Công cụ `node PRN232_BE/scripts/verify-booking-refund-live.mjs` chỉ GET ba API của hệ thống, không dùng khóa SDK, không tự gọi payOS hay chuyển tiền. Cấu hình environment: P12_API_BASE_URL (kết thúc /api), P12_CUSTOMER_TOKEN, P12_ADMIN_TOKEN, P12_BOOKING_ID, P12_PAYMENT_REQUEST_ID, P12_REFUND_REQUEST_ID, P12_MONTH (YYYY-MM). Nhập token tại máy local, không commit hoặc chia sẻ log shell chứa token. Công cụ không in token/tài khoản; lỗi chỉ trả thông báo tổng quát. Hãy xóa token environment sau khi dùng.

Kết quả công cụ xác nhận trạng thái BE và trả tổng báo cáo; không thay thế chứng từ bank/payOS hoặc chứng minh riêng một ledger đã nằm trong aggregate. `scripts/audit-booking-refunds.sql` là các SELECT kiểm tra ledger nhất quán/không trùng/unknown-date, không trả tài khoản ngân hàng. Không giả lập chữ ký webhook bằng khóa thật để ghi nhận khoản chưa nhận.

Nguồn đối chiếu kênh chi: [payOS API chính thức](https://payos.vn/docs/api/) (referenceId, x-idempotency-key, tra cứu payout); [SDK .NET](https://payos.vn/docs/sdks/back-end/net/).

## Kết quả chạy tại workspace (13/11/2026)

- Bộ BE trọng tâm P6–P10 + báo cáo P12: **117 pass, 1 skip** (PostgreSQL opt-in).
- Hồi quy BE: **1.227 pass, 12 skip, 1 fail**. Fail duy nhất là SupabaseReadOnlyInspectionTests cũ yêu cầu environment ConnectionStrings__DefaultConnection thật; không sửa/giả lập connection để làm xanh test. Các ca skip cần PostgreSQL/opt-in. Không tuyên bố hồi quy toàn bộ xanh.
- FE báo cáo: **14/14 pass**. Hồi quy FE: **296/297 pass**; test managementMaintenancePage thay ảnh hỏng bị lỗi khi chạy chung, chạy riêng toàn bộ file **10/10 pass**, không sửa production bảo trì trong P12.
- Liên thông FE → BE → SDK giả lập merchant → webhook/đối soát → DB test → báo cáo: **3/3 pass** (webhook, mất phản hồi tạo link/phục hồi, tiền mặt). Đã sửa truy vấn test E2E để chấp nhận tên khu xuất hiện ở cả bảng doanh thu và tiền cọc. Đây là mock merchant, không phải tài khoản payOS thật.
- Typecheck, production build pass; 2 test Node ngày hợp đồng và 3 test công cụ đọc pass.
- PostgreSQL 17 local cổng 55432 chưa chạy; Docker engine không sẵn sàng. Ca concurrent PostgreSQL + báo cáo đã được bổ sung vào test opt-in nhưng chưa thực thi trên provider đó trong lượt này.
- Người dùng cung cấp booking #4. Kiểm tra DB **chỉ đọc** với cấu hình Development + user secrets + environment hiện có cho thấy localhost/prn232_local, không có booking #4; public chỉ có Bookings/Payments/PaymentRequests trong nhóm bảng kiểm tra, chưa có BookingRefundRequests/PayoutAttempts/BookingRefunds. Không tự chạy migration trên DB này hoặc chuyển tiền. Đang chờ xác nhận môi trường chứa booking #4 trước khi thử thật.

P12 đã triển khai báo cáo và công cụ kiểm thử; phần xác nhận tài khoản thật và PostgreSQL opt-in còn chưa hoàn tất.
