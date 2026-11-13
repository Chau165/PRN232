# P11 — Giao diện đặt cọc và hoàn cọc

## Customer

- Vào **Lịch xem & đặt cọc** (`/my-bookings`). Booking đã cọc chưa có hợp đồng có nút **Xin hoàn cọc** khi BE xác nhận đủ điều kiện; các đơn khác có nút theo dõi.
- Form gồm lý do, ngân hàng nhận, số tài khoản (giữ nguyên số 0 đầu), tên chủ tài khoản. Số tiền cọc hiển thị để đối chiếu, không có ô tự chọn số tiền yêu cầu. BE xác định khoản gốc và số tiền.
- Chọn tên ngân hàng phổ biến; ngân hàng khác có ô mã 6 chữ số. Danh mục BIN đã đối chiếu với nguồn công khai https://api.vietqr.io/v2/banks ngày 2026-11-13. FE không gửi tài khoản tới danh mục này; dữ liệu tài khoản chỉ gửi vào API BE được cấu hình.
- Gửi xong đọc lại lịch sử từ DB. Không lưu số tài khoản/lý do vào localStorage, sessionStorage hoặc URL. Nếu mất phản hồi POST, khóa gửi tiếp và yêu cầu cập nhật trạng thái trước; không tự POST lại.
- Lịch sử cho thấy khoản yêu cầu, số tiền được duyệt, tài khoản đã che, lý do xét duyệt, thời điểm gửi/xét duyệt/hoàn tất. Sau Rejected có thể gửi lại khi BE xác nhận còn đủ điều kiện. Refunded cập nhật booking sang Cancelled qua luồng P10 và tải lại danh sách.
- Khoản cọc chưa đủ chứng từ thu trực tuyến hoặc đã có hợp đồng không có form hoàn trực tuyến; hướng dẫn liên hệ quản lý để kiểm tra. UI không chuyển một khoản thu giả lập/không rõ chứng từ thành khoản cọc payOS thật.

## Admin

- Menu **Xét duyệt hoàn cọc** (`/management/refunds`), chỉ Admin mở được. Mặc định lọc PendingReview; có bộ lọc tất cả trạng thái và phân trang 20 đơn/trang.
- Danh sách che số tài khoản. Xem chi tiết để đối chiếu payment gốc, cọc thực nhận, số tiền yêu cầu, BIN, số và tên tài khoản. API Admin hiện có quyền đọc dữ liệu tài khoản đầy đủ; không cache bền vững dữ liệu này.
- Duyệt: nhập số tiền nguyên dương không vượt khoản yêu cầu, lý do và xác nhận đã đối chiếu. Tên do Customer cung cấp chưa được ngân hàng xác minh tự động; checkbox không đổi AccountNameVerified thành true.
- Từ chối: bắt buộc lý do, gửi approvedAmount=null, không cần checkbox duyệt. Quyết định đã ghi không có nút ghi đè.
- Chặn bấm hai lần. Nếu POST lỗi/xung đột, ẩn thao tác cũ và đọc lại chi tiết trước khi cho xử lý tiếp. Phản hồi GET cũ không được ghi đè quyết định vừa gửi.
- Duyệt có thể khiến worker P8 gửi tiền khi kênh chi được bật. Trang không gọi payout trực tiếp và không tự coi Approved là đã hoàn tiền.

## Manager và tiến độ

Trang **Đặt phòng & Đặt cọc** (`/management/bookings`) hiển thị trạng thái hoàn và mở lịch sử chỉ đọc. Manager chỉ có dữ liệu của các khu đang được phân công; thay Manager làm Manager cũ mất quyền cả booking lẫn lịch sử hoàn. Không có nút xét duyệt trên màn Manager.

Không còn nút hủy trực tiếp booking đã cọc trên UI; booking Pending vẫn có thể hủy. Tạo hợp đồng bị ẩn/chặn ở cả danh sách và trang tạo khi có yêu cầu hoàn chưa bị từ chối. BE vẫn kiểm tra lại trong transaction, nên UI không phải cơ chế phân quyền cuối cùng.

| Trạng thái BE | Nhãn UI |
| --- | --- |
| PendingReview | Chờ Admin xét duyệt |
| Approved | Đã duyệt, chờ chuyển tiền |
| Refunding | Đang xử lý chuyển tiền |
| RequiresReview | Cần kiểm tra hoàn tiền; không gửi đơn/chuyển thêm |
| Rejected | Đã từ chối; hiển thị lý do |
| Refunded | Đã hoàn cọc và hủy booking |

Các panel mở đọc lại mỗi 15 giây, tối đa 20 lượt rồi hiện thông báo tạm dừng để người dùng cập nhật thủ công. Có abort/timeout, dọn timer khi rời trang, xóa dữ liệu cũ khi đổi tài khoản hoặc mất quyền. Không polling vô hạn. Các booking đã cọc hiển thị giữ phòng đến khi chốt hợp đồng/hoàn cọc, không dùng expiryDate của cửa sổ thanh toán làm hạn giữ cọc.

## Đặt cọc qua payOS

Màn `/deposit` thay lựa chọn giả lập bằng `POST /api/bookings/{id}/payment-link`, tái sử dụng link theo quy tắc BE. Chỉ mở URL checkout HTTPS đúng host pay.payos.vn, khi link Pending còn hạn và dữ liệu đúng booking/số tiền/VND. Creating, RequiresReview hoặc kết quả chưa đủ điều kiện mở checkout đưa tới màn kiểm tra, không tự báo nhận tiền.

`/payment-result` nay được đăng ký trong router, dùng chung cho booking và hóa đơn. `/payment-success` cũ chuyển tiếp tới màn xác minh này, không còn trang thành công cố định. ReturnUrl/CancelUrl hiện có của BE có thể dùng một trong hai đường dẫn; nên dùng `/payment-result`.

Kết quả lấy từ `GET /api/payment-requests/{id}`, không từ `status`, `code`, `cancel`, orderCode hoặc provider ID trên URL. Ngữ cảnh quay về checkout chỉ lưu request ID, booking/invoice ID và link ID theo từng user/tab; không lưu kết quả thanh toán. Booking DTO có latestPaymentRequestId để mở lại trạng thái từ danh sách dù mất ngữ cảnh trình duyệt hoặc booking đã quá hạn Pending. Giao diện không gọi API thanh toán giả lập để tạo khoản cọc mới.

## API đọc bổ sung cho FE

`GET /api/bookings/{bookingId}/refund-requests`: Customer sở hữu booking, Manager được phân công còn hoạt động hoặc Admin; trả lịch sử mới nhất trước bằng BookingRefundDto, chỉ số tài khoản đã che, Cache-Control no-store. Không tồn tại/ngoài phạm vi trả 404. POST tạo và GET chi tiết P6 vẫn Customer-only. API Admin P7 vẫn Admin-only.

BookingDto thêm refundStatus, canRequestRefund, latestPaymentRequestId. Đây là gợi ý trạng thái UI; các thao tác vẫn qua kiểm tra nghiệp vụ atomic ở BE. Repository trả entities/read model và service thực hiện mapping; không đưa tài khoản thô vào DTO booking chung.

P11 không đổi entity/schema, không có migration mới. Phải triển khai BE mới cùng FE; migration AddBookingRefundLedger của P10 vẫn cần có trước khi bật worker P9/P10.

## Chạy thử

1. Bật kênh thu PayOS.Enabled và PayOS.BookingPaymentsEnabled với cấu hình thật phía BE; Customer đặt cọc qua checkout, chờ booking Deposited. Không dùng payment giả lập làm chứng từ hoàn.
2. Customer mở booking → Xin hoàn cọc → nhập tài khoản và gửi; tải lại vẫn thấy đơn.
3. Admin vào Xét duyệt hoàn cọc, đối chiếu và duyệt/từ chối; kiểm tra Customer thấy lý do và trạng thái mới.
4. Với đơn duyệt, bật cấu hình kênh chi/dispatch/reconciliation P5/P8/P9 khi sẵn sàng chuyển tiền thật. Customer/Manager theo dõi, cập nhật cho đến Refunded; booking Cancelled và phòng được bỏ giữ theo P10.
5. Thử gửi/duyệt lại, mở bằng user khác hoặc Manager cũ, đổi tab/tải lại, lỗi mạng: không ghi đè quyết định và không hiển thị dữ liệu ngoài quyền.

Kiểm thử tự động dùng mock HTTP/API và DB test riêng, không gọi payOS thật/Supabase. Đã kiểm tra trực tiếp bố cục Customer/Admin trong trình duyệt với dữ liệu giả lập cục bộ; ảnh tại artifacts/p11-customer-refund.png và artifacts/p11-admin-refunds.png. Đây là ảnh QA, không phải chứng từ chuyển tiền thật. P12 kiểm thử tài khoản thật/báo cáo vẫn là bước riêng.

Kết quả xác minh: BE 1.218 test pass, 4 test skip; EF không có thay đổi model cần migration. FE toàn bộ 294 trường hợp đã pass qua lượt regression và chạy lại bộ hoàn cọc sau khi sửa truy vấn test trùng nhãn (regression 293/294, bộ hoàn cọc chạy lại 13/13); thêm 2 test Node pass. Typecheck và production build pass.
