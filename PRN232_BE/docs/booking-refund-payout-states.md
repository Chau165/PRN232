# P1 — Trạng thái booking, yêu cầu hoàn cọc và lần gửi payout

## Phạm vi P1

P1 bổ sung constants, bảng chuyển trạng thái và policy nghiệp vụ thuần trong Application, kèm test.
Chưa thêm entity/migration, endpoint hoàn cọc, worker payout hoặc gọi chuyển tiền.
Các API booking/contract hiện tại chưa đọc policy này; cần nối các kiểm tra dưới đây vào transaction
ở P6–P10, sau khi P2–P4 có dữ liệu booking checkout/refund/payout.

Hiện CancelAsync vẫn cho Manager/Admin hủy booking đã cọc trực tiếp theo luồng cũ.
P6–P10 phải đóng nhánh này cho booking có khoản cọc thực: chuyển sang yêu cầu hoàn, không dùng cancel
để bỏ qua payout. Contract creation cũng phải đọc yêu cầu hoàn dưới cùng thứ tự khóa room/booking.
Không bật luồng payout trước khi các kiểm tra đó được nối và kiểm thử.

## 1. Booking: tình trạng giữ phòng

Giữ các giá trị hiện có trong BookingStatuses, không thêm Refunding/Refunded vào booking.

| Status | Ý nghĩa |
| --- | --- |
| Pending | Chờ cọc, giữ phòng đến ExpiryDate theo thời hạn booking hiện hành |
| Deposited | Đã nhận cọc, giữ phòng không giới hạn thời gian đến khi lập hợp đồng hoặc hoàn tất hủy |
| Cancelled | Đã hủy; không còn giữ phòng bởi booking này |

Pending → Deposited chỉ sau khi xác minh tiền cọc thực nhận; Pending → Cancelled phải kiểm tra checkout đang hoạt động.
Deposited → Cancelled theo luồng hoàn cọc chỉ khi xác nhận payout thành công và chưa có hợp đồng.
Không dùng ExpiryDate để hết hạn booking Deposited. RoomRentabilityPolicy hiện đã giữ Deposited không phụ thuộc ExpiryDate.
Đã lập hợp đồng được xác định bằng quan hệ Contract hiện có; không tạo thêm status Contracted trong P1.

Trong mọi trạng thái yêu cầu hoàn đang xử lý, booking vẫn Deposited và phòng vẫn được giữ.
Nhả phòng nghĩa là không còn bị booking này giữ; vẫn phải áp dụng policy chung về trạng thái phòng và hợp đồng khác.

## 2. BookingRefundRequestStatuses: quyết định và tiến trình hoàn

| Status | Ý nghĩa | Đang xử lý? |
| --- | --- | --- |
| PendingReview | Customer đã gửi, chờ Admin xét | Có |
| Approved | Admin đã duyệt; chưa có payout hoạt động hoặc lần trước đã xác nhận không chuyển tiền | Có |
| Refunding | Đã bắt đầu gửi/theo dõi payout | Có |
| RequiresReview | Kết quả hoặc thông tin payout cần kiểm tra | Có |
| Refunded | Đã xác nhận chuyển tiền hoàn và hoàn tất nghiệp vụ DB | Không, kết thúc |
| Rejected | Admin từ chối trước khi phát lệnh, có lý do | Không, kết thúc |

Các cạnh hợp lệ:

```text
PendingReview → Approved → Refunding → Refunded
       └─────→ Rejected       │
                             ├──→ RequiresReview → Refunding / Refunded
                             └──→ Approved ←────── RequiresReview
```

Refunding/RequiresReview chỉ trở lại Approved khi xác minh lần payout trước đã thất bại dứt điểm,
không chuyển tiền và không có lệnh chi chưa rõ kết quả khác. Không trở về Approved chỉ vì timeout/404.
Approved không được nhảy thẳng Refunded: phải có attempt và bằng chứng chuyển tiền.
Không được Rejected sau khi đã bắt đầu phát lệnh để tránh coi một lệnh đang chuyển tiền là đã bị từ chối.
Rejected là lịch sử bất biến; nếu customer gửi lại thì tạo request PendingReview mới, không mở lại request cũ.

Một booking chỉ được có một request đang xử lý. Sau Refunded không được xin hoàn lại khoản cọc đó.
Nếu request đã Refunded nhưng booking còn Deposited vì dữ liệu không nhất quán, chặn cả hoàn mới và lập hợp đồng để xử lý.

## 3. PayoutAttemptStatuses: một lần gửi tiền

| Status | Ý nghĩa | Được tạo attempt khác? |
| --- | --- | --- |
| Creating | Đã lưu ý định gửi; chưa biết chắc payOS đã tạo lệnh hay chưa | Không |
| Processing | payOS đã nhận lệnh, chuyển tiền chưa có kết quả cuối | Không |
| Succeeded | Đã xác minh giao dịch chuyển tiền thành công | Không |
| Create-failed | Có bằng chứng payOS từ chối/không tạo lệnh; không có chuyển tiền | Có, khi request vẫn Approved và các kiểm tra khác đạt |
| Transfer-failed | Đã có lệnh nhưng xác nhận giao dịch chuyển tiền thất bại, không chi thành công | Có, cùng điều kiện trên |
| RequiresReview | Dữ liệu không khớp hoặc kết quả cần xác minh thêm | Không |

Creating → Processing / Succeeded / Create-failed / Transfer-failed / RequiresReview.
Processing → Succeeded / Transfer-failed / RequiresReview.
RequiresReview → Processing / Succeeded / Create-failed / Transfer-failed sau khi có bằng chứng xác thực.
Succeeded, Create-failed, Transfer-failed không có cạnh đi ra.

Timeout khi tạo: giữ Creating; timeout khi theo dõi: giữ Processing. Không tự gọi là Create-failed.
Nếu bị timeout, hỏi lại bằng reference/idempotency của attempt cũ, không sinh khóa mới để gửi lại.
Nếu có thể gửi lại sau thất bại đã xác minh: tạo attempt mới và khóa mới; giữ nguyên bản ghi, reference và khóa của attempt cũ.
GET trả chưa tìm thấy đơn chưa đủ để kết luận không có chuyển tiền.

Status nội bộ không ánh xạ mù quáng từ HTTP 200, code=00 hoặc approvalState của payout.
P8/P9 phải kiểm tra trạng thái giao dịch chuyển tiền, reference, số tiền và tài khoản đích của đúng attempt.
P1 chỉ định nghĩa cạnh chuyển trạng thái; không cho client tự gửi status để coi là bằng chứng ngân hàng.

## 4. Chính sách phối hợp ba loại trạng thái

BookingRefundPolicy có các hàm:

- CanRequestRefund: Deposited, có payment cọc thành công, chưa có Contract; chưa có request hoặc request gần nhất Rejected.
- CanCreateContract: Deposited, chưa có Contract; chưa có request hoặc request gần nhất Rejected.
- CanStartPayout: Deposited, chưa có Contract, request Approved; chưa có attempt hoặc attempt gần nhất Create-failed/Transfer-failed.
- CanFinalizeCancellation: Deposited, chưa có Contract, request Refunding/RequiresReview, attempt Succeeded.

Status lạ không mở quyền lập hợp đồng/hoàn tiền/phát lệnh. Các hàm không thay thế kiểm tra role:
Customer chỉ gửi request của mình; Admin xét duyệt/phát lệnh; Manager chỉ xem đúng khu trọ theo scope đã chốt.
Số tiền, tổng hoàn trước đó, tài khoản nhận và quyền người thao tác phải được kiểm tra từ DB tại P6–P10.
hasSuccessfulDeposit phải dựa trên bằng chứng khoản cọc thực nhận. Payment Success từ cổng giả lập cũ
không đủ căn cứ để tự động chuyển tiền thật; dữ liệu legacy thiếu bằng chứng cần xác minh trước khi cho payout.

Ví dụ:

| Sự kiện | Booking | Refund request | Payout attempt |
| --- | --- | --- | --- |
| Nhận cọc | Deposited | Chưa có | Chưa có |
| Customer xin hủy | Deposited | PendingReview | Chưa có |
| Admin từ chối | Deposited | Rejected | Chưa có |
| Admin duyệt | Deposited | Approved | Chưa có |
| Worker chuẩn bị gửi | Deposited | Refunding | Creating |
| Mất phản hồi tạo lệnh | Deposited | Refunding | Creating, kèm lỗi chưa rõ kết quả |
| payOS nhận lệnh | Deposited | Refunding | Processing |
| Xác nhận chuyển thất bại | Deposited | Approved | Transfer-failed, giữ lịch sử |
| Cần kiểm tra | Deposited | RequiresReview | RequiresReview |
| Xác nhận chuyển thành công và commit nghiệp vụ | Cancelled | Refunded | Succeeded |

Sau payout thành công, P10 phải ghi trạng thái attempt/request/booking, bản ghi hoàn và thông báo một cách nhất quán.
Không thể có transaction chung với ngân hàng; nếu tiền đã đi nhưng DB lỗi, P9 hỏi lại cùng attempt và P10 hoàn tất idempotent.
Không gửi lại payout chỉ vì transaction cập nhật DB thất bại.

## 5. Kiểm thử và bàn giao

BookingRefundStateTests kiểm tra: duyệt không đồng nghĩa hoàn; request đang xử lý chặn hợp đồng/yêu cầu trùng;
từ chối cho phép request mới; booking có hợp đồng/không có cọc không được xin hoàn;
timeout và trạng thái không rõ chặn chi lần hai; retry tạo attempt mới;
chỉ payout thành công mới đủ điều kiện hủy và trạng thái kết thúc không bị mở lại.

Các bước tiếp theo dùng chính các constants cho entity mặc định, check constraint, unique index request/attempt hoạt động,
DTO và handlers; không tạo thêm bộ tên trạng thái khác trong controller/FE.
