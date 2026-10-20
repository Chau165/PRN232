# Phase 3.1A — Live Supabase Schema and Data Audi

**Snapshot nghiệp vụ:** 2026-10-19 05:18:04 UTC. Catalog bảo trì được đối chiếu thêm trong một phiên read-only kế tiếp cùng ngày.
**Trạng thái:** Live verified; chỉ gồm metadata và aggregate, không có PII.

## Kết nối và tính chỉ đọc

- BE dùng Npgsql qua Program.cs và GetConnectionString(DefaultConnection).
- ConnectionStrings__DefaultConnection có trong process environment và User Secrets; audit dùng giá trị process environment hiện tại. Không in host đầy đủ, username, password hay URI.
- Project ref qvdrjyecaprfpgvbfyfe khớp; cấu hình là Supabase pooler, cổng 5432, database postgres, schema public, PostgreSQL 17.11.
- Transaction dùng REPEATABLE READ READ ONLY, statement_timeout 10 giây; xác nhận transaction_read_only=on rồi ROLLBACK.
- Vai trò DB không phải superuser nhưng có BYPASSRLS. 14 bảng liên quan được kiểm tra đều bật RLS và không có policy trong pg_policies. Điều này không đại diện quyền của anon/authenticated hoặc API client.

## Public schema

Có 24 base tables: Amenities, Bookings, Contracts, Equipments, IdentityDocuments, InvoiceDetails, Invoices, MaintenanceLogs, MaintenanceReportImages, MaintenanceReports, MaintenanceRequests, Notifications, Payments, Properties, Roles, RoomAmenities, RoomImages, RoomTypeServices, RoomTypes, Rooms, Services, Users, UtilityReadings, __EFMigrationsHistory.

Không có PropertyAmenities, PropertyServices, PropertyServiceRates, ServiceRates hoặc UtilityRates; tên bảng công khai khác cũng không thể hiện bảng rate/property-amenity tương đương.

## Schema liên quan

| Table | Cột và kiểu đáng chú ý | Khóa, quan hệ, index đáng chú ý |
|---|---|---|
| Properties | PropertyId integer; OwnerId integer; PropertyName, AddressText, Ward, District, City text; Latitude/Longitude numeric(18,2); PrimaryImageUrl text nullable. Không có Description. | PK PropertyId; OwnerId → Users, ON DELETE CASCADE; index OwnerId. Không thấy check/unique liên quan Property Detail. |
| Rooms | RoomId, PropertyId, RoomTypeId integer; RoomCode, Status text; AreaSqm numeric(18,2). | PK; PropertyId → Properties và RoomTypeId → RoomTypes, đều CASCADE; index cả hai FK. |
| RoomTypes | RoomTypeId integer; TypeName text; Price numeric(18,2) NOT NULL. | PK RoomTypeId; không có PropertyId, unit hoặc unique TypeName. |
| Amenities | AmenityId integer; Name text. | PK AmenityId. |
| RoomAmenities | RoomId, AmenityId integer. | PK ghép; hai FK → Rooms/Amenities, CASCADE; index AmenityId. |
| Services | ServiceId integer; Name text; Price numeric(18,2) NOT NULL. | PK ServiceId; không thấy CHECK giá; không có UnitCode/BillingBasis/effective date. |
| RoomTypeServices | RoomTypeId, ServiceId integer. | PK ghép; hai FK → RoomTypes/Services, CASCADE; index ServiceId. |
| Invoices | InvoiceId, ContractId integer; InvoiceMonth, DueDate date; Status text. | PK; ContractId → Contracts, CASCADE; index ContractId. |
| InvoiceDetails | InvoiceDetailId, InvoiceId integer; ItemType, Description text; Amount numeric(18,2). | PK; InvoiceId → Invoices, CASCADE; index InvoiceId. Không có ServiceId, Quantity, UnitCode hoặc UnitPrice. |
| UtilityReadings | ReadingId, ContractId integer; ReadingMonth date; ElectricStart/End, WaterStart/End numeric(18,2). | PK; ContractId → Contracts, CASCADE; index ContractId. Không có rate hoặc đơn vị tính tiền. |
| Payments | PaymentId, UserId; BookingId, ContractId, InvoiceId, ReadingId nullable; Amount numeric(18,2); PaymentMethod, Status text. | PK; 4 FK optional đều ON DELETE RESTRICT; không có CHECK “chính xác một target”. Dữ liệu hiện tại có 6/6 dòng tham chiếu đúng một target: 3 Booking, 3 Invoice. |
| Contracts / Bookings | Contracts có RoomId, BookingId nullable, StartDate/EndDate, MonthlyRent, DepositAmount, Status; Bookings có RoomId, Status, ExpiryDate. | Các cột dùng để tái tạo predicate rentable đang có trong source. |

Các cột ID chính liên quan được PostgreSQL đánh dấu identity. Bảng giá dùng numeric(18,2); ràng buộc giá âm hiện được kiểm tra trong Service/RoomType service layer, không có CHECK live.

## Data snapshot theo Property

Property ID và tên được truy vấn trực tiếp theo tên.

| Property | Rooms | Rentable theo predicate source | Amenity links: tất cả / rentable rooms | Service types qua tất cả / rentable RoomTypes | Ảnh chính | Description |
|---|---:|---:|---:|---:|---|---|
| 1 — Nhà trọ Bình An | 4 | 1 | 13 / 4 | 4 / 4 | Có URL | Không có cột |
| 2 — Khu trọ Sunrise | 3 | 2 | 9 / 6 | 4 / 4 | Có URL | Không có cột |
| 3 — Nhà trọ Minh Tâm | 2 | 1 | 5 / 3 | 4 / 4 | Có URL | Không có cột |

Tất cả 4 service catalog entries được liên kết tới cả 3 RoomType IDs; mỗi entry hiện qua 3 Property và 9 Room. Không có RoomType ID nào dùng bởi nhiều Property trong snapshot; schema vẫn cho phép. Không có nhóm TypeName trùng trong dữ liệu hiện tại.

## Counts aggregate

| Table | Rows | Table | Rows |
|---|---:|---|---:|
| Properties | 3 | Rooms | 9 |
| RoomTypes | 3 | Amenities | 6 |
| RoomAmenities | 27 | Services | 4 |
| RoomTypeServices | 12 | Invoices | 5 |
| InvoiceDetails | 25 | UtilityReadings | 5 |
| Payments | 6 | MaintenanceLogs | 9 |
| MaintenanceReports | 4 | MaintenanceReportImages | 5 |

Invoice statuses: Overdue 1, Paid 2, Unpaid 2. InvoiceDetails có 5 dòng mỗi ItemType: Electricity, Internet, Rent, Trash, Water. UtilityReadings có 5 dòng thuộc 3 Contract và 2 tháng; không duplicate Contract+ReadingMonth hoặc orphan Contract. Counts là snapshot.

## EF migration history và live drif

Live __EFMigrationsHistory có 4 migrations, tất cả ProductVersion 8.0.10:

1. 20261003142421_InitialCreate
2. 20261004073837_RenameKhuTroDichVuToPropertyService
3. 20261013140609_SeedRolesAndAuthSetup
4. 20261019034905_AddPropertyPrimaryImageUrl

Source có thêm 20261013192052_ReconcileMaintenanceReportsAndLogNullability nhưng migration ID đó chưa có trong history live. Source migration dùng CreateTable cho MaintenanceReports và MaintenanceReportImages, trong khi live hai bảng đã tồn tại. Migration không idempotent ở bước CreateTable; chạy database update có thể lỗi “relation already exists”. Down của migration có DropTable hai bảng này.

Đối chiếu migration pending với live:

- MaintenanceLogs.OldStatus live nullable, phù hợp thay đổi nullability trong Up.
- FKs của MaintenanceReports và MaintenanceReportImages hiện hữu, gồm unique index trên MaintenanceReports.RequestId.
- Live thiếu IX_MaintenanceReports_ReportedBy và IX_MaintenanceReportImages_ReportId mà migration source định tạo.
- Live MaintenanceReports.CreatedAt có default CURRENT_TIMESTAMP, migration source không khai báo default.
- Hai bảng MaintenanceReports và MaintenanceReportImages bật RLS, không có policy nhìn thấy; phù hợp phần RLS trong Up.
- Live đang có 4 MaintenanceReports và 5 MaintenanceReportImages; không chạy Down vì sẽ xóa các bảng có dữ liệu.

Đây là schema/history drift cần xử lý trước migration Phase 3.1B. Chưa xác định chính xác thao tác nào tạo các bảng trước khi ghi migration history.

## Những gì chưa được xác minh

Không kiểm tra quyền Supabase Storage, quyền anon/authenticated, HTTP response body của public GET, dữ liệu từng invoice, tenant/customer details, nội dung Description hay tính đúng nghiệp vụ của giá. Không có mutation nào chạy.
