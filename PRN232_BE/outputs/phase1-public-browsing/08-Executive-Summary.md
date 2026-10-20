# 08 — Tóm tắt điều hành

## Kết quả

**COMPLETE — BLOCKED BY BUSINESS/SCHEMA DECISIONS.**

Đã audit source BE liên quan qua Controller → Application service → repository → entity/AppDbContext, inventory routes/controllers, đối chiếu schema live read-only, và truy vết Home/Property/Room UI FE. CodeGraph không dùng được vì service báo BE chưa được index. FE source có sẵn; các route Home hiện không gọi backend.

## Kết luận chính

- **API dùng lại được:** GET /api/room-types để lấy row options rồi FE dedup theo TypeName; GET /api/amenities; GET /api/services; GET /api/room-types/{id}/services cho đúng RoomTypeId. GET /api/properties và GET /api/rooms có thể reuse query logic, nhưng DTO hiện tại không nên trở thành public contract.
- **API còn thiếu:** Property listing theo một Property/card; property detail và phòng thuê được phân trang; public room detail có eligibility gate; filters, pagination/sort và aggregation count/min-price. Không có Booking/Contract lifecycle API.
- **Quan hệ thật:** Properties 1-N Rooms; RoomTypes 1-N Rooms; Rooms 1-N RoomImages/Equipments/Bookings/Contracts; Rooms N-N Amenities qua RoomAmenities; RoomTypes N-N Services qua RoomTypeServices; Booking có thể liên kết tối đa một Contract qua nullable unique Contracts.BookingId.
- **Availability đề xuất:** Status Available AND không có Contract được xác nhận là hiệu lực trong ngày AND không có Booking đang giữ theo status/ExpiryDate đã được PO chốt AND Property được phép public. Status hiện tại một mình không đủ. Safe rule chưa thể chốt vì không có booking workflow/visibility.
- **AvailableRoomCount:** COUNT(DISTINCT rentable RoomId) theo Property.
- **StartingPrice:** MIN(RoomTypes.Price) trên cùng tập rentable Rooms. Không tính từ phòng occupied; nếu count 0 thì price null và mặc định Property bị loại khỏi browse listing.
- **Cách 1:** Hiện khả thi. Live DB RoomTypes chỉ unique theo RoomTypeId; không unique TypeName, không trigger; CRUD không chặn trùng. TypeName có thể trùng với giá khác. RoomTypes không có PropertyId nên gán theo ID là quy ước, không enforce theo Property.
- **FE gaps:** Home/Property/Rooms đều mock; không có API calls. Search/filter/sort/radius là UI tĩnh; kết quả 42 và phòng trống 5 không khớp 3 mock rooms; distance/coords tĩnh; detail link cố định. Occupants, floor, available date, description, property image/rules/fee unit thiếu schema.
- **Schema gaps:** không có publication flag, thiếu booking move-in dates, thiếu service billing units/capacity; tọa độ numeric(18,2) không đủ radius; RoomTypes global; live Reserved không được Room CRUD chấp nhận.
- **Blockers:** (1) Property public visibility, (2) Booking status/expiry có giữ phòng không, (3) Contract Active/date/early termination semantics, (4) xử lý Reserved/status sync.

## Khuyến nghị API Phase 2

Strategy B: GET /api/public/properties; GET /api/public/properties/{id}; GET /api/public/properties/{id}/rooms; GET /api/public/rooms/{id}. Dùng DTO public allow-list và N-layer service/repository riêng; giữ nguyên CRUD hiện tại. Chốt blockers trước khi expose ẩn danh.

## Artifacts

- 01-Current-API-Audit.md
- 02-FE-Field-Mapping.md
- 03-Database-Relationship-Audit.md
- 04-Room-Availability-Business-Rules.md
- 05-Public-Browsing-API-Contracts.md
- 06-Schema-Gaps-And-Open-Decisions.md
- 07-Phase2-Implementation-Plan.md
- 08-Executive-Summary.md

## An toàn và xác minh

Git ban đầu clean. Live DB query dùng transaction READ ONLY và ROLLBACK. Không sửa backend/frontend source, migration, live DB hay credentials; không commit/push. Không chạy build/test vì đây là audit/design-only; test plan nằm trong artifact 07. Sau khi tạo docs, cần xác nhận git status/diff theo prompt.