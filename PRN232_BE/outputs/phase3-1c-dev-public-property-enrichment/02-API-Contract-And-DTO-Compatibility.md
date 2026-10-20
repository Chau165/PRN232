# API contract và tương thích DTO

## Endpoint

GET /api/public/properties/{id} giữ route và quy tắc hiện có: trả 200 khi Property có phòng rentable; trả generic 404 nếu không tìm thấy hoặc không còn phòng rentable. Không thêm endpoint hoặc query parameter.

## Fields giữ nguyên

PropertyId, PropertyName, AddressText, Ward, District, City, PrimaryImageUrl, AvailableRoomCount, StartingPrice và RoomAmenities. RoomAmenities tiếp tục là danh sách AmenityId/Name cùng RentableRoomCount.

## Fields thêm dạng additive

- Description: string hoặc null.
- CommonAmenities: mảng AmenityId/Name; rỗng khi chưa có link.
- LivingCosts: mảng ServiceId, ServiceName, Price, UnitCode, BillingBasis, EffectiveFrom, EffectiveTo; rỗng khi không có rate hiệu lực.

DTO positional constructor cũ được giữ nguyên; field mới là init properties với mặc định null/empty. JSON camelCase được xác nhận qua API integration tests. Các giá trị DateOnly serialize thành YYYY-MM-DD. Không có OwnerId, CustomerId, booking, contract, invoice hoặc credential trong response.

## Hành vi theo feature flag

| Flag | Schema | Hành vi |
|---|---|---|
| false hoặc không cấu hình | Cũ hoặc mới | Chỉ truy vấn các cột/bảng của public detail cũ. Field mới vẫn additive: description null, commonAmenities/livingCosts rỗng. |
| true | Đã migrate Phase 3.1B | Đọc Description, PropertyAmenities và rates hiệu lực. |
| true | Schema cũ | Không hỗ trợ; query lỗi được để nổi lên, không bắt lỗi để giấu schema drift. |

Không đổi hình dạng hiện tại của roomAmenities hay contract 404. Public room detail giữ DTO service cũ.

## Compatibility result

PostgreSQL test pre-enrichment xác nhận bốn public GET và các GET management Property/Room liên quan tiếp tục chạy khi flag false; captured SQL không nhắc Description, PropertyAmenities hoặc PropertyServiceRates. Cùng test xác nhận GetEntityByIdAsync của Property lỗi 42703 trên schema cũ vì materialize full entity. Đây là blocker riêng với management write path, không phải lỗi public detail branch.