# Phase 2E — Kiểm toán anonymous GET và consumer

## Authorization hiệu lực

`Program.cs` gọi `AddAuthorization()` nhưng không đặt `FallbackPolicy`; do đó chỉ controller/action có `[Authorize]` mới yêu cầu đăng nhập. Bốn controller Public có `[AllowAnonymous]` tường minh. Các route liệt kê dưới đây thực sự anonymous theo cấu hình/source hiện tại.

Tìm trên toàn bộ `PRN232.Api/Controllers` có **21 anonymous GET**:

| Route | Controller/action; response | Rủi ro và consumer evidence | Trạng thái |
|---|---|---|---|
| `GET /api/public/properties` | `PublicPropertiesController.GetAll`; public allow-list, page/filter. | Public contract mới; không có Owner/PII. FE chưa gọi API. | **KEEP PUBLIC-APPROVED** |
| `GET /api/public/properties/{id}` | `PublicPropertiesController.GetById`; Property summary + availability. | Cùng contract public mới. | **KEEP PUBLIC-APPROVED** |
| `GET /api/public/properties/{id}/rooms` | `PublicPropertiesController.GetRooms`; rentable Room page. | Cùng contract public mới; `RoomCode` vẫn chờ owner xác nhận. | **KEEP PUBLIC-APPROVED** |
| `GET /api/public/rooms/{roomId}` | `PublicRoomsController.GetById`; Room detail allow-list. | Cùng contract public mới; images/equipments rỗng. | **KEEP PUBLIC-APPROVED** |
| `GET /api/properties`, `GET /api/properties/{id}` | `PropertiesController.GetAll/GetById`; `PropertyDto` gồm `OwnerId`, `OwnerName`, tọa độ và tổng `RoomCount`; list nhận `ownerId` từ query. | Lộ liên kết danh tính/chủ sở hữu, tọa độ và inventory; `ownerId` hỗ trợ enumeration. Không filter rentability. Không thể chỉ thêm role attribute: Manager có thể truyền ownerId khác; GetById không có owner check. FE hiện không gọi; external/deployed UNKNOWN. | **BLOCKED** — không đổi nếu chưa chứng minh tương thích/owner approval và thiết kế server-side owner scope. |
| `GET /api/rooms`, `GET /api/rooms/{id}` | `RoomsController.GetAll/GetById`; `RoomDto` gồm Property/RoomType, giá, `RoomCode`, diện tích, raw `Status`, primary image URL. | Không áp dụng Booking/Contract rentability; có thể trả phòng occupied/blocked/status lạ và URL ảnh chưa duyệt. Room ID enumerable. FE hiện không gọi; external/deployed UNKNOWN. | **BLOCKED** — cần tách public allow-list khỏi management read và kiểm tra owner scope. |
| `GET /api/rooms/{id}/images` | `RoomsController.GetImages`; `RoomImageDto` trả `RoomImageId`, `RoomId`, `ImageUrl`, `IsPrimary`. | Đọc ảnh theo RoomId, không kiểm tra publication/rentability/host/ACL; URL có thể là opaque/private. Không có consumer FE trong checkout; external UNKNOWN. | **BLOCKED** — ảnh chỉ được mở sau host allow-list, ACL và compatibility gate. |
| `GET /api/rooms/{id}/amenities` | `RoomsController.GetAmenities`; amenity ID/name theo RoomId. | Có thể dò amenities của phòng không public/không rentable. Consumer ngoài repo UNKNOWN. | **BLOCKED** — cần public eligibility hoặc route management được bảo vệ. |
| `GET /api/room-types`, `GET /api/room-types/{id}`, `GET /api/room-types/{id}/services` | `RoomTypesController`; `RoomTypeDto` (ID/name/price) và service catalog theo RoomType. | Danh mục/giá ít nhạy cảm hơn Room/Property nhưng chưa có owner decision cho public catalog; service không có billing unit. FE không gọi API. | **PENDING APPROVAL** — không suy diễn `/tháng`. |
| `GET /api/services`, `GET /api/services/{id}` | `ServicesController`; Service ID/name/price. | Lộ toàn bộ giá dịch vụ và catalog; kỳ tính phí chưa xác định. Consumer ngoài repo UNKNOWN. | **PENDING APPROVAL** |
| `GET /api/amenities`, `GET /api/amenities/{id}` | `AmenitiesController`; Amenity ID/name. | Nội dung reference data đơn giản; chưa tìm thấy quyết định cho direct anonymous catalog. FE chưa gọi API. | **PENDING APPROVAL** |
| `GET /api/roles`, `GET /api/roles/{id}` | `RolesController`; Role ID/name. | Cấu trúc role phục vụ authorization; chưa có public reference approval. Consumer ngoài repo UNKNOWN. | **PENDING APPROVAL** |
| `GET /api/health-checks` | `HealthChecksController.Get`; status/time. | Cho biết service đang phản hồi; consumer monitor/deployment chưa kiểm kê. | **PENDING APPROVAL** |
| `GET /api/health-checks/database` | `HealthChecksController.GetDb`; `connected`/`unreachable`. | Tiết lộ trạng thái kết nối DB nhưng không trả exception/host; consumer monitor/deployment chưa kiểm kê. | **PENDING APPROVAL** |

Các nhóm trên gồm 4 public + 6 Property/Room + 11 reference/diagnostic = **21 routes**.

## Protected GET đã đối chiếu

`UsersController` có `[Authorize]` ở class (GET list thêm Admin; `/me` cần đăng nhập); `EquipmentsController` giới hạn Admin/Manager; `IdentityDocumentsController`, `MaintenanceRequestsController` và `NotificationsController` có authorization ở class/action. Các GET quản trị này không anonymous theo source hiện tại. Management write `POST /api/rooms` được test không token và trả `401`.

## Consumer matrix

- Search `PRN232_FE/src` cho `fetch`, `axios`, client API và `/api/` không thấy callsite. Home dùng `src/data/properties.ts`; property detail dùng mock `properties[0]`/`propertyDetails.ts`; room list dùng `src/data/rooms.ts`; management properties/rooms dùng `getManagementScope`. README FE nói còn cần HTTP client để nối backend.
- Không tìm thấy test FE/backend gọi legacy `/api/properties` hoặc `/api/rooms`. Swagger/source có route nhưng không chứng minh production caller.
- **External, deployed, monitoring và consumer ngoài checkout: UNKNOWN.** Không kết luận route không ai dùng.

## Security và compatibility

- Thêm `[Authorize]` hàng loạt không an toàn: Property/Room GET không lấy owner từ authenticated principal, `ownerId` do caller truyền, và GetById/images/amenities không kiểm tra ownership. Policy đúng cần Manager chỉ đọc Property/Room của mình; Admin giữ quyền hiện tại.
- Public caller tương lai phải dùng bốn `/api/public/...` allow-list, không dùng management `PropertyDto`/`RoomDto`. Cần inventory external/deployed consumer, owner xác nhận contract, rồi migration/deprecation + authorization/compatibility tests.
- EF projection/parameterization được dùng trong public query; không thấy SQL string concatenation ở path này. Direct ID enumeration vẫn có ở legacy GET. Public GET không có rate-limit policy; CORS không phải authorization.
- `POST /api/roles` là phát hiện ngoài danh sách GET: controller/action không có `[Authorize]`, trong khi action tạo Role. Đây là **BLOCKER riêng**, chưa thay đổi do scope/consumer/policy cần owner xác nhận.
