# 04 — Quy tắc khả dụng phòng

## Bằng chứng trạng thái và luồng hiện hành

| Entity | Source code | Live values quan sát | Diễn giải |
|---|---|---|---|
| Rooms.Status | RoomStatuses chỉ định Available, Occupied, Maintenance; RoomService tạo phòng là Available và chỉ cho update các giá trị trong All | Available, Maintenance, Occupied, Reserved | Reserved có dữ liệu thật nhưng CRUD hiện từ chối; DB Status là text, không có CHECK constraint nên danh sách live không chứng minh vocabulary đầy đủ |
| Bookings.Status | Entity là string; không có Booking controller/service/repository hoặc constants | Cancelled, Deposited, Pending | Đây là values đang có, không xác nhận đầy đủ lifecycle hoặc trạng thái nào giữ phòng |
| Contracts.Status | Entity là string; backend không có Contract workflow; MaintenanceService còn ghi chú vocabulary chưa được xác lập | Active | Giá trị quan sát không đủ chứng minh mọi Active đều chiếm phòng hoặc các status khác không tồn tại |

Nguồn: RoomStatuses.cs:3-11; RoomService.cs:49-55, 72-95; Bookings.cs:3-16; Contracts.cs:3-23; MaintenanceService.cs:319-328; live catalog cho Status type text và không có custom CHECK constraint trên Rooms/Bookings/Contracts.

Backend không có luồng tạo/duyệt/hủy/hết hạn Booking hay tạo/chấm dứt Contract. Booking chỉ có ExpiryDate (timestamptz), không có ngày thuê; Contract có StartDate/EndDate (date). RoomService cho phép chỉnh Status thủ công, không đồng bộ với Booking/Contract. MaintenanceRepository.GetContractAsync lấy Contract theo ID; MaintenanceService kiểm tra ngày hiện tại UTC nằm trong khoảng StartDate..EndDate, hai đầu inclusive, nhưng ghi rõ chưa biết vocabulary Status. Đây là quy tắc cho maintenance, không phải quy tắc xác nhận rentability.

## Phân tích các tình huống bắt buộc

| # | Dữ liệu tình huống | Kết luận an toàn hiện tại | Quy tắc đề xuất / cần xác nhận |
|---|---|---|---|
| A | Status Available; không Booking liên quan; không Contract hiệu lực | Chưa đủ visibility rule của Property | Có thể thuê nếu Property được xác nhận công khai |
| B | Available; Booking đang giữ phòng | UNKNOWN: backend không định nghĩa “đang giữ” | Đề xuất Pending/Deposited với ExpiryDate > now UTC giữ phòng; PO phải xác nhận |
| C | Available; Contract Active có ngày hiệu lực | Không thể chỉ tin Status Available | Đề xuất loại phòng nếu Status=Active và StartDate ≤ asOfDate ≤ EndDate; PO xác nhận Active semantics |
| D | Occupied; không Contract hiệu lực | Dữ liệu không nhất quán; không quảng cáo | Loại khỏi public set và ghi log sửa dữ liệu |
| E | Available; Booking Cancelled lịch sử | Không nên bị block chỉ vì Booking tồn tại | Cancelled không block; kiểm tra bằng status chính xác sau khi lifecycle được chốt |
| F | Available; Contract hết hạn/đã kết thúc | Không nên để Contract lịch sử block vĩnh viễn | Chỉ block khi status/date khớp rule; ngày EndDate inclusive theo maintenance code, nhưng áp vào thuê cần xác nhận |
| G | Nhiều Booking khác status | Không thể coi mọi Booking là blocker | Chỉ status được PO xác nhận và chưa hết ExpiryDate mới block |
| H | Contract tương lai | Không cản “available now” theo predicate ngày hiện tại; nhưng có thể đã được cam kết tương lai | Phase 2 MVP không hứa ngày chuyển vào tương lai; chốt có giữ chỗ tương lai không |
| I | Booking/Contract chồng ngày | Không có conflict resolution code | Fail closed: bất kỳ hold/Contract hiệu lực phù hợp đều loại phòng; ghi nhận xung đột để quản trị |

Status live được chụp tại thời điểm audit, không được coi là enum đầy đủ. Room.Status=Reserved phải được loại khỏi public, nhưng hiện CRUD không cho cập nhật trạng thái này.

## Predicate đề xuất — chưa phải behavior hiện tại

~~~text
IsRoomRentableNow(room, asOfUtc):
  property is explicitly public
  AND room.Status == "Available"
  AND no Contract where
      Contract.RoomId == room.RoomId
      AND Contract.Status == "Active"          [cần PO xác nhận semantics]
      AND StartDate <= UTC-date(asOfUtc) <= EndDate
  AND no Booking that product rules say holds the room
      AND Booking.ExpiryDate > asOfUtc
~~~

Ứng viên bảo thủ cho blocker Booking là Pending hoặc Deposited chưa hết hạn, Cancelled không block. Đây là PROPOSED, không phải kết luận từ code. Nếu product không xác nhận được status hold, API không được khẳng định số phòng/giá thuê an toàn. Với Status khác Available (gồm Reserved, Occupied, Maintenance, giá trị lạ), fail closed và không quảng cáo.

## Ba nghĩa khác nhau của availability

- **Available now:** predicate theo UTC instant hiện tại và Contract date hiệu lực hiện tại; đây là phạm vi MVP đề xuất.
- **Available for move-in date:** cần đối chiếu Contract/Booking theo ngày tương lai. Booking không có ngày move-in, nên chưa tính tin cậy.
- **Available for rental period:** cần kiểm tra khoảng ngày yêu cầu giao với StartDate/EndDate và chính sách đặt trước. Không có endpoint hoặc policy hiện hành.

Phase 2 MVP chỉ nên cung cấp “có thể thuê ngay” sau khi chốt status Booking, hiệu lực Contract và Property visibility. Nếu chưa chốt, đánh dấu API Phase 2 BLOCKED; không dùng Rooms.Status == Available một mình.