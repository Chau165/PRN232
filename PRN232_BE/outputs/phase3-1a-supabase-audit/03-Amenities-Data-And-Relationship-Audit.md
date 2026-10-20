# Phase 3.1A — Amenities Data and Relationship Audi

## Quan hệ live

Live chỉ có Amenities(AmenityId, Name) và RoomAmenities(RoomId, AmenityId). RoomAmenities có PK ghép, hai FK CASCADE; hiện có 27 link, 0 orphan Room, 0 orphan Amenity, 0 duplicate link. Không có PropertyAmenities hoặc quan hệ property-level tương đương.

Do đó:

- Amenity trong RoomAmenities chỉ chứng minh tiện ích được gắn vào phòng đó.
- “Có tại tất cả phòng hiện thuê được” chỉ được nói khi rentableRoomCount bằng AvailableRoomCount.
- Không đổi nghĩa tiện ích phòng thành “tiện ích chung của khu trọ”.
- Property-level common amenities chưa được nhập vì schema không có nơi lưu; đây là MISSING TABLE/RELATIONSHIP, không phải bảng có nhưng rỗng.

## Chính sách phòng dùng cho snapsho

Public source hiện dùng RoomRentabilityPolicy.IsRentableAt: Room.Status phải là Available; Booking Deposited chặn; Pending chặn khi ExpiryDate lớn hơn instant UTC; Booking status không nhận diện chặn; Contract Active hoặc status không nhận diện chặn. Tập recognized Contract status trong source chỉ có Active, nên mọi Contract hiện hữu đều loại phòng theo biểu thức hiện tại.

Không có publication flag trên Property. Public listing/detail chỉ trả Property khi có ít nhất một phòng theo predicate này. Báo cáo Phase 1.5 vẫn ghi ngữ nghĩa một số trạng thái chờ owner quyết định; số dưới đây phản ánh hành vi source tại snapshot, không chứng minh chính sách đã được duyệt.

## Coverage: số phòng có amenity — tổng phòng / phòng rentable

| Property | Amenity | All rooms | Rentable rooms |
|---|---|---:|---:|
| Nhà trọ Bình An | Wi-Fi | 4 | 1 |
| Nhà trọ Bình An | Air conditioner | 3 | 1 |
| Nhà trọ Bình An | Balcony | 1 | 1 |
| Nhà trọ Bình An | Private kitchen | 2 | 1 |
| Nhà trọ Bình An | Mezzanine | 1 | 0 |
| Nhà trọ Bình An | Shared washing machine | 2 | 0 |
| Khu trọ Sunrise | Wi-Fi | 3 | 2 |
| Khu trọ Sunrise | Air conditioner | 2 | 1 |
| Khu trọ Sunrise | Balcony | 2 | 1 |
| Khu trọ Sunrise | Private kitchen | 1 | 1 |
| Khu trọ Sunrise | Shared washing machine | 1 | 1 |
| Nhà trọ Minh Tâm | Wi-Fi | 2 | 1 |
| Nhà trọ Minh Tâm | Air conditioner | 1 | 1 |
| Nhà trọ Minh Tâm | Balcony | 1 | 1 |
| Nhà trọ Minh Tâm | Mezzanine | 1 | 0 |

Không có link Shared washing machine trên phòng rentable ở Bình An, hoặc Mezzanine trên phòng rentable ở Bình An/Minh Tâm. Đây là aggregate theo link hiện tại; không suy ra phòng không rentable không có tiện ích.

## BE/FE mapping hiện tại

- PublicPropertyListingRepository.GetDetailAsync join RoomAmenities với tập rentable rooms, group theo AmenityId/Name và trả RentableRoomCount. Đây là roomAmenities, đúng phạm vi phòng.
- PublicPropertyDetailDto có roomAmenities nhưng không có commonAmenities.
- PropertyDetailPage hiển thị coverage count cho roomAmenities. Section Tiện ích chung hiển thị “đang được cập nhật”.
- Swagger runtime tại localhost:5102 không có roomAmenities dù source có; FE dùng runtime mặc định có thể nhận schema cũ và hiển thị empty state. Kiểm tra Swagger không gọi public GET và không truy vấn DB.
- Public response giữ 404 cho Property không có phòng rentable theo contract trước đó; audit không đổi chính sách.

## Đề xuất để owner duyệ

Giữ RoomAmenities cho tiện ích từng phòng và tiếp tục summary theo rentable-room set. Nếu cần tiện ích chung, thêm PropertyAmenities(PropertyId, AmenityId) dùng lại Amenities, PK ghép và FK; cần CRUD có owner/Manager scope. Không backfill tiện ích phòng thành tiện ích chung. API nên trả commonAmenities và roomAmenities tách biệt; mảng rỗng khi chưa có dữ liệu.
