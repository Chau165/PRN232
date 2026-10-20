# Tiện ích công khai và scope Property

## Tiện ích chung

CommonAmenities lấy duy nhất từ PropertyAmenities có PropertyId bằng Property trong route; join Amenities để lấy tên; thứ tự Name rồi AmenityId. Không chuyển RoomAmenities lên Property, không suy luận tiện ích chung từ phòng mẫu.

## Tiện ích phòng

RoomAmenities giữ semantics hiện hữu: query các liên kết thuộc các Room trong tập RoomRentabilityPolicy.IsRentableAt(asOfUtc), group theo AmenityId/Name và đếm số RoomId riêng biệt. Chỉ phòng thực sự có thể thuê được tính vào RentableRoomCount. Status phòng, booking và contract tiếp tục theo policy Phase 2A; thời gian booking/contract vẫn đánh giá bằng UTC.

## Kết quả rỗng

Property không có mô tả trả description null; không có liên kết tiện ích chung trả commonAmenities []; không có room amenity trong tập phòng rentable trả roomAmenities []; không có rate hiệu lực trả livingCosts []. Không có fallback/mock hoặc dữ liệu của Property khác.

## PostgreSQL evidence

Fixture tổng hợp có hai Property mang description và amenities riêng, Property thứ ba có Description null, room-only amenity, phòng thuê được, phòng bị booking chặn, phòng bị contract chặn, và Property không còn phòng public. Assertions xác nhận không lẫn Property amenities với room amenities, RentableRoomCount chỉ tính hai phòng đúng điều kiện, empty state đúng và 404 được giữ nguyên.

Các fixture này chỉ tồn tại trong database local do test tạo; không dùng dữ liệu Supabase hay thông tin cá nhân.