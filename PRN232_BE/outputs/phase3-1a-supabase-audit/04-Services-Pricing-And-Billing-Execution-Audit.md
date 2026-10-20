# Phase 3.1A — Services, Pricing và Billing Execution Audi

## Luồng hiện có trong BE

| Đường đi | Source quan sát được | Ý nghĩa |
|---|---|---|
| Danh mục dịch vụ | ServicesController → ServiceCatalogService → ServiceRepository → Services | GET /api/services và GET /api/services/{id} trả ServiceDto có Price. Create/Update kiểm tra Name không rỗng, Price không âm; Update thay giá hiện tại, không có hiệu lực theo ngày hoặc lịch sử rate. |
| Gắn dịch vụ vào loại phòng | RoomTypesController POST/DELETE /api/roomtypes/{id}/services/{serviceId} → RoomTypeService → RoomTypeRepository → RoomTypeServices | PK ghép ngăn duplicate link. Link chỉ nói service nào gắn với RoomType; không lưu giá, đơn vị, required/optional, kỳ tính hoặc PropertyId. |
| Đọc services của RoomType | GET /api/roomtypes/{id}/services → RoomTypeService.GetServicesAsync → RoomTypeRepository.GetServicesAsync | Repository join RoomTypeServices và Services, trả ServiceId/Name/Price. |
| Public room detail | PublicPropertyListingRepository.GetRoomDetailAsync đọc RoomTypeServices và project Service.Price | Giá catalog có thể xuất hiện trên public room detail, nhưng không có đơn vị/billing basis đi kèm DTO. |
| Invoice/Billing BE | Không tìm thấy InvoiceController, InvoiceService, InvoiceRepository hoặc hàm sinh Invoice trong API/Application/Infrastructure | Invoices/InvoiceDetails/UtilityReadings chỉ có Entity, DbSet, EF mapping và schema/data live; chưa có execution path BE để xác nhận giá Invoice lấy từ đâu. |

Tham chiếu source: ServicesController.cs; ServiceCatalogService.cs; ServiceRepository.cs; RoomTypesController.cs; RoomTypeService.cs; RoomTypeRepository.cs; PublicPropertyListingRepository.cs; Invoices.cs; InvoiceDetails.cs; UtilityReadings.cs; AppDbContext.cs.

## Giá và liên kết hiện có trong live

| Service name trong catalog | Services.Price | RoomTypes gắn | Property có liên kết | Rooms qua các RoomTypes |
|---|---:|---:|---:|---:|
| Electricity - per kWh | 3,500.00 | 3 | 3 | 9 |
| Water - per m3 | 20,000.00 | 3 | 3 | 9 |
| Internet - monthly | 100,000.00 | 3 | 3 | 9 |
| Trash - monthly | 30,000.00 | 3 | 3 | 9 |

Live có 4 Services, 12 RoomTypeServices. Giá đều khác nhau giữa bốn service, không có null/zero/âm. Services.Price là một giá toàn cục cho ServiceId; không có giá khác theo Property hoặc RoomType. Hiện mỗi RoomType chỉ dùng trong một Property, nhưng schema không đảm bảo điều đó lâu dài. Mỗi Property mục tiêu có 4 service IDs đi qua RoomTypes, kể cả tập rentable RoomTypes.

Từ “per kWh”, “per m3”, “monthly” nằm trong Name; chúng không phải UnitCode hay billing contract. Không khẳng định 3,500 là VND/kWh chỉ dựa trên Name.

## Schema và data Billing

- Invoices: ContractId, InvoiceMonth, DueDate, Status.
- InvoiceDetails: InvoiceId, ItemType, Description, Amount; không có ServiceId, Quantity, UnitCode, UnitPrice hoặc RateId.
- UtilityReadings: ContractId, ReadingMonth, ElectricStart/End, WaterStart/End; không có đơn giá.
- Snapshot live: 5 invoices, 25 details (5 mỗi ItemType: Electricity, Internet, Rent, Trash, Water), 5 readings trên 3 Contract và 2 tháng. Không có duplicate Contract+ReadingMonth.
- Payments có 6 rows, 3 tham chiếu Booking và 3 Invoice; hiện cả 6 có đúng một target, dù schema không có CHECK ràng buộc điều này.

Các table có một phần dữ liệu lịch sử nhưng không chứa đơn giá/unit có cấu trúc để làm nguồn “chi phí sinh hoạt hiện tại” trên Property Detail.

## FE Billing là mô phỏng

ManagementInvoicesPage lấy dữ liệu qua getManagementScope và gọi createManagedInvoice. CreateInvoiceModal có hằng số: điện 2,047; nước 6,700; gửi xe 70,000; Internet 100,000; rác 35,000. managedInvoices.ts tính usage × rate rồi ghi hóa đơn và số công tơ vào localStorage. Không thấy request API cho luồng này.

So với DB Services.Price: điện 3,500, nước 20,000, Internet 100,000, rác 30,000; Parking chưa có Service catalog tương ứng. Giá FE là mock, không được backfill hoặc dùng làm rate live nếu owner/Billing chưa xác nhận.

## Rủi ro thiết kế

1. Sửa Services.Price hôm nay đổi giá toàn cục và không giữ effective history.
2. RoomTypeServices biểu diễn applicability theo RoomType, không làm giá thành property-specific.
3. InvoiceDetails hiện không lưu rate/unit/quantity có thể kiểm tra lại. Description text không phải snapshot máy đọc đáng tin cậy.
4. Không thay RoomTypes.Price bằng Service price: RoomTypes.Price là rent cơ bản; Contract.MonthlyRent và Invoice line là khái niệm snapshot khác.
5. FE mock và live DB có giá chênh; không tự chọn một phía làm dữ liệu đúng.

## Hướng đề xuấ

Nếu rate khác theo khu trọ, chọn rate theo Property+Service, UnitCode/BillingBasis, EffectiveFrom/To; giữ Services làm catalog. Xác định Services.Price là legacy/catalog default và loại khỏi tính tiền mới khi cutover; không dual-write hai nguồn. Invoice creation cần snapshot ServiceId/RateId, quantity, unit, unit price và amount vào line đã phát hành. RoomTypeServices chỉ tiếp tục quyết định applicability khi Billing xác nhận dịch vụ phụ thuộc RoomType; phí property-wide cần quy tắc riêng.
