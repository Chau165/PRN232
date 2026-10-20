# 03 — Kiểm toán bảng và quan hệ dữ liệu

## Nguồn và độ tin cậy

Live PostgreSQL public catalog được truy vấn read-only ngày 2026-10-18 qua information_schema.columns, table_constraints, key_column_usage, constraint_column_usage, pg_indexes, pg_constraint và pg_trigger; transaction được ROLLBACK. Không lấy user/tenant values hoặc ghi DB.

Các file database-overview.md, database-schema.md, database-relationships.md, database-dependency-order.md, schema-full.sql, schema-application.sql và sample-data-plan.md không có trong checkout. docs/database hiện chỉ có tài liệu migration của Maintenance. migration.sql và migration InitialCreate phản ánh schema lịch sử có KhuTros/KhuTroId/DichVus; migration2.sql có bước đổi/đưa schema sang Properties, Services, RoomTypeServices. Dùng live catalog + entity/AppDbContext hiện hành làm nguồn quyết định, không coi SQL lịch sử là schema live.

## Bảng chính trong public schema

Tất cả cột nêu dưới đây đều NOT NULL, trừ nơi ghi rõ nullable. PK/FK được xác nhận từ catalog live và đối chiếu EF/entity.

| Bảng | Cột/thực tế | Quan hệ và vai trò browsing |
|---|---|---|
| Properties | PropertyId PK int; OwnerId int; PropertyName, AddressText, Ward, District, City text; Latitude, Longitude numeric(18,2) | OwnerId → Users.UserId; 1 Property → N Rooms. Không có Status/Published, Description, image, amenity, rules |
| Rooms | RoomId PK int; PropertyId, RoomTypeId int; RoomCode text; AreaSqm numeric(18,2); Status text | PropertyId → Properties; RoomTypeId → RoomTypes; 1 Room → N RoomImages, RoomAmenities, Equipments, Bookings, Contracts |
| RoomTypes | RoomTypeId PK int; TypeName text; Price numeric(18,2) | 1 RoomType → N Rooms; N-N Services qua RoomTypeServices. Không có PropertyId |
| RoomImages | RoomImageId PK; RoomId int; ImageUrl text; IsPrimary bool | RoomId → Rooms. Không phải ảnh Property |
| Amenities | AmenityId PK; Name text | N-N Rooms qua RoomAmenities |
| RoomAmenities | RoomId, AmenityId int, PK ghép | RoomId → Rooms; AmenityId → Amenities; mỗi cặp Room-Amenity duy nhất |
| Services | ServiceId PK; Name text; Price numeric(18,2) | N-N RoomTypes qua RoomTypeServices; thiếu đơn vị tính/bắt buộc |
| RoomTypeServices | RoomTypeId, ServiceId int, PK ghép | RoomTypeId → RoomTypes; ServiceId → Services |
| Equipments | EquipmentId PK; RoomId int; Name, Supplier, Status text; InstallDate, WarrantyExpiry date | RoomId → Rooms; các cột supplier/warranty/status là metadata quản trị |
| Bookings | BookingId PK; RoomId, CustomerId int; DepositAmount numeric(18,2); Status text; ExpiryDate timestamptz | RoomId → Rooms; CustomerId → Users. Không có ngày bắt đầu/kết thúc thuê |
| Contracts | ContractId PK; RoomId, CustomerId int; BookingId int nullable; ProcessedByStaffId int nullable; StartDate, EndDate date; MonthlyRent, DepositAmount numeric(18,2); Status text | RoomId → Rooms; CustomerId/ProcessedByStaffId → Users; BookingId → Bookings |

Các entity tương ứng: PRN232.Domain/Entities/Property.cs:3-17, Rooms.cs:3-19, RoomTypes.cs:3-12, RoomImages.cs:3-12, Amenities.cs:3-11, RoomAmenities.cs:3-11, Service.cs:3-11, RoomTypeServices.cs:3-10, Equipments.cs:3-16, Bookings.cs:3-16, Contracts.cs:3-23. AppDbContext khai PK và PK ghép ở AppDbContext.cs:34-67; quan hệ Booking-Contract ở :126-132; precision decimal mặc định :151-156.

## ER diagram — FK xác nhận từ live DB

~~~mermaid
erDiagram
    Users ||--o{ Properties : owns
    Properties ||--o{ Rooms : contains
    RoomTypes ||--o{ Rooms : prices
    Rooms ||--o{ RoomImages : has
    Rooms ||--o{ RoomAmenities : links
    Amenities ||--o{ RoomAmenities : links
    RoomTypes ||--o{ RoomTypeServices : links
    Services ||--o{ RoomTypeServices : links
    Rooms ||--o{ Equipments : has
    Rooms ||--o{ Bookings : reserved_by
    Rooms ||--o{ Contracts : leased_by
    Bookings ||--o| Contracts : may_create
~~~

Bookings–Contracts là 1:1 tùy chọn theo Contracts.BookingId nullable và unique index IX_Contracts_BookingId; Contract không bắt buộc bắt nguồn từ Booking. FK RoomId tồn tại độc lập ở cả Bookings và Contracts; DB không ràng buộc hai RoomId phải trùng nếu Contract có BookingId. RoomAmenities và RoomTypeServices dùng khóa ghép, đúng quan hệ N-N.

## Feasibility Cách 1 — giữ RoomTypes hiện tại

**CONFIRMED: khả thi theo schema và CRUD đang quan sát.**

- Live RoomTypes chỉ có PK_RoomTypes(RoomTypeId); không có unique constraint/index trên TypeName, không có custom check constraint hoặc trigger trên bảng.
- RoomTypeService.CreateAsync/UpdateAsync chỉ Trim TypeName; Validate chỉ chặn tên rỗng và Price âm; không kiểm tra trùng (RoomTypeService.cs:24-43, 102-113).
- RoomTypes không có PropertyId hay FK tới Property. Đây là bảng dùng chung; Cách 1 là quy ước data management, không bảo đảm TypeId chỉ dùng tại một Property.
- RoomService xác nhận PropertyId và RoomTypeId tồn tại độc lập; không kiểm tra RoomType đó thuộc Property (RoomService.cs:21-38). Có thể gán RoomTypeId “của khu khác”.
- Room luôn lấy giá hiện tại theo Rooms.RoomTypeId → RoomTypes.Price; hai row cùng TypeName nhưng ID khác có giá độc lập (RoomRepository.cs:26-33).
- Sửa Price của một RoomTypeId tác động mọi Room tham chiếu đúng ID đó. Contracts.MonthlyRent là cột riêng; không thấy code đồng bộ giá RoomType sang Contract. Không kết luận được policy lịch sử ngoài phần code này.
- RoomTypeServices nối với RoomTypeId cụ thể; dịch vụ không được suy ra bằng TypeName.

Live index cho RoomTypes chỉ PK; các index FK hiện có gồm IX_Rooms_PropertyId, IX_Rooms_RoomTypeId, IX_RoomImages_RoomId, IX_RoomAmenities_AmenityId (PK ghép bắt đầu bằng RoomId), IX_RoomTypeServices_ServiceId (PK ghép bắt đầu bằng RoomTypeId), IX_Bookings_RoomId, IX_Contracts_RoomId, IX_Equipments_RoomId, IX_Properties_OwnerId. Không thấy index theo Status, Booking expiry/status, Contract status/date, RoomTypes.TypeName hoặc tọa độ.

## Giới hạn dữ liệu riêng tư

Không đưa OwnerId, OwnerName, CustomerId, ProcessedByStaffId, BookingId, DepositAmount, Contract.Status/MonthlyRent, booking personal data hoặc maintenance relations vào public DTO. Equipment public projection nếu cần chỉ dùng field đã được duyệt (đề xuất tối thiểu Name); không trả Supplier, InstallDate, WarrantyExpiry, Status.

Equipment.Status không tham gia vào query listing hiện tại; EquipmentService chỉ yêu cầu Status không rỗng khi CRUD. Không có bằng chứng status thiết bị làm phòng không thể thuê. Tham chiếu: PRN232.Application/Services/EquipmentService.cs.