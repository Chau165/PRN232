# 03 — Audit Room.Status và Reserved

Ngày lập/đối chiếu: 2026-10-19 (Asia/Saigon). Snapshot live Supabase: 2026-10-18 17:00:33 UTC (transaction READ ONLY, ROLLBACK). Không sửa Room, source hay schema.

## Kết luận

**BLOCKED** — Live DB có `Reserved`, nhưng Domain/Application CRUD không công nhận status này. Chỉ `Available` là status có thể làm điều kiện cần cho public rentability; status không đủ để chứng minh Booking, Contract hoặc Property hợp lệ.

## Từ vựng status đã xác minh

| Status | Live số lượng | Room CRUD | Hiển thị công khai? | Count rentable? | Kết luận |
|---|---:|---|---|---|---|
| Available | 4 | Được `RoomStatuses.All` chấp nhận; Room mới được tạo với status này | Chỉ có thể hiện nếu qua tất cả blocker/visibility gates | Điều kiện cần, không đủ | **CONFIRMED** là code value |
| Occupied | 3 | Được chấp nhận | Không | Không | **CONFIRMED** code value |
| Maintenance | 1 | Được chấp nhận | Không | Không | **CONFIRMED** code value |
| Reserved | 1 | Bị CRUD update từ chối | Không; fail closed | Không | **CONFIRMED** trong live data, semantics/nguồn tạo **UNKNOWN** |
| Giá trị khác | 0 trong snapshot | Bị CRUD update từ chối nếu dùng UpdateRoomDto | Không; fail closed | Không | Schema `text` không giới hạn nên vocabulary không đầy đủ |

Source: `PRN232.Application/Services/RoomStatuses.cs:3–10` định nghĩa Available, Occupied, Maintenance và mảng All. `RoomService.CreateAsync` dòng 21–59 kiểm tra Property/RoomType riêng rồi đặt Status=Available; `UpdateAsync` dòng 61–95 chỉ kiểm tra `RoomStatuses.All.Contains(dto.Status)` rồi gán trực tiếp. DTO là `UpdateRoomDto(..., string Status)` ở `PRN232.Application/DTOs/RoomDto.cs:17`. Không có transition matrix hoặc kiểm tra Booking/Contract trước khi đổi Status.

Live `Rooms.Status` là `text`, không có CHECK constraint; không có user trigger trên Rooms/Bookings/Contracts/Properties. Vì vậy database chấp nhận Reserved và các string khác dù CRUD từ chối.

## Reserved: status thật hay dữ liệu cũ?

**CONFIRMED:** một Room có giá trị `Reserved`. Aggregate link cho thấy Room đó có một Booking `Pending` với ExpiryDate còn ở tương lai tại lúc truy vấn. Đây là dấu hiệu hai cột có liên hệ trong dữ liệu hiện tại, nhưng không chứng minh code nào đã tạo status hoặc `Pending` có ý nghĩa giữ phòng.

**UNKNOWN:** Reserved là business state đang dùng, legacy data, hay state dự kiến. Source không có luồng Booking; không có logic đổi Room thành Reserved khi tạo/duyệt Booking, trả Available khi huỷ/hết hạn, hoặc thành Occupied khi tạo Contract.

## Phân tích transition

| Sự kiện | Hành vi trong source hiện tại | DB enforcement | Kết luận |
|---|---|---|---|
| Tạo Room | RoomService đặt Available | Status text; không trigger | **CONFIRMED** |
| Booking được tạo | Không có Booking create flow | FK cho phép nhiều Booking/Room | Không có transition |
| Booking được xác nhận/deposit | Không có flow/status transition | Không ràng buộc Status với Room.Status | Không được giả định Reserved |
| Booking bị huỷ | Không có flow giải phóng Room | Không trigger | Không được giả định Available |
| Booking hết hạn | Không có query/job đổi state | `ExpiryDate` không tự đổi Status | Live Booking hết hạn vẫn giữ Status |
| Contract bắt đầu | Không có Contract workflow đổi Room | Không trigger/constraint cross-table | Không được giả định Occupied |
| Contract kết thúc | Không có auto-expiry hoặc transition | Không trigger/constraint | Không được giả định Available |

RoomService cho phép cập nhật giữa ba giá trị trong `All` mà không kiểm tra trạng thái cũ; do đó **về code** các chuyển đổi giữa ba status này đều có thể gửi qua CRUD. Không có quy tắc ngăn `Occupied -> Available` khi Contract còn hiệu lực. Người dùng CRUD cần role Admin/Manager theo RoomsController; đây không phải approval workflow.

## Khuyến nghị reconciliation

1. Trước Phase 2, Rooms module owner xác nhận Reserved có phải status hợp lệ hay không và ai chịu trách nhiệm transition.
2. Cho đến khi chốt: public query chỉ xét đúng `Status == "Available"`; mọi status khác, kể cả Reserved và giá trị lạ, bị loại.
3. Booking/Contract phải được xét độc lập; không cho rằng đổi Room.Status tự giải quyết blocker.
4. Nếu Reserved là hợp lệ: cập nhật source vocabulary/validation và transition/tests trong một phase được duyệt; cân nhắc DB CHECK chỉ sau khi dữ liệu khác được thống kê/chuẩn hóa.
5. Nếu Reserved là legacy: cần quyết định cách sửa các row hiện tại bằng migration/data change riêng; Phase 1.5 không được phép thực hiện.

Không cần schema change chỉ để lưu Reserved vì cột hiện là text; nhưng muốn DB cưỡng chế vocabulary nhất quán thì cần constraint sau khi owner phê duyệt và dữ liệu đã được xử lý.

