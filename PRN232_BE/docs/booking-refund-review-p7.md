# P7 — Admin xét duyệt hoàn cọc

Các API dưới đây chỉ dành cho Admin; phản hồi `Cache-Control: no-store` vì chứa tài khoản nhận đầy đủ.

| API | Chức năng |
| --- | --- |
| `GET /api/booking-refund-requests?status=PendingReview&page=1&pageSize=20` | Danh sách mới nhất trước, trả mảng; status tùy chọn, pageSize 1–100 |
| `GET /api/booking-refund-requests/{id}` | Chi tiết khoản cọc gốc, khoản yêu cầu/được duyệt, tài khoản nhận, lý do và thông tin xét duyệt |
| `POST /api/booking-refund-requests/{id}/review` | Ghi quyết định một lần cho đơn PendingReview |

Duyệt:

```json
{ "approve": true, "approvedAmount": 3000000, "reviewNote": "Đã đối chiếu khoản cọc và tài khoản nhận" }
```

Từ chối:

```json
{ "approve": false, "approvedAmount": null, "reviewNote": "Thông tin tài khoản nhận cần được bổ sung" }
```

`reviewNote` bắt buộc cả hai trường hợp, tối đa 2000 ký tự. Số tiền duyệt là VND nguyên dương, không vượt khoản yêu cầu đã xác định từ tiền cọc thực nhận. Có thể duyệt một phần; lý do cần giải thích phần giữ lại. Tài khoản nhận được giữ nguyên từ đơn Customer, không sửa qua API xét duyệt. Tên tài khoản phải có trước khi duyệt; nếu thiếu, từ chối để Customer gửi lại. `accountNameVerified=false`: tên do Customer cung cấp, chưa được ngân hàng/payOS xác minh.

Trong transaction, BE kiểm tra Admin còn hoạt động, trạng thái đơn, khóa phòng/booking theo thứ tự dùng chung với luồng tạo hợp đồng và yêu cầu hoàn. Trước khi duyệt phải còn booking Deposited, chưa có bất kỳ hợp đồng nào, đúng Customer và đúng một khoản cọc payOS thành công có PaymentRequest Succeeded khớp tiền/VND; không có thanh toán chờ đối soát hoặc khoản hoàn khác đã chiếm tiền gốc. Đơn có payout attempt không được xét duyệt lại. Cập nhật có điều kiện PendingReview bảo đảm một quyết định thắng. Quyết định và thông báo Customer cùng transaction.

Thành công trả 200 với chi tiết và `ReviewedByAdminId`, `ReviewedAtUtc`, `ReviewNote`; 400 dữ liệu không hợp lệ, 401 chưa đăng nhập, 403 sai quyền, 404 không tồn tại, 409 trạng thái/chứng từ không còn phù hợp hoặc đã xét duyệt.

Approved chỉ có nghĩa đã duyệt, chờ P8 chuyển tiền. Không tạo payout/payment, không hủy booking, không nhả phòng. Rejected cho phép Customer gửi lại đơn. Customer GET đơn của mình nhận thêm approvedAmount/reviewNote/reviewedAtUtc, vẫn che số tài khoản. Không thay schema, không cần migration. FE quản lý đơn thuộc P11.
