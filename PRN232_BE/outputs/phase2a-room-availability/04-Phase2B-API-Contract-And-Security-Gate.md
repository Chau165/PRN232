# Phase 2B — API contract và security gate

## Chính sách public đã duyệt

Không có Property publish field. MVP listing tự hiển thị Property có ít nhất một Room qua `RoomAvailabilityQueries`; filter `AvailableRoomCount > 0` ở listing. Không tạo `IsPublished`, không công khai dữ liệu dựa trên frontend mock.

Count/StartingPrice phải cùng một `asOfUtc` và cùng rentable set. Một Property chỉ có một summary row; Room type giá lấy theo ID cụ thể; không nối amenities/images trước khi aggregate. Detail khi Property đã có trong listing nhưng availability đổi về 0 giữa hai request cần được chốt response trong Phase 2B (404 hay 200 rỗng); Phase 2A không tự quyết.

## Public endpoint proposal

Các route dưới đây chưa được tạo trong Phase 2A. Dùng prefix mới, explicit anonymous policy và DTO allow-list riêng:

| Route đề xuất | Contract |
|---|---|
| `GET /api/public/properties` | Một item/Property, chỉ khi count rentable > 0; bounded page/pageSize (gợi ý 1–50), sort ổn định theo PropertyId sau price sort. Fields: PropertyId, tên/địa chỉ/khu vực/tọa độ có trong Property schema, AvailableRoomCount, StartingPrice. |
| `GET /api/public/properties/{id}` | Property fields allow-list và availability summary từ cùng predicate; xử lý 0 rentable rooms theo quyết định ở trên. Không lấy Owner fields từ `PropertyDto`. |
| `GET /api/public/properties/{id}/rooms` | Chỉ Room rentable của Property, pagination ổn định theo RoomId; RoomTypeId/name/price theo Room thực tế, không expose raw Booking/Contract/Status. |
| `GET /api/public/rooms/{id}` | Chỉ Room hiện rentable; không tồn tại/không rentable trả cùng một response để tránh dò trạng thái. Room images/amenities chỉ từ đúng Room sau allow-list. |

Query `asOfUtc` phải do server tạo một lần bằng TimeProvider trong request; không bind clock từ client. Public DTO không có OwnerId/OwnerName, CustomerId, BookingId/Status/Deposit, ContractId/CustomerId/Status/date/rent, payment, identity-document, maintenance, hay dữ liệu quản trị. `RentableRoomDto` trong Phase 2A là query DTO an toàn, chưa phải toàn bộ public response contract.

## Legacy anonymous exposure cần xử lý riêng

Đã đối chiếu source hiện tại:

| Endpoint hiện tại | Anonymous behavior/response | Rủi ro |
|---|---|---|
| `GET /api/properties`, `GET /api/properties/{id}` | Không có `[Authorize]`; trả `PropertyDto` gồm OwnerId, OwnerName và RoomCount tổng, không phải rentable count. List còn nhận `ownerId` query filter. | Lộ identity/owner relation và số phòng tổng; lọc owner làm tăng khả năng enumeration. |
| `GET /api/rooms`, `GET /api/rooms/{id}` | Không có `[Authorize]`; trả `RoomDto` có raw Status, Property/RoomType/price/code/area/image. List không xét Booking/Contract. | Có thể trả Reserved/Occupied/Maintenance hoặc Room bị Booking/Contract block; raw operational status bị lộ. |
| `GET /api/rooms/{id}/images`, `GET /api/rooms/{id}/amenities` | Anonymous; đọc theo RoomId mà không yêu cầu Room qua rentability/publication predicate. | Có thể dò asset/amenity của Room không public hoặc không rentable. |

**Mitigation:** route mới không gọi legacy DTO/repository projection. Trước khi đổi hoặc khóa route legacy, inventory consumer FE/admin và test của họ; sau đó chọn hardening authorization hoặc DTO/route compatibility change trong task security riêng. Không thay đổi route legacy trong Phase 2A.

## Gate và readiness

- **Có thể bắt đầu Phase 2B implementation** cho route mới, dùng auto-list policy đã duyệt, availability predicate dùng lại, DTO allow-list và query paging server-side.
- **Chưa production/public-rollout ready** cho toàn bộ surface: legacy anonymous routes vẫn lộ OwnerId/OwnerName/raw Room.Status; cần owner/security disposition và compatibility tests trước khi quyết định harden chúng.
- Chốt zero-availability detail response trước khi final hóa contract.
- Reservation end-to-end vẫn phụ thuộc Booking write contract, khóa per-room, payment verification và Contract handoff trong `03-Booking-Write-Contract-And-Concurrency.md`.
