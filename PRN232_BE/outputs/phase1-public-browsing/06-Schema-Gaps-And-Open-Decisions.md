# 06 — Gaps schema và quyết định còn mở

## Gaps đã xác nhận

| Gap | Bằng chứng | FE ảnh hưởng / tác động | Phase 2 không đổi DB? | Hướng xử lý | Ưu tiên |
|---|---|---|---|---|---|
| Chưa có Property visibility/Published | Properties không có Status/visibility; GET hiện trả tất cả | Home có thể quảng bá Property không được phép | Chỉ nếu PO xác nhận mọi Property đều public hoặc có allow-list bên ngoài | Nếu cần trạng thái xuất bản bền vững, đề xuất schema riêng sau phê duyệt | BLOCKER |
| Booking/Contract rule chưa được backend cài | Không có API/service/repo lifecycle; schema Status là text không có CHECK | Count/price có thể quảng cáo phòng đã giữ/đang thuê | Chưa, cho tới khi business rule được xác nhận | Chốt status hold, ExpiryDate, Contract.Active/date, future overlap và sync với Rooms.Status | BLOCKER |
| Room Status lệch giữa DB và code | Live có Reserved; RoomStatuses.All chỉ Available/Occupied/Maintenance | Update CRUD không quản được giá trị Reserved; trạng thái không thể làm nguồn duy nhất | API có thể fail-closed, nhưng cần quyết định cách sửa CRUD/dữ liệu | Chốt status vocabulary và trạng thái chuẩn; không tự ghi DB Phase 1 | P1 |
| RoomTypes toàn cục, không có PropertyId | RoomTypes chỉ Id/Name/Price; Room và Property FK riêng | Có thể gán nhầm RoomTypeId của Property khác; services/price theo ID đó | Có thể dùng Cách 1 nếu quy trình quản lý gán đúng ID | Ghi quy ước; validate assignment trong scope quản lý nếu được duyệt; không thêm PropertyId trong Phase 1 | P1 |
| Thiếu đơn vị tính/optional cho Service.Price | Services chỉ Name/Price | Không thể hiển thị/chốt tổng phí hàng tháng | Có, nếu chỉ trình bày tên và giá chưa xác định kỳ | Bổ sung billing unit/mandatory semantics trong quyết định schema tương lai nếu cần estimate | P1 |
| Thiếu capacity, floor, available-from, Room description | Rooms chỉ RoomCode, AreaSqm, Status, FK | Occupant filter và chi tiết hiện tại dựa mock | Có, bằng cách ẩn/không trả các field này | Thêm nguồn dữ liệu sau khi owner chốt nghiệp vụ | P1 |
| Không có Property image/Description/Rules | Properties không có các cột này; chỉ RoomImages gắn Room | Gallery/mô tả/nội quy mock có thể gây hiểu nhầm | Có, nếu dùng ảnh phòng có sourceRoomId và ẩn text không có nguồn | Ảnh Property riêng chỉ khi nghiệp vụ cần; không giả ảnh phòng là ảnh Property | P1 |
| Latitude/Longitude numeric(18,2) | Live catalog và AppDbContext decimal(18,2) | Sai số xấp xỉ kilomet, không đáng tin cho bán kính 1–10km hoặc marker sát địa chỉ | Có nếu bỏ radius/distance MVP | Đánh giá numeric(9,6) hoặc PostGIS geography + GiST khi triển khai geospatial | P2 |
| Không có paging/sort trên GET hiện tại | Controller/service trả IEnumerable/List đầy đủ | 42-result mock và danh sách không có server paging | Có thể xây trong public API mới | page/pageSize 1–50, ordering ổn định | P0 |
| RoomImages primary không được DB enforce duy nhất | Có IsPrimary bool; không có unique partial index | Có thể có nhiều primary nếu ghi ngoài service hoặc race | Có, repository chọn ổn định | Query order IsPrimary DESC, RoomImageId ASC; đánh giá constraint tương lai | P2 |
| Index chưa tối ưu cho eligibility filter | FK indexes có RoomId; không có composite Status/expiry/date | Listing có thể tốn khi dữ liệu tăng | Có cho MVP nhỏ, nhưng đo query | EXPLAIN trước; cân nhắc composite Bookings(RoomId,Status,ExpiryDate), Contracts(RoomId,Status,StartDate,EndDate) nếu plan chứng minh cần | P2 |

## Cách 1 — quyết định đã chốt, feasibility

**CONFIRMED FEASIBLE ở schema/CRUD đang kiểm tra.** Live RoomTypes không có unique constraint/index cho TypeName, không có custom check hoặc trigger. Chỉ PK RoomTypeId. RoomTypeService.Trim tên, chặn tên trống và giá âm, không chặn trùng. Vì vậy có thể có nhiều RoomTypes cùng TypeName nhưng khác RoomTypeId/Price mà không sửa schema.

Giới hạn đi kèm: không có PropertyId trên RoomTypes, nên dữ liệu không ép giá/type riêng cho một Property. Phân giá là convention do Rooms.RoomTypeId; admin có thể gán nhầm ID nếu nghiệp vụ không kiểm soát. Sửa Price của một ID ảnh hưởng mọi Room dùng đúng ID; ID khác cùng tên không bị ảnh hưởng. RoomTypeServices nối theo ID. Không thêm Rooms.MonthlyPrice, PropertyRoomPrice, RoomTypes.PropertyId; không tạo data trong audit.

## Decision log — cần chốt trước Phase 2

### D1. Property nào được công khai?

- Quan sát: schema không có Published/visibility; GET /api/properties hiện trả toàn bộ.
- Lựa chọn: A) xác nhận mọi Property trong bảng đều public; B) dùng allow-list/nguồn ngoài có kiểm soát; C) thiết kế trạng thái publish riêng ở phase được duyệt.
- Khuyến nghị: không bật anonymous listing trước khi có rule rõ.
- Yêu cầu product owner: YES — blocker.

### D2. Booking nào giữ phòng?

- Quan sát: status live đã thấy Pending, Deposited, Cancelled; ExpiryDate là timestamptz; không có lifecycle code.
- Ứng viên: Pending và Deposited chưa hết hạn giữ phòng; Cancelled không giữ. Đây là đề xuất bảo thủ, không phải rule đã xác nhận.
- Cần chốt expiry ở UTC, status transition và việc deposit thất bại/hết hạn.
- Yêu cầu product owner: YES — blocker.

### D3. Contract nào chiếm phòng?

- Quan sát: Status live có Active; Contract có StartDate/EndDate date. Maintenance kiểm tra UTC date trong khoảng inclusive nhưng không xác thực Contract.Status.
- Ứng viên: Active và ngày hiệu lực hiện tại block; Contract tương lai xử lý riêng.
- Cần chốt Active semantics, kết thúc sớm, timezone/business date và khoảng ngày inclusive không.
- Yêu cầu product owner: YES — blocker.

### D4. Status Reserved

- Quan sát: có row Reserved trong DB nhưng RoomService không nhận khi update.
- Cần chốt Reserved là status hợp lệ hay dữ liệu cũ; cách chuyển status và sync với Booking.
- Yêu cầu owner của Rooms module: YES.

### D5. Giá/option logical type

- Dữ liệu: duplicate TypeName được phép; giá và service theo RoomTypeId.
- Khuyến nghị: dropdown một option mỗi normalized TypeName; query roomTypeName match các row tương ứng; roomTypeId exact một row.
- Có thể tiến hành sau khi Cách 1 giữ nguyên; không cần schema migration.

### D6. Map, capacity và fee

- Tọa độ cần precision/geospatial cho radius; FE geolocation cần fallback.
- occupantCount cần capacity field.
- Service total cần billing unit/mandatory policy.
- Hãy bỏ các filter/ước tính này khỏi MVP hoặc xin quyết định data/schema riêng.