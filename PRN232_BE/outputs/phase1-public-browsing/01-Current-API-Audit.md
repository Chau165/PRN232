# 01 — Kiểm toán API hiện tại

## Phạm vi và cách đọc bằng chứng

Audit inventory 13 API controllers và đọc source theo luồng liên quan trong PRN232.Api, PRN232.Application, PRN232.Domain, PRN232.Infrastructure; đồng thời truy vết component Home/Property/Room của PRN232_FE. Bản chuẩn coding là CODING_STANDARDS.md. Live PostgreSQL được đọc trong transaction READ ONLY rồi ROLLBACK; chỉ đọc catalog và các giá trị Status, không lấy PII, không ghi dữ liệu.

CodeGraph được thử với projectPath C:\PRN232\PRN232_BE nhưng tool báo thư mục này chưa được index và yêu cầu không gọi lại. Không tạo/rebuild index; đã dùng tìm kiếm và đọc source theo luồng controller → service → repository → entity/mapping.

Nhãn: CONFIRMED = có source/catalog hỗ trợ; INFERRED = suy ra từ cấu trúc; PROPOSED = thiết kế Phase 2; UNKNOWN = chưa có bằng chứng.

Coding standard yêu cầu Controller gọi service, service gọi repository, truy vấn EF ở repository với projection DTO; DTO không trả entity; async method có hậu tố Async. Tham chiếu: CODING_STANDARDS.md:23-41, 58-85, 90-97.

## Endpoint liên quan đến browsing

| Route | Public? | Hành vi/DTO hiện tại | Hạn chế chính |
|---|---|---|---|
| GET /api/properties?ownerId= | Có, không có [Authorize] | List PropertyDto; lọc ownerId tùy chọn | Trả OwnerId, OwnerName và RoomCount tổng; không paging, search, rentability hay giá |
| GET /api/properties/{id} | Có | Một PropertyDto | Không kiểm tra trạng thái công khai; không ảnh, giá bắt đầu, phòng thuê được |
| GET /api/rooms?propertyId=&status=&city=&maxPrice= | Có | List RoomDto; lọc đúng các query trên | Không paging; status chỉ so sánh chuỗi; không minPrice, room type, amenities, Booking/Contract |
| GET /api/rooms/{id} | Có | Một RoomDto | Có thể trả phòng Occupied/Reserved; không kiểm tra khả năng thuê |
| GET /api/rooms/{id}/images | Có | RoomImageDto[] | Không kiểm tra public visibility hoặc rentability |
| GET /api/rooms/{id}/amenities | Có | AmenityDto[] | Chỉ tiện ích của một phòng, không lọc Property |
| GET /api/room-types | Có | RoomTypeDto[]: RoomTypeId, TypeName, Price | Có thể dùng lấy option; trả một row cho mỗi ID, nên FE phải nhóm tên trùng |
| GET /api/room-types/{id} | Có | RoomTypeDto | Giá thuộc đúng RoomTypeId |
| GET /api/room-types/{id}/services | Có | ServiceDto[] của đúng RoomTypeId | Không có đơn vị tính/mandatory flag |
| GET /api/services | Có | ServiceDto[]: ServiceId, Name, Price | Price không cho biết chu kỳ hoặc cách tính |
| GET /api/amenities | Có | AmenityDto[] | Danh mục, chưa có filter cho listing |
| GET /api/equipments[?roomId=], GET /api/equipments/{id} | Không; controller yêu cầu Admin/Manager | EquipmentDto đầy đủ | Giữ nguyên giới hạn quản trị; không dùng DTO này trực tiếp cho khách |

Nguồn: PropertiesController.cs:10-31; RoomsController.cs:10-34, 100-145; RoomTypesController.cs:10-78; ServicesController.cs:10-24; AmenitiesController.cs:10-24; EquipmentsController.cs:10-27. Controller CUD được gắn [Authorize(Roles="Admin,Manager")]; EquipmentsController có authorize ở cấp class.

Program.cs đăng ký Authentication/Authorization nhưng không cấu hình FallbackPolicy; chỉ actions/controller có [Authorize] bị chặn. Do đó các GET trên không có [Authorize] hiện gọi được ẩn danh. Route token dùng slug transformer, nên các route thực tế ở dạng lower-case/kebab-case. Tham chiếu: Program.cs:39-63, 122-126, 145-157.

## DTO, filter và query hiện hành

- PropertyDto gồm PropertyId, OwnerId, OwnerName, PropertyName, AddressText, Ward, District, City, Latitude, Longitude, RoomCount (PropertyDto.cs:3-14). Repository đếm p.Rooms.Count, tức tổng phòng, không lọc Status/Booking/Contract (PropertyRepository.cs:18-54).
- RoomDto gồm PropertyId/PropertyName, RoomTypeId/RoomTypeName/RoomTypePrice, RoomCode, AreaSqm, Status và PrimaryImageUrl (RoomDto.cs:3-13). RoomRepository so sánh status/city chính xác, maxPrice với r.RoomType.Price, và projection giá bằng RoomTypeId đang tham chiếu (RoomRepository.cs:18-36, 39-54).
- Không có pagination/sort trong IPropertyService, IRoomService hoặc các controller list hiện có.
- Không có API/controller/service/repository cho Booking hoặc Contract lifecycle. Có entity và FK, nhưng không có create/approve/cancel/expire/sync flow trong backend source. Không có BookingController hay ContractsController trong danh sách controllers.
- RoomTypeService kiểm tra tên không rỗng và giá không âm, rồi Trim; không tìm tên trùng (RoomTypeService.cs:24-43, 102-113).
- Existing CRUD endpoint scope thuộc module quản lý; tránh thay đổi nghĩa/response của các GET hiện hữu khi thêm public browsing.

Các controller còn lại được route-inventory: Auth, HealthChecks, IdentityDocuments, MaintenanceRequests, Notifications, Roles, Users. Các route người dùng/maintenance/notification có authorization riêng; chúng không cung cấp Property/Room browsing. Không tìm thấy endpoint Booking/Contract.

PRN232.Application.Tests hiện có test cho Maintenance và storage; không tìm thấy test cho Property/Room/RoomType browsing hoặc availability. Không chạy test trong Phase 1 vì đây là audit/design-only.

## Tương thích, tái sử dụng và rủi ro lộ dữ liệu

Có thể tái sử dụng để lấy dữ liệu danh mục: GET /api/room-types, GET /api/amenities, GET /api/services và GET /api/room-types/{id}/services. FE cần nhóm TypeName và không được gom giá/dịch vụ theo tên thay vì ID.

GET /api/properties và GET /api/rooms có thể tái sử dụng một phần query/repository, nhưng DTO/ý nghĩa hiện tại không phù hợp làm hợp đồng Home: PropertyDto phát OwnerId/OwnerName và RoomCount tổng; RoomDto trả Status/RoomCode/PropertyId cho mọi phòng; các route ảnh/amenity cũng không kiểm tra tính công khai. Sửa DTO hoặc đổi semantics của chúng có thể làm hỏng consumer quản lý chưa được tìm thấy trong FE hiện tại.

Rủi ro đã xác nhận:
1. Không có cờ Published/visibility trong Property; GET /api/properties trả toàn bộ bản ghi.
2. GET Room by ID, ảnh và tiện ích không giới hạn phòng Available; có thể phơi bày thông tin phòng không được quảng cáo.
3. DTO Property công khai có OwnerId và OwnerName.
4. Không có rule bảo vệ phòng khỏi Booking đang giữ hoặc Contract hiệu lực.
5. Equipment DTO có Supplier, InstallDate, WarrantyExpiry, Status; endpoint quản trị hiện đã được bảo vệ, không nên bỏ auth.

Không tìm thấy API key hoặc request /api trong source FE; do đó các API hiện hữu chưa được tích hợp vào Home.

## So sánh chiến lược API

| Tiêu chí | A — mở rộng GET hiện hữu | B — route public riêng |
|---|---|---|
| Tái dùng code | Ít thay đổi ban đầu | Có thể dùng chung logic repository/service nhưng DTO riêng |
| Tương thích | Có thể đổi nghĩa RoomCount/Status hoặc DTO đang được dùng | Giữ nguyên hợp đồng CRUD hiện có |
| FE | Ít route hơn | Public contract rõ ràng, dễ tích hợp |
| Tách quyền và dữ liệu | Khó tách DTO quản trị/khách | Dễ allow-list field, chỉ trả dữ liệu đủ điều kiện |
| Bảo mật | Dễ vô tình tiếp tục lộ owner/room state | Có chỗ áp visibility và availability thống nhất |
| Bảo trì | Ít file, nhưng nhiều nhánh/semantics | Thêm lớp public module, dễ test theo nghiệp vụ |
| Coding standard | Có thể giữ pattern hiện tại | Tuân thủ N-layer với DTO/service/repository riêng |

**Khuyến nghị: B.** Tạo public read-only API và DTO riêng, reuse phép chiếu/query khi phù hợp; không đổi behavior CRUD hiện tại. Chỉ bật public result sau khi chốt visibility và availability. Cách này tránh trả OwnerId/OwnerName, tránh lộ phòng không thuê được và không làm thay đổi RoomCount hiện hữu.