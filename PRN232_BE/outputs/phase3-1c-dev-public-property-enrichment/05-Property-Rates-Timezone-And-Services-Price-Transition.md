# Rates, timezone và chuyển tiếp Services.Price

## Rate public mới

LivingCosts đọc PropertyServiceRates, nối Service chỉ để lấy tên, lọc chính xác theo PropertyId và ngày Việt Nam:

EffectiveFrom <= effectiveDate && (EffectiveTo IS NULL || EffectiveTo > effectiveDate)

Khoảng là nửa mở [EffectiveFrom, EffectiveTo). Instant UTC lấy một lần cho request rồi đổi sang Asia/Ho_Chi_Minh; không đổi timezone của RoomRentabilityPolicy. Đơn vị/BillingBasis lấy nguyên từ DB: kwh/usage, m3/usage, room_month/fixed_room, person_month/occupant, vehicle_month/vehicle. Rate là thông tin tham khảo theo Property; API không khẳng định khoản phí bắt buộc hoặc tổng chi phí tháng.

## Consumer matrix

| Consumer | Nguồn giá hiện tại | Ghi chú |
|---|---|---|
| GET /api/public/properties/{id} | PropertyServiceRates theo Property + ngày hiệu lực | Field mới livingCosts có đơn vị, billing basis và hiệu lực. |
| GET /api/public/rooms/{roomId} | RoomTypeServices -> Services.Price | Public DTO cũ chỉ có serviceId/name/price, không có unit hoặc hiệu lực. |
| GET /api/services và ServiceCatalogService | Services.Price | Catalog/management CRUD vẫn dùng giá service toàn cục. |
| GET /api/room-types/{id}/services, RoomTypeService/RoomTypeRepository | Services.Price qua RoomTypeServices | API quản lý loại phòng vẫn trả ServiceDto có Price. |
| FE RoomDetailModal | Public room detail services[].price | Format tiền VND, không hiện đơn vị. |
| FE PropertyDetailPage | Chưa gọi field enrichment | Vẫn hiển thị placeholder cho giới thiệu, tiện ích chung và chi phí. |

## Quyết định không gây nhập nhằng

Giữ các consumer Services.Price và public room DTO nguyên trạng để tránh breaking change. Integration test cố ý đặt Services.Price = 100.000, PropertyServiceRates = 120.000 để chứng minh hai nguồn có thể khác; test cũng chứng minh service có Services.Price nhưng không có rate hiệu lực sẽ không lọt vào livingCosts. Không fallback, dual-write hoặc đổi Billing.

Vì public room contract vẫn có consumer bắt buộc đọc Services.Price, PublicBrowsing:PropertyDetailEnrichmentEnabled phải giữ false ngoài test cho đến khi owner duyệt cách chuyển consumer, nhãn giao diện và hợp đồng FE/BE. Test bật flag chỉ chứng minh tính đúng dữ liệu trên schema migrate; không phải phê duyệt bật feature ở staging/live.