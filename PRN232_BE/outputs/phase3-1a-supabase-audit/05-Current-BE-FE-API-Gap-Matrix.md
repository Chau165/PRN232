# Phase 3.1A — Current BE/FE/API Gap Matrix

| Field/section | Live DB | BE source | FE source | Kết luận |
|---|---|---|---|---|
| Introduction / Description | Properties không có Description. | Property entity, create/update DTO và PublicPropertyDetailDto đều không có trường này. | PropertyDetailPage có placeholder “Thông tin giới thiệu đang được cập nhật”, không render mô tả. | MISSING COLUMN + API DOES NOT PROJECT + FE DOES NOT RENDER. |
| Common amenities | Không có PropertyAmenities hay bảng tương đương. Amenities catalog có 6 entries, RoomAmenities chỉ liên kết Room. | PublicPropertyDetailDto không có commonAmenities; repository không truy vấn property link. | Section Tiện ích chung là placeholder tĩnh. | MISSING TABLE/RELATIONSHIP + API DOES NOT PROJECT + FE DOES NOT RENDER. |
| Room amenities | 27 link RoomAmenities; 0 orphan/duplicate. Snapshot có 15 amenity-Property coverage rows; báo cáo 03 có chi tiết coverage. | Source PublicPropertyDetailDto có roomAmenities; GetDetailAsync group RoomAmenities thuộc phòng rentable và trả RentableRoomCount. | Type roomAmenities optional; UI render tên và số phòng/tổng phòng rentable, empty-state nếu [] hoặc không có. | Data và source API đã có. RUNNING API IS STALE: Swagger runtime 5102 không khai báo roomAmenities. |
| Living costs | Services.Price và RoomTypeServices có; không có UnitCode/BillingBasis, rate theo Property, effective dates hay tariff table. | PublicPropertyDetailDto/repository không project livingCosts. Service catalog và RoomType endpoints expose global Price riêng. | Section hiển thị placeholder về chi phí theo đơn vị thanh toán. | Không thiếu bảng Service, nhưng thiếu nguồn giá niêm yết đáng tin cậy; API DOES NOT PROJECT + FE DOES NOT RENDER. |
| Property image | Properties.PrimaryImageUrl text nullable; cả 3 mục tiêu có URL. | Property Entity và public list/detail projection có PrimaryImageUrl. Migration AddPropertyPrimaryImageUrl có trong live history. | FE detail/list dùng field ảnh theo public contract. | Field đã có; không phải gap Phase 3.1A. |
| Starting rent | RoomTypes.Price numeric(18,2); truy cập qua Rooms.RoomTypeId. | Public detail tính min Price trên cùng tập rentable rooms. | FE format startingPrice. | Rent cơ bản, không phải living cost/Service.Price. |
| Invoice line pricing | InvoiceDetails chỉ ItemType/Description/Amount; UtilityReadings chỉ số điện nước. | Chỉ Entity/DbSet/mapping; không thấy BE Billing execution. | Màn quản lý hóa đơn tính với hằng số và lưu localStorage. | Billing backend NOT IMPLEMENTED; FE mock không phải API/live pricing. |
| API runtime parity | Swagger localhost:5102 HTTP 200; schema gồm propertyId, propertyName, addressText, ward, district, city, primaryImageUrl, availableRoomCount, startingPrice; thiếu roomAmenities. | Source hiện tại trả thêm roomAmenities. Source cũng chưa có Description/commonAmenities/livingCosts. | Vite mặc định dùng /api và proxy target localhost:5102; có thể override bằng env. | Source/runtime lệch cho roomAmenities. Không gọi API GET có DB query trong audit. |

## Trạng thái nguyên nhân

- MISSING TABLE/COLUMN: Description, PropertyAmenities, PropertyServiceRates/ServiceRates/UtilityRates.
- TABLE EXISTS BUT EMPTY: không phải nguyên nhân chính; Services, RoomTypeServices, RoomAmenities đều có dữ liệu.
- LINK ROW MISSING: không phát hiện orphan/duplicate trên RoomAmenities/RoomTypeServices; amenity trên phòng không rentable không được tính vào public summary.
- API DOES NOT PROJECT: Description, commonAmenities, livingCosts.
- FE DOES NOT RENDER: FE chưa có nguồn description/common amenity/cost; các section chủ ý hiển thị trạng thái cập nhật.
- RUNNING API IS STALE: roomAmenities có trong source nhưng không có trong Swagger runtime trên 5102.

Vite proxy target lấy từ PRN232_DEV_API_PROXY_TARGET nếu được đặt, nếu không mặc định localhost:5102. Không xác minh environment override của tiến trình FE hiện tại.
