# 01 — Xác minh quy tắc Booking

Ngày lập/đối chiếu: 2026-10-19 (Asia/Saigon). Snapshot live Supabase: 2026-10-18 17:00:33 UTC (transaction READ ONLY, ROLLBACK). Phạm vi: source BE hiện tại, EF mapping, schema và số liệu tổng hợp live Supabase. Không đọc PII; truy vấn live chạy `SET TRANSACTION READ ONLY` và `ROLLBACK`.

## Kết luận

**BLOCKED** — Source không có vòng đời Booking hoặc quy tắc Booking giữ phòng. Schema lưu `Status` và `ExpiryDate` nhưng không định nghĩa ý nghĩa nghiệp vụ. Quy tắc “Pending/Deposited giữ phòng đến ExpiryDate” là **PROPOSED**, cần Product Owner xác nhận trước khi trả availability công khai.

## Schema thật và bằng chứng source

Live `public.Bookings` có:

| Cột | Kiểu | Null? | Ghi chú |
|---|---|---:|---|
| BookingId | integer | Không | PK |
| RoomId | integer | Không | FK đến Rooms.RoomId |
| CustomerId | integer | Không | FK đến Users.UserId |
| DepositAmount | numeric | Không | Không đưa vào API công khai |
| Status | text | Không | Không có CHECK constraint |
| ExpiryDate | timestamp with time zone | Không | Instant thời gian; không có BookingDate hoặc ngày bắt đầu/kết thúc thuê |

EF entity: `PRN232.Domain/Entities/Bookings.cs`, `Bookings` (BookingId, RoomId, CustomerId, DepositAmount, Status, ExpiryDate). `PRN232.Infrastructure/Data/AppDbContext.cs` khai báo DbSet/PK ở dòng 21–22, 56; quan hệ tùy chọn Bookings–Contracts tại dòng 126–131.

Tìm trong source production (`PRN232.Api`, `PRN232.Application`, `PRN232.Domain`, `PRN232.Infrastructure`, loại trừ Migrations) không thấy Booking controller, service, repository, status constants, create/approve/cancel/expire handler, background worker hoặc code đọc `ExpiryDate`. `PRN232.Infrastructure/Repositories/RoomRepository.cs:84–88` chỉ kiểm tra có Booking hay không để chặn xóa Room; đây không phải logic availability.

## Status và dữ liệu live

Chỉ các giá trị hiện có trong snapshot được xác nhận; vì cột là `text` và không có CHECK nên danh sách này không phải enum đầy đủ.

| Status | Số dòng | ExpiryDate ≤ thời điểm truy vấn | ExpiryDate > thời điểm truy vấn | Ý nghĩa/hold |
|---|---:|---:|---:|---|
| Cancelled | 1 | 1 | 0 | Tên trạng thái gợi ý đã hủy; source không định nghĩa việc giải phóng |
| Deposited | 3 | 3 | 0 | Có tiền cọc; source không xác nhận đã giữ phòng |
| Pending | 1 | 0 | 1 | Đang chờ theo tên; source không xác nhận giữ phòng |

`ExpiryDate` không nullable theo live schema. Snapshot liên kết theo Room: một Booking Cancelled đã quá hạn trên Room Available; ba Booking Deposited đã quá hạn trên ba Room Occupied; một Booking Pending còn hạn trên Room Reserved. Đây là các số đếm tổng hợp, không phải định nghĩa trạng thái. Bốn Booking đều có hạn đã qua nhưng Status vẫn còn nguyên; không có dấu hiệu worker tự đổi trạng thái.

Các FK/index hiện có: FK_Bookings_Rooms_RoomId, FK_Bookings_Users_CustomerId; PK_Bookings; index thường `IX_Bookings_RoomId` và `IX_Bookings_CustomerId`. Không có unique/exclusion constraint trên RoomId hoặc status/expiry. Snapshot hiện không có Room nào chứa nhiều Booking, nhưng schema cho phép. Một `Contracts.BookingId` nullable có unique index chỉ giới hạn tối đa một Contract cho một Booking; nó không giới hạn Booking trên một Room.

## Ma trận availability Booking

| Kịch bản | Source/schema và snapshot | Booking có block? | Kết quả / phân loại / rủi ro |
|---|---|---|---|
| A. Available, không Booking | Schema cho phép; snapshot có 3/4 Room Available không có Booking | Không có bằng chứng blocker | Rentable chỉ khi Contract và Property visibility cũng đạt. **CONFIRMED** là dữ liệu mẫu; quy tắc tổng thể chưa chốt. |
| B. Available, Pending | Schema cho phép nhưng snapshot Pending còn hạn đang gắn Reserved | **Chưa xác định** | Khuyến nghị Pending còn hạn block; **PROPOSED**. Bỏ qua sẽ quảng cáo trùng; block khi không cần có thể làm thiếu phòng. |
| C. Available, “confirmed” Booking | Không có status tên Confirmed trong dữ liệu hiện tại hoặc constants | Không thể gán nghĩa Confirmed cho Deposited | Status Confirmed là **UNKNOWN/not observed**; không tự tạo trạng thái hay chuyển Deposited thành Confirmed. |
| D. Available, Cancelled | Snapshot có 1 Room Available + Cancelled đã quá hạn | Đề xuất không block | Tên status chưa đủ chứng minh chính sách giải phóng. **PROPOSED**, dù mẫu hiện tại trông nhất quán. |
| E. Available, Booking quá hạn | Snapshot có Booking hết hạn; Cancelled gắn Available, Deposited gắn Occupied | Đề xuất Booking hết hạn không block | Đặt quy tắc instant cụ thể cần owner: hết hạn tại `ExpiryDate <= asOfUtc`. Hiện source không kiểm tra thời gian. |
| F. Available, không có ExpiryDate | Cột NOT NULL | Không thể biểu diễn qua schema hiện tại | Không cần xử lý như trường hợp hợp lệ; nếu schema đổi/null lọt vào, fail closed. **CONFIRMED** theo schema. |
| G. Nhiều Booking, status khác nhau | Không có ràng buộc tối đa một Booking/Room; snapshot hiện 0 Room có nhiều Booking | Nếu có một Booking đang giữ thì block | **PROPOSED**; đếm Room duy nhất. Nhiều trạng thái đồng thời có thể gây false availability nếu chỉ xét một dòng. |
| H. Reserved, Booking đã hết hạn | Schema cho phép; snapshot Reserved hiện có Pending còn hạn, không phải tình huống này | Reserved vẫn không được tính rentable theo status | Expiry của Booking không tự chuyển Reserved. **PROPOSED** fail-closed cho tới khi Rooms owner chốt Reserved. |
| I. Hai Booking tạo đồng thời | Không có API/service xử lý và không có unique index/constraint phù hợp | Có thể cả hai request cùng qua `NOT EXISTS` | **CONFIRMED** là không có DB guard trong schema; race runtime chưa được quan sát. Phase 2 cần transaction/locking và kiểm tra lại nếu Booking creation được bổ sung. |

“Pending” còn hạn và “Deposited” hết hạn là các status có thật; “confirmed” chưa phải status đã xác minh.

## ExpiryDate: semantics và khuyến nghị

- Kiểu PostgreSQL là `timestamptz`; entity là `DateTime`. Giá trị biểu diễn một instant, không lưu múi giờ gốc của người tạo.
- Không có code so sánh, tự chuyển Status, scheduled job hoặc worker Booking trong source hiện tại.
- Live data chứng minh Booking hết hạn có thể tiếp tục giữ Status `Pending`/`Deposited`/`Cancelled`; do đó không thể coi Status đã tự hết hiệu lực.
- **PROPOSED:** nếu owner xác nhận Pending và Deposited là hold status, Booking giữ phòng khi `ExpiryDate > asOfUtc`; tại đúng thời điểm `ExpiryDate == asOfUtc` đã hết hạn (`<=` giải phóng). Dùng một `asOfUtc` UTC thống nhất cho toàn bộ query.
- Với expiry, query availability có thể tính động từ cột hiện có; không cần worker đổi Status để tính. Một worker riêng chỉ cần nếu nghiệp vụ muốn trạng thái quản trị được đóng tự động. Chưa có bằng chứng worker đang tồn tại.

## Quyết định owner còn cần

1. Status chính xác nào giữ phòng: đề xuất `Pending` và `Deposited`; `Cancelled` không giữ.
2. Expiry tại equality xử lý theo UTC instant; đề xuất `ExpiryDate <= asOfUtc` là đã hết hạn.
3. Hết hạn chỉ làm mất hiệu lực trong availability hay còn phải đổi Status bằng job.
4. Booking status không nhận diện được hoặc dữ liệu thiếu nhất quán: đề xuất không trả Room là rentable cho tới khi xử lý.

Không cần đổi schema chỉ để đọc Status/ExpiryDate theo rule đã chốt. Chống đặt đồng thời có thể cần thay đổi transaction/locking hoặc constraint sau khi có Booking workflow; chưa thể khẳng định phương án cuối do không có ngày thuê trong Bookings.

