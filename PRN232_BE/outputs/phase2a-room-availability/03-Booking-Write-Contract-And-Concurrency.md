# Booking write contract và concurrency — Phase sau

## Hiện trạng đã xác minh

- Không có Booking create/update/deposit/cancel endpoint, service, repository hoặc expiry worker.
- Live schema: `Bookings.RoomId` integer NOT NULL, `Status` text NOT NULL, `ExpiryDate` timestamptz NOT NULL; `IX_Bookings_RoomId` không unique. Nhiều Booking có thể cùng Room.
- Booking status hiện quan sát: Pending, Deposited, Cancelled. Expiry cũ không tự đổi status.
- Không có khóa idempotency, `CreatedAt`, hoặc ràng buộc tối đa một hold có hiệu lực/Room.

Do đó Phase 2A chỉ implement read-side rule. Không tuyên bố đã tạo Pending 30 phút hoặc ngăn double-booking.

## Contract đề nghị cho Booking write API

Đây là yêu cầu thực thi cho phase write riêng, chưa phải API đã implement:

1. Caller phải đăng nhập với role/customer eligibility đã xác minh. Lấy `CustomerId` từ authenticated principal và validate record còn hoạt động; không nhận CustomerId, Booking.Status, DepositAmount, hoặc expiry do client tự chọn.
2. Server lấy một `nowUtc` từ injectable `TimeProvider` cho toàn request. Chỉ tạo status `Pending`, với `ExpiryDate = nowUtc + 30 phút`.
3. Trong một PostgreSQL transaction, khóa row Room trước mọi Booking/Contract lock, đọc lại `Room.Status` và blockers, rồi mới insert Pending. Dùng `SELECT ... FOR UPDATE` trên row `Rooms` hoặc giải pháp PostgreSQL tương đương. Mọi Booking/Contract writer phải dùng cùng khóa và cùng thứ tự lock; nếu có writer bỏ qua quy ước này, khóa không bảo vệ được khỏi race.
4. Tạo Booking và commit sau khi hold được ghi. Thêm idempotency key theo cơ chế được chốt cho write phase để retry HTTP không sinh nhiều hold; schema hiện tại chưa có field/index idempotency.
5. Pending hết hạn được coi không còn hold khi `ExpiryDate <= nowUtc`; availability query không tự update status và không giải phóng Room `Reserved`.
6. Chỉ payment/business workflow tin cậy mới chuyển sang `Deposited`. Client không thể gửi status update để tự xác nhận tiền. Cancel/refund/release phải có authorization, audit và transition hợp lệ.
7. Khi tạo Contract từ Booking, thực hiện trong cùng transaction đã khóa Room: kiểm tra Booking hiện còn hiệu lực và payment đã xác minh; tạo Contract và chuyển Booking qua trạng thái handoff đã chốt. Không có khoảng thời gian commit mà cả Booking lẫn Contract đều không block Room.
8. `GET availability` là snapshot tại thời điểm truy vấn. Giữa GET và POST Room có thể được caller khác giữ; POST phải luôn khóa và re-check.

## Reserved ownership và reconciliation

`Room.Status=Reserved` và Booking hold là hai nguồn blocker độc lập. Phase 2A không set/clear Room status từ Booking vì source/schema không cho biết ai đã đặt Reserved hoặc Booking nào sở hữu trạng thái đó. Expired Pending không đủ chứng minh được phép đổi Room sang Available.

Khuyến nghị an toàn cho write phase: Booking writer ghi hold trong Booking và giữ `Rooms.Status` do quản trị Rooms sở hữu; availability kết hợp cả hai. Chỉ owner/Admin transition Reserved qua Room management sau khi rà blockers. Nếu product muốn tự đồng bộ Reserved, cần một liên kết/ownership marker có thể audit hoặc một quy tắc owner chốt; không suy luận từ các row legacy và không chạy bulk cleanup.

## Readiness

- Read-side filtering: IMPLEMENTED.
- Pending server-side 30-minute write: NOT IMPLEMENTED — không có writer.
- PostgreSQL per-room serialization, idempotence, payment verification, Contract handoff: NOT IMPLEMENTED.
- End-to-end reservation/anti-double-booking: NOT READY.
