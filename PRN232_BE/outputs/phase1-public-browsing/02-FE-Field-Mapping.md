# 02 — Ánh xạ Home UI và dữ liệu

## Stack và nguồn dữ liệu hiện tại

Frontend là React 19, TypeScript, Vite, React Router; state trong các component dùng React useState. package.json không có client HTTP/query library. Tìm trong src không thấy fetch, axios, biến API URL hay route /api. Home dùng mảng cục bộ từ src/data/properties.ts; Property detail dùng properties[0], propertyDetails.ts; danh sách phòng dùng src/data/rooms.ts. Đây là dữ liệu mock, không phải API hoặc live database.

Tham chiếu: src/pages/HomePage/HomePage.tsx:1-38; src/components/home/SearchSection.tsx:1-56; src/data/properties.ts:1-45; src/data/rooms.ts:1-62; src/pages/PropertyDetailPage/PropertyDetailPage.tsx:1-160; src/pages/AvailableRoomsPage/AvailableRoomsPage.tsx:1-79.

## Bảng field Home và chi tiết

| UI/component | Field hiển thị hoặc filter | Nguồn hiện tại | Nguồn DB / API hiện có | API mới / field đề xuất | Trạng thái |
|---|---|---|---|---|---|
| HeroSearch | Ảnh hero, tiêu đề | public/hero-room.png và text tĩnh | Không có trường hero trong DB | Giữ asset/thông điệp tĩnh | E — static |
| SearchSection | Keyword/tên đường/trường | input không bind state | PropertyName, AddressText, Ward, District, City; chưa có filter API | keyword | B — dữ liệu có, thiếu API logic |
| SearchSection | Khoảng giá | SelectControl chỉ là button label | RoomTypes.Price qua RoomTypeId; Rooms GET chỉ có maxPrice | minPrice/maxPrice trên cùng eligible Room | B — phần maxPrice có cho room list; Home aggregation chưa có |
| SearchSection | Loại phòng | SelectControl không có option/state | GET /api/room-types trả từng ID, TypeName, Price | roomTypeName nhóm logic; roomTypeId chỉ exact row | B — thiếu filter; option cần dedup |
| FilterChips | Vị trí hiện tại | Button không gọi geolocation | Properties.Latitude/Longitude, precision numeric(18,2) | latitude/longitude/radiusKm sau quyết định precision | D — quyền trình duyệt + precision/geo |
| FilterChips | Số người ở | Button tĩnh | Không có capacity/occupant column | occupantCount chỉ sau bổ sung dữ liệu | C — thiếu schema |
| FilterChips | Tiện ích | Button tĩnh | Amenities + RoomAmenities; GET /api/amenities và /api/rooms/{id}/amenities | amenityIds áp trên cùng một eligible Room | B — schema có, thiếu listing filter |
| FilterChips | Radius 1/3/5/10km | state chỉ đổi nút đang chọn | Tọa độ Property nhưng độ chính xác thấp | radiusKm sau cải thiện tọa độ hoặc geospatial | D — chưa thể lọc chính xác |
| FilterChips | Bộ lọc nâng cao | Button không xử lý | Không có endpoint | Chỉ bật các query đã có data | E — chưa implement |
| ResultsHeader | Số kết quả = 42 | Hằng số | Tổng số Property sau filter | totalCount từ pagination | E — hardcoded |
| ResultsHeader | Sắp xếp “Phù hợp nhất” | Button tĩnh | Không có rank/relevance | sortBy=startingPriceAsc/startingPriceDesc; nearest chỉ khi có tọa độ tốt | B/D |
| HorizontalPropertyCard | PropertyName | data mock name | Properties.PropertyName; GET /api/properties có nhưng trả DTO quản trị | propertyName | A — schema/API cơ bản có |
| HorizontalPropertyCard | Address | mock string ghép | AddressText, Ward, District, City | addressText + ward/district/city | A — cần map/format |
| HorizontalPropertyCard | Distance | mock “1,2 km” | Có tọa độ thô, không có distance | Không trả distance MVP; thêm sau geolocation | D/E — hiện giả lập |
| HorizontalPropertyCard | Primary image | Unsplash URL tĩnh | Không có Property image; RoomImages.ImageUrl/IsPrimary | representativeImageUrl từ phòng thuê được; null nếu thiếu | B |
| HorizontalPropertyCard | Available count | mock số phòng | Rooms.Status + Booking + Contract | availableRoomCount từ tập eligible | B — RoomCount hiện tại là tổng |
| HorizontalPropertyCard | Starting price | mock string | Rooms.RoomTypeId → RoomTypes.Price | startingPrice | B — phải tính trên eligible rooms |
| HorizontalPropertyCard | Amenities summary | mock array | RoomAmenities → Amenities.Name, theo từng Room | amenitiesSummary từ một phòng đại diện, hoặc ghi rõ phạm vi | B — không phải Property-level |
| HorizontalPropertyCard | Description | mock description | Không có Property/Room description | Ẩn hoặc bổ sung nguồn sau | C |
| HorizontalPropertyCard | Xem chi tiết | link luôn tới Sunrise Residence | Không đọc ID route động | propertyId route và GET public detail | E — link hardcoded |
| MapSection | Marker, x/y và giá | properties mock với CSS % | Property lat/lon nhưng numeric(18,2) | tọa độ thật sau xác nhận visibility/precision | E/D — marker hiện không phải map tile |
| MapSection | Selected Property card | state selected hoạt động; data mock | Các field Property API sau khi tích hợp | cùng DTO listing | B |
| PropertyDetailPage | Tên/địa chỉ/giá/count | properties[0] | Property/Rooms/RoomTypes | public property detail DTO | B |
| PropertyDetailPage | Gallery | sunrisePhotos, URL tĩnh | Chỉ RoomImages, không có Property image | ảnh phòng đại diện có sourceRoomId; không giả làm ảnh Property | B |
| PropertyDetailPage | Amenities | detailAmenities tĩnh | RoomAmenities/Amenities theo phòng | amenities của phòng đại diện hoặc label rõ scope | B |
| PropertyDetailPage | Chi phí dịch vụ | propertyCosts tĩnh, có đơn vị | Services.Name/Price không có đơn vị | serviceFees giữ riêng; không cộng thành tiền thuê tháng | C/B |
| PropertyDetailPage | Nội quy/mô tả khu trọ | propertyRules/text tĩnh | Không có Property.Description/Rules | Ẩn cho tới khi có nguồn | C |
| AvailableRooms | RoomCode, AreaSqm | room mock; component dùng code/size | Rooms.RoomCode/AreaSqm | room list DTO | A/B |
| AvailableRooms | Occupants, floor, ngày trống | mock strings | Không có capacity/floor/available-from; Booking thiếu move-in date | Không hiển thị hoặc thêm schema/rule sau | C |
| AvailableRooms | Giá/ảnh | mock | RoomTypes.Price, RoomImages | giá theo RoomTypeId và ảnh phòng | B |
| AvailableRooms | Amenity tags | mock cardAmenities | RoomAmenities/Amenities | amenities từ cùng Room | B |
| RoomDetailModal | Description, tối đa người, tầng, ngày trống | mock | Không có các cột tương ứng | Không trả như dữ liệu thật | C |
| Navbar/Login/Register | Đăng nhập, đăng ký | Navbar login; Login có link Register; FE auth dựa localStorage | POST /api/auth/register tồn tại; không ảnh hưởng browsing | giữ auth riêng khỏi API đọc công khai | A/E — FE auth mock |

Tham chiếu FE: SearchSection.tsx:21-55; FilterChips.tsx:3-32; SelectControl.tsx:1-9; ResultsHeader.tsx:1-13; HorizontalPropertyCard.tsx:27-85; MapSection.tsx:1-105; AvailableRooms.tsx:1-79; RoomDetailModal.tsx:1-125; propertyDetails.ts:1-33; Navbar.tsx:13, 219-223; LoginPage.tsx:117-121; RegisterPage.tsx:119-132.

## Mismatch và quy tắc đề xuất

- Mock Home có 3 Property, ResultsHeader lại ghi 42 kết quả; trang AvailableRooms ghi 5 phòng nhưng mảng rooms hiện có 3. Đây là UI mock, không phải số DB.
- Property Detail luôn chọn properties[0]; card link luôn mở /property/sunrise-residence.
- Amenities trên từng Room có thể khác nhau. Khi filter nhiều amenity, phải yêu cầu cùng một eligible Room có đủ tất cả; không gộp Wi-Fi từ phòng A và máy lạnh từ phòng B để cho Property match.
- Room type option nên dedup theo tên đã chuẩn hóa, nhưng query dùng roomTypeName logic. Exact roomTypeId chỉ chọn một row; giá và services luôn lấy theo ID thật.
- Các field không có schema (occupant count, floor, available-from, mô tả, rules, unit fee) phải bỏ/đánh dấu chưa có dữ liệu trong Phase 2; không giữ mock làm dữ liệu thật.