# Phase 2E — Rà soát hợp đồng 4 Public Browsing API

## Kết quả

**Đã đối chiếu source, test API và gọi đủ bốn route qua HTTP test host dùng PostgreSQL READ ONLY.** Public DTO và quy tắc rentability hiện nhất quán. Việc xác minh không đóng các quyết định owner về `RoomCode`, 404 khi Property hết phòng, ảnh và thiết bị.

| Route | Quyền và thành công | Lọc, phân trang, dữ liệu | Lỗi và tương thích |
|---|---|---|---|
| `GET /api/public/properties` | `[AllowAnonymous]`; `200` với `{ items, totalCount, page, pageSize }`. Không có kết quả phù hợp trả `200`, `items: []`. | `keyword`, city/district/ward, min/max price, `roomTypeName`, lặp `amenityIds`, `sortBy`, page/pageSize. Default page 1, size 10; size tối đa 50. Count, giá và amenity filter dùng cùng tập Room thuê được; amenity phải cùng một Room. Sort giá có tie-break `PropertyId`. | Query sai trả `400 ValidationProblemDetails`. Không có thay đổi contract Phase 2B. `primaryImageUrl` luôn `null`. |
| `GET /api/public/properties/{id:int}` | `[AllowAnonymous]`; `200` khi Property có ít nhất một Room thuê được. | Trả `propertyId`, tên/địa chỉ/phường/quận/thành phố, số Room thuê được và giá thấp nhất trong tập đó. Một câu SQL projection cho count/min. | Unknown và zero rentable cùng `404` generic. Zero-room `404` vẫn **PENDING OWNER APPROVAL**; giữ contract hiện tại. |
| `GET /api/public/properties/{id:int}/rooms` | `[AllowAnonymous]`; `200` cho trang kết quả, filter rỗng hoặc page vượt cuối; `404` khi Property unknown/không còn rentable. | `minPrice`, `maxPrice`, `roomTypeName`, lặp `amenityIds`, `sortBy`, page/pageSize. Sort theo giá tie-break `RoomId`; route ID quyết định scope. Giá/type/amenities gắn cùng một Room. DTO chỉ gồm ID, `RoomCode`, RoomType, giá, diện tích, ảnh nullable và amenity summary. | Query sai trả `400`; property ẩn trả cùng `404` generic. `RoomCode` tiếp tục có trong DTO theo contract Phase 2C, **PENDING OWNER APPROVAL**. |
| `GET /api/public/rooms/{roomId:int}` | `[AllowAnonymous]`; `200` cho Room hiện rentable. | Giá đọc từ đúng `Rooms.RoomTypeId`; amenities giới hạn đúng Room; services gắn đúng RoomType ID và giữ từng `price` riêng. `images` và `equipments` luôn `[]`. | Unknown và không rentable cùng `404` generic. Non-integer không khớp route constraint nên framework trả `404`; không có contract `400`. |

## Quy tắc chung đã đối chiếu

- Service lấy một `asOfUtc` từ `TimeProvider` cho mỗi request hợp lệ; client không điều khiển thời điểm này.
- Chỉ `Room.Status == "Available"` mới đủ điều kiện xét tiếp. `Reserved`, `Occupied`, `Maintenance` và status Room lạ đều fail-closed.
- Booking `Pending` giữ phòng khi `ExpiryDate > asOfUtc`; tại đúng hạn không giữ. `Deposited` luôn giữ; `Cancelled` không tự giữ; Booking status lạ fail-closed.
- Contract `Active` chặn bất kể ngày. Contract status khác/lạ cũng fail-closed theo Phase 2A.
- `availableRoomCount` và `startingPrice` dùng cùng rentable set. Trùng `TypeName` không làm gộp `RoomTypeId`, giá hay service.
- DTO anonymous không trả Owner/customer ID, raw status, Booking/Contract/payment/maintenance hoặc thông tin quản trị. Room detail không đọc `RoomImages`/`Equipments`.
- `Service.Price` không có billing unit trong schema; không được đổi nhãn thành phí/tháng hoặc cộng vào tiền thuê.
- Không tìm thấy cache middleware/response cache trên các route. GET chỉ là snapshot; không giữ phòng và không bảo đảm availability tới request sau.

## Giới hạn xác minh

PostgreSQL HTTP test đã xác nhận route/status/shape khi có Room rentable tại thời điểm chạy. Các test API với fixture kiểm tra thêm `400`, generic `404`, query filters, pagination và trạng thái boundary. Lỗi provider được tiêm có chủ đích, p95/load, deployed config và frontend browser UAT **chưa xác minh**. `Program.cs` không có exception handler/ProblemDetails middleware cấu hình tường minh; không kết luận về body lỗi database ở môi trường production.
