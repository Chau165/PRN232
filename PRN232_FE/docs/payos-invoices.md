# B11 — Thanh toán hóa đơn qua payOS

## Cấu hình chạy

FE không cần SDK hoặc khóa payOS. BE phải có cấu hình payOS hợp lệ, B5–B10 và các migration tương ứng.
Trong cấu hình BE, đặt cả `PayOS:ReturnUrl` và `PayOS:CancelUrl` tới trang kết quả của FE:

```text
Development:
http://localhost:8443/payment-result

Production:
https://<ten-mien-frontend>/payment-result
```

HTTP loopback chỉ được BE chấp nhận ở Development/Testing. Production dùng HTTPS.
Hosting FE cần SPA fallback về index.html cho /payment-result; API proxy/CORS như cấu hình hiện có.
Đảm bảo webhook payOS trỏ tới BE và B9 được cấu hình nếu muốn phục hồi khi webhook thiếu/chậm.
FE không tự đăng ký webhook hoặc thay đổi cấu hình merchant.

## Luồng customer

- Hóa đơn chưa trả → Thanh toán → Thanh toán qua payOS.
- FE gọi POST /api/invoices/{id}/payment-link, không gửi số tiền/phương thức giả lập.
- Pending có URL payOS HTTPS và hạn còn hiệu lực: chuyển cùng tab tới checkout.
- Creating/không có URL: chuyển /payment-result?requestId={internalId} để theo dõi.
- Trang kết quả gọi GET /api/payment-requests/{id}; chỉ BE xác nhận kết quả.
- Succeeded + invoice Paid mới hiển thị thành công qua payOS. Invoice Paid do tiền mặt được mô tả là hóa đơn đã thanh toán.
- RequiresReview không cho mở checkout; hướng dẫn liên hệ quản lý và không trả thêm.
- Callback có cancel=true không tự chuyển request sang Cancelled; status/code/orderCode/id của URL không phải bằng chứng thanh toán.
- Nút Về hóa đơn của tôi tải lại danh sách từ BE, không tự sửa invoice thành Paid khi vừa tạo link.

FE lưu requestId/invoiceId và ID link checkout trong sessionStorage theo tài khoản và tab trước khi rời website.
Context sống qua reload/checkout/đăng nhập lại trong cùng tab, không chứa khóa hoặc kết quả thanh toán.
Nếu callback có id link, nó phải khớp link đã lưu trước khi dùng context. Nếu thiếu context hoặc mở tab khác,
trang hướng dẫn quay về hóa đơn, không đoán request hoặc hiển thị thành công theo query URL.
Trang hóa đơn có nút Kiểm tra thanh toán cho yêu cầu gần nhất lưu trong tab.
Mỗi tab hiện lưu một yêu cầu gần nhất; các yêu cầu khác vẫn được BE quản lý và có thể lấy lại qua POST tạo link cùng hóa đơn.

GET trạng thái được gọi nối tiếp mỗi 4 giây, tối đa 30 lần/khoảng 2 phút, mỗi GET có timeout 15 giây.
Dừng ở trạng thái cuối hoặc lỗi 401/403/404/429; hủy fetch/timer khi rời trang.
Chờ quá lâu chỉ tạm dừng tự kiểm tra, không báo thanh toán thất bại; có nút kiểm tra lại thủ công.
Chỉ mở URL thuộc https://pay.payos.vn/web/{id}; không mở URL từ query callback.

## Manager

Thao tác ghi tiền mặt hiện có hiển thị rõ bước kiểm tra/hủy link trong lúc xử lý.
409 từ B10 không được thông báo là đã thu thành công: FE cảnh báo online có thể đã trả hoặc chưa hủy xong,
đồng thời tải lại danh sách hóa đơn (online có thể vừa được B10 ghi nhận trong lần gọi này).
Khóa các nút ghi tiền mặt khi đang có một thao tác xử lý.

## Kiểm thử

```sh
npm run typecheck
npm test
npm run build
```

Test FE giả lập API và chuyển trang; không tạo link hoặc chuyển tiền trên tài khoản payOS thật.
Kiểm thử trực tiếp cần: hóa đơn thật thuộc customer, payOS enabled, callback/webhook đúng URL,
thử checkout thành công, đóng/hủy checkout, webhook chậm và Manager ghi tiền mặt khi còn link.
Màn hình Admin xử lý trả trùng và chuyển booking deposit sang payOS chưa thuộc B11 này.
