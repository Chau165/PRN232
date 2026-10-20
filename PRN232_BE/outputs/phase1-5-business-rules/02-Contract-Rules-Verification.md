# 02 — Xác minh quy tắc Contract

Ngày lập/đối chiếu: 2026-10-19 (Asia/Saigon). Snapshot live Supabase: 2026-10-18 17:00:33 UTC (transaction READ ONLY, ROLLBACK). Live schema và dữ liệu được đọc trong transaction read-only rồi rollback; chỉ báo cáo aggregate counts.

## Kết luận

**BLOCKED** — Có thể xác định được phép kiểm tra ngày trong module Maintenance, nhưng source không có Contract lifecycle hoặc rentability query. Status `Active`, hiệu lực tương lai, kết thúc sớm, precedence Status/date và chặn hợp đồng chồng lấn chưa được quy định cho public browsing.

## Schema và source hiện tại

`public.Contracts`:

| Cột | Kiểu | Null? | Bằng chứng |
|---|---|---:|---|
| ContractId | integer | Không | PK |
| RoomId | integer | Không | FK đến Rooms.RoomId |
| CustomerId | integer | Không | FK đến Users.UserId |
| BookingId | integer | Có | FK đến Bookings.BookingId; unique index, nên một Booking gắn tối đa một Contract |
| ProcessedByStaffId | integer | Có | FK đến Users.UserId |
| StartDate / EndDate | date / date | Không / Không | Ngày, không phải timestamp; EndDate không thể null |
| MonthlyRent / DepositAmount | numeric | Không | Không dùng để xác định Rentable |
| Status | text | Không | Không có CHECK constraint |

Entity: `PRN232.Domain/Entities/Contracts.cs:3–18`; mapping: `PRN232.Infrastructure/Data/AppDbContext.cs:56, 71–80, 126–131`. Live có FK/index theo RoomId nhưng index `IX_Contracts_RoomId` không unique. `IX_Contracts_BookingId` unique chỉ trên BookingId nullable. Không có check `StartDate <= EndDate`, unique room-active contract hoặc exclusion constraint ngày.

Source dùng ngày Contract tại `PRN232.Application/Services/MaintenanceService.cs`, `ValidateContractDate` dòng 319–328: lấy `DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime)`, từ chối nếu `today < StartDate || today > EndDate`; nghĩa là inclusive hai đầu theo UTC date trong luồng Maintenance. Comment ngay trong method nói vocabulary Contract Status chưa được xác lập. Repository chỉ tải Contract liên quan đến Maintenance (`PRN232.Infrastructure/Repositories/MaintenanceRepository.cs:77–81`). Không tìm thấy Contract controller/service/repository lifecycle, constants, termination flow, auto-expiry, hoặc Room.Status synchronization.

## Status và mẫu live

Chỉ quan sát được `Active`: 3 Contract. Cả 3 có `StartDate <= UTC-today <= EndDate` và đều thuộc Room `Occupied`. Snapshot không có Contract đã kết thúc, tương lai, cancelled, ngày đảo ngược hoặc cặp Contract chồng lấn trên cùng Room. Đây là trạng thái tại thời điểm truy vấn, không chứng minh schema/service ngăn các trường hợp khác.

Không có FK/trigger/check nào đồng bộ `Contracts.Status` với `Rooms.Status`. Không có constraint ngăn nhiều Contracts cùng Room hoặc chồng khoảng ngày; truy vấn read-only tìm thấy 0 Room có cặp khoảng ngày chồng lấn hiện tại.

## Ma trận Contract

| Kịch bản | Source/schema và snapshot | Availability đề xuất | Phân loại và việc cần chốt |
|---|---|---|---|
| A. Available, không Contract | Schema cho phép; snapshot 4 Room Available không có Contract Active hiệu lực | Có thể thuê nếu Booking và Property cũng đạt | **CONFIRMED** cho mẫu; predicate tổng thể vẫn chờ owner. |
| B. Available, Contract hiện hiệu lực | Không thấy trong snapshot; schema cho phép | Block | **PROPOSED**: Active + ngày hiệu lực; không được chỉ tin Room.Status. |
| C. Occupied, Contract hiện hiệu lực | Có 3 trường hợp trong snapshot | Không rentable theo cả Status lẫn Contract | **CONFIRMED** mẫu hiện nhất quán; không có cơ chế bắt buộc duy trì. |
| D. Available, Contract đã kết thúc | Không thấy; schema cho phép Active/status cũ tồn tại sau EndDate | Theo khuyến nghị, ngày kết thúc là dữ liệu hiệu lực; sau EndDate UTC thì Contract lịch sử không block | **PROPOSED**; cần owner chọn precedence khi Status vẫn Active. |
| E. Available, Contract cancelled | Không quan sát status Cancelled; cột text cho phép | Status terminal đã được owner định nghĩa mới không block | **UNKNOWN**; không mặc định status/value chưa thấy. |
| F. Available, Contract tương lai | Không thấy; schema có StartDate/EndDate | Với API không có ngày bắt đầu thuê, khuyến nghị Active future commitment vẫn block public listing đến EndDate | **PROPOSED**, rất quan trọng để tránh quảng cáo phòng đã hẹn thuê. Không phải cách Maintenance tính “đang trong hợp đồng”. |
| G. Available, Contract không EndDate | Không thể lưu null theo schema | Không áp dụng cho schema hiện tại | **CONFIRMED**; hợp đồng mở không thời hạn cần schema change nếu nghiệp vụ muốn. |
| H. Available, Contracts chồng lấn | Snapshot có 0 cặp chồng; không có DB/service guard | Block Room nếu bất kỳ Contract blocker nào còn hiệu lực; báo xung đột nội bộ | **PROPOSED**; không để một Contract khác làm Room có vẻ trống. |
| I. Occupied, không Contract hiệu lực | Không thấy trong snapshot; schema cho phép | Không rentable theo `Room.Status`; đánh dấu dữ liệu không nhất quán | **PROPOSED** fail-closed. Không tự đổi Status hoặc tạo Contract. |
| J. Reserved, Contract tương lai | Không thấy trong snapshot; schema cho phép | Không rentable do Reserved; future Contract cũng là blocker theo khuyến nghị tổng quát | **PROPOSED**; chờ Rooms/Contract owner quyết định ý nghĩa Reserved. |

## Date semantics: xác nhận và đề xuất

**CONFIRMED trong module Maintenance:** `StartDate` và `EndDate` đều được so sánh theo ngày UTC; ngày bắt đầu và ngày kết thúc đều được chấp nhận. Quy tắc này chỉ bảo vệ yêu cầu bảo trì; nó không đọc Contract.Status và không thể tự động trở thành business rule cho thuê.

**PROPOSED cho public listing “có thể nhận thuê”:**

- `Active` + khoảng ngày hợp lệ (`StartDate <= EndDate`) + `EndDate >= asOfUtcDate` là cam kết đang chạy hoặc sắp bắt đầu, nên loại Room khỏi listing hiện tại; cách này bao gồm Contract tương lai do API chưa nhận requested move-in/lease period.
- EndDate được coi inclusive: Contract hết hạn sau ngày `EndDate`; chỉ từ ngày UTC kế tiếp mới không còn blocker theo ngày.
- Nếu Product Owner định nghĩa chỉ “đang có người thuê ngay bây giờ”, predicate hẹp hơn là `StartDate <= asOfUtcDate <= EndDate`; nhưng nó không chứng minh Room có thể nhận một hợp đồng mới khi đã có cam kết tương lai.
- `Status=Active` nhưng EndDate đã qua, ngày đảo ngược, hoặc một status chưa được nhận diện phải được xử lý theo quyết định owner; đề xuất không quảng cáo Room khi dữ liệu mâu thuẫn cho tới khi được xác minh. Không tự cập nhật records.
- Do không có termination field/workflow, kết thúc sớm chỉ biểu diễn được nếu module thay đổi Status hoặc ngày; chưa có source chứng minh hành vi nào đang được dùng.

Không cần đổi schema để truy vấn hợp đồng theo các cột hiện có. Muốn hợp đồng không thời hạn, lưu lịch sử chấm dứt sớm có audit, hoặc cưỡng chế không overlap thì cần nghiệp vụ/schema/module riêng. Phase 1.5 không thực hiện.

## Rủi ro Phase 2

`NOT EXISTS` là cách đúng để loại Room theo Contract trong read query, nhưng không ngăn hai request cùng tạo Contract chồng lấn. Index RoomId có sẵn hỗ trợ join cơ bản; index ghép theo Status/StartDate/EndDate chỉ cân nhắc sau `EXPLAIN` trên truy vấn thật. Không tạo index trong task này.

