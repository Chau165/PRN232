# 05 — Hợp đồng API Public Browsing Phase 2

Tất cả nội dung dưới đây là PROPOSED, chưa được triển khai. Chỉ bật public endpoint sau khi chốt policy Property visibility và quy tắc Booking/Contract ở tài liệu 06.

## Chọn chiến lược endpoint

Chọn Strategy B: API public riêng, DTO riêng, không đổi route/response CRUD hiện tại. Các danh mục GET /api/room-types, /api/amenities, /api/services có thể tái dùng; không dùng trực tiếp GET /api/rooms hoặc GET /api/properties làm hợp đồng Home.

## Endpoint tối thiểu

### 1. GET /api/public/properties

- Truy cập: anonymous; [AllowAnonymous] tường minh sau khi policy visibility được duyệt.
- FE: HomePage, SearchSection, HorizontalPropertyCard, MapSection.
- Query: keyword, city, district, ward, minPrice, maxPrice, roomTypeName, roomTypeId, amenityIds, availableOnly, sortBy, page, pageSize.
- page mặc định 1; pageSize mặc định 10, giới hạn 1–50; page ≥ 1.
- minPrice ≤ maxPrice; giá không âm; id amenity/type dương; sortBy chỉ nhận startingPriceAsc hoặc startingPriceDesc ở MVP.
- roomTypeName và roomTypeId loại trừ nhau để tránh nhầm logical label với một row cụ thể.
- availableOnly mặc định true. availableOnly=false chỉ được bật khi policy public Property đã xác định; Property hết phòng có startingPrice=null.
- city/district/ward và keyword so khớp không phân biệt hoa thường, trim khoảng trắng; keyword tìm PropertyName/AddressText/Ward/District/City.
- minPrice/maxPrice, roomTypeName/roomTypeId, amenityIds là điều kiện của Room. Tất cả điều kiện Room-level phải cùng áp dụng trên một eligible Room. amenityIds là AND; Property chỉ khớp nếu có một Room thỏa mọi amenity.
- occupantCount chưa hỗ trợ vì schema không có capacity. latitude/longitude/radiusKm chưa hỗ trợ MVP do precision tọa độ không đủ.
- sort mặc định: startingPrice ASC, rồi PropertyId ASC; price DESC có PropertyId tie-break. UI “phù hợp nhất” chưa có rank.
- 200 với trang rỗng nếu không có kết quả; 400 với query invalid/unsupported.

Response DTO đề xuất: PublicPropertyCardDto gồm propertyId, propertyName, addressText, ward, district, city, latitude, longitude, representativeRoomId, representativeImageUrl (nullable), representativeRoomAmenities, availableRoomCount, startingPrice (nullable). Không có OwnerId/OwnerName. Amenities được gắn nhãn ở FE là tiện ích của phòng đại diện, không phải tiện ích chung mọi phòng.

### 2. GET /api/public/properties/{propertyId}

- Truy cập anonymous sau khi policy visibility chốt.
- FE: PropertyDetailPage.
- 200 PublicPropertyDetailDto: thông tin Property, số phòng thuê được, giá bắt đầu, một gallery giới hạn ảnh từ các eligible Room (mỗi ảnh có sourceRoomId), danh sách roomTypeOptions đã dedup theo logical TypeName.
- roomTypeOptions dùng key đã normalize và label người dùng thấy; không dùng RoomTypeId làm key cho option logical.
- Không tải toàn bộ Equipment/Booking/Contract. Property không có image/Description/Rules riêng; không trả dữ liệu giả.
- 404 nếu Property không tồn tại hoặc không được public policy cho phép. Property tồn tại nhưng hết phòng có thể trả detail với count=0, startingPrice=null nếu availableOnly=false được cho phép.

### 3. GET /api/public/properties/{propertyId}/rooms

- Truy cập anonymous sau khi policy visibility chốt.
- FE: AvailableRoomsPage và component AvailableRooms.
- Query: page, pageSize, sortBy (priceAsc/priceDesc; mặc định priceAsc; tie-break RoomId).
- Chỉ trả Room thỏa rentability predicate; property không công khai trả 404; Property công khai nhưng không có phòng thuê được trả 200 với items=[].
- DTO ngắn: roomId, roomCode, roomTypeId, roomTypeName, baseRent, areaSqm, primaryImageUrl, amenities.
- Cần endpoint riêng vì GET /api/rooms?propertyId= hiện có không lọc Booking/Contract và có thể trả mọi trạng thái phòng.

### 4. GET /api/public/rooms/{roomId}

- Truy cập anonymous; FE: RoomDetailModal.
- Chỉ trả nếu Property được public policy cho phép và Room hiện thuê được; nếu không tồn tại/không public/không rentable thì 404 để tránh dò trạng thái phòng.
- 200 PublicRoomDetailDto: roomId, roomCode, roomTypeId, roomTypeName, baseRent, areaSqm, rentableNow=true, Property summary, images, amenities, serviceFees, equipment.
- ServiceFees lấy theo Room.RoomTypeId → RoomTypeServices → Services, giữ name/price riêng; không cộng thành monthly total.
- Equipment public projection đề xuất chỉ gồm Name. Không trả Supplier, warranty, install date, internal Status.
- Không trả tenant identity, contract/booking data, Owner/manager PII hoặc maintenance records.

## Quy tắc tính nhất quán

Tập RentableRooms(Property, asOfUtc) được tính một lần theo quy tắc sau khi product owner chốt status Booking/Contract và visibility. AvailableRoomCount = COUNT(DISTINCT RoomId) và StartingPrice = MIN(RoomTypes.Price) từ cùng tập này. Không dùng Property.RoomCount.

Price/amenity/type filter xác định Property có match nếu có ít nhất một Room từ RentableRooms thỏa toàn bộ điều kiện Room-level. Count và startingPrice trên card vẫn mô tả toàn bộ phòng thuê được của Property, không chỉ phòng match filter; đây là đề xuất để card hiển thị “Còn … phòng” và “Giá từ …” đúng nghĩa. Nếu muốn count sau filter, phải đổi tên field thành matchingRoomCount và chốt riêng.

Đại diện ảnh/amenities: chọn Room thuê được theo Price ASC, RoomId ASC. Lấy primary image của phòng đó; nếu không có thì lấy ảnh đầu theo RoomImageId; nếu không có ảnh, trả null và FE dùng placeholder. Amenities lấy từ chính phòng đại diện, không union nhiều phòng.

Zero availability: mặc định loại Property khỏi listing (availableOnly=true) để không có “Từ …”. Nếu cho phép availableOnly=false, trả count 0, price null và FE hiển thị “Hết phòng”.

## RoomTypeName và Cách 1

- Giá luôn từ RoomTypes.Price theo ID mà Room tham chiếu. Không tìm giá bằng TypeName.
- roomTypeName là filter logical: chuẩn hóa Unicode FormKC, Trim, gộp chuỗi whitespace liên tiếp thành một dấu cách, so sánh case-insensitive invariant.
- roomTypeId là filter chính xác một row RoomTypes; không đại diện mọi Studio.
- FE lấy GET /api/room-types, nhóm option theo normalized TypeName, chọn label hiển thị ổn định (tên đã trim từ RoomTypeId nhỏ nhất). Query roomTypeName gửi label logical; mỗi Room vẫn giữ RoomTypeId và giá riêng.
- RoomTypeServices luôn lấy từ RoomTypeId thật; hai Studio khác ID có thể có services khác nhau.
- Không thêm Rooms.MonthlyPrice, PropertyRoomPrice hoặc RoomTypes.PropertyId; không tạo/sửa dữ liệu RoomTypes trong Phase 1.

## JSON mẫu — mọi ID/tên/giá bên dưới chỉ minh họa

### A. Property listing

~~~json
{
  "items": [
    {
      "propertyId": 101,
      "propertyName": "Sunrise Residence",
      "addressText": "12 Võ Văn Ngân",
      "ward": "Linh Chiểu",
      "district": "Thủ Đức",
      "city": "TP.HCM",
      "latitude": 10.85,
      "longitude": 106.77,
      "representativeRoomId": 1101,
      "representativeImageUrl": "https://cdn.example.test/rooms/1101/primary.jpg",
      "representativeRoomAmenities": ["Wi-Fi", "Máy lạnh"],
      "availableRoomCount": 2,
      "startingPrice": 3500000
    },
    {
      "propertyId": 102,
      "propertyName": "Mộc Miên",
      "addressText": "8 Lê Văn Chí",
      "ward": "Linh Trung",
      "district": "Thủ Đức",
      "city": "TP.HCM",
      "latitude": 10.86,
      "longitude": 106.78,
      "representativeRoomId": 2201,
      "representativeImageUrl": null,
      "representativeRoomAmenities": [],
      "availableRoomCount": 1,
      "startingPrice": 4200000
    }
  ],
  "totalCount": 2,
  "page": 1,
  "pageSize": 10
}
~~~

### B. Property details

~~~json
{
  "propertyId": 101,
  "propertyName": "Sunrise Residence",
  "addressText": "12 Võ Văn Ngân",
  "ward": "Linh Chiểu",
  "district": "Thủ Đức",
  "city": "TP.HCM",
  "latitude": 10.85,
  "longitude": 106.77,
  "availableRoomCount": 2,
  "startingPrice": 3500000,
  "representativeImages": [
    {
      "roomId": 1101,
      "imageUrl": "https://cdn.example.test/rooms/1101/primary.jpg"
    }
  ],
  "availableRoomTypes": [
    {
      "key": "studio",
      "label": "Studio"
    }
  ]
}
~~~

### C. Room details

~~~json
{
  "roomId": 1101,
  "roomCode": "A101",
  "roomTypeId": 201,
  "roomTypeName": "Studio",
  "baseRent": 3500000,
  "areaSqm": 20,
  "rentability": {
    "availableNow": true,
    "asOfUtc": "2026-10-18T00:00:00Z"
  },
  "property": {
    "propertyId": 101,
    "propertyName": "Sunrise Residence",
    "addressText": "12 Võ Văn Ngân",
    "district": "Thủ Đức",
    "city": "TP.HCM"
  },
  "images": [
    "https://cdn.example.test/rooms/1101/primary.jpg"
  ],
  "amenities": [
    {
      "amenityId": 1,
      "name": "Wi-Fi"
    }
  ],
  "serviceFees": [
    {
      "serviceId": 4,
      "name": "Internet",
      "price": 100000
    }
  ],
  "equipment": [
    {
      "name": "Máy lạnh"
    }
  ]
}
~~~

Service price không có billing unit trong schema; ví dụ không khẳng định 100000 là theo tháng. Availability JSON là response minh họa, không phải output DB thật.

### D. Không có Property match

~~~json
{
  "items": [],
  "totalCount": 0,
  "page": 1,
  "pageSize": 10
}
~~~

### E. Property công khai nhưng không có phòng thuê được

Chỉ trả khi client dùng availableOnly=false và visibility policy đã cho phép.

~~~json
{
  "propertyId": 103,
  "propertyName": "Căn hộ mẫu",
  "addressText": "Địa chỉ minh họa",
  "ward": "Phường minh họa",
  "district": "Quận minh họa",
  "city": "TP.HCM",
  "latitude": 10.80,
  "longitude": 106.70,
  "representativeRoomId": null,
  "representativeImageUrl": null,
  "representativeRoomAmenities": [],
  "availableRoomCount": 0,
  "startingPrice": null
}
~~~

### F. Room không tồn tại/không public/không rentable

~~~json
{
  "type": "about:blank",
  "title": "Not Found",
  "status": 404
}
~~~

## Bảo mật, performance và coding conventions

- Public endpoints dùng allow-list DTO, [AllowAnonymous] tường minh, không lấy Owner/tenant/Contract/Booking fields.
- Chỉ public hóa Property sau policy xác định; không suy luận Published từ việc Owner tồn tại.
- Projection AsNoTracking và aggregation trong database; tránh Include toàn bộ graph hoặc N+1.
- Filter, count, min price, sort và page chạy server-side. Count distinct sau joins.
- Existing indexes có FK coverage nhưng chưa có index Status/ExpiryDate hoặc Contract Status/date; đo bằng EXPLAIN ANALYZE trên query thực tế trước khi thêm index. Không thêm index ở Phase 1.
- Giữ N-layer: controller → interface/service → repository; EF query chỉ ở repository; DTO tách public khỏi management theo CODING_STANDARDS.md:23-41, 76-85.
- Không trả distance/radius ở MVP; khi có geospatial precision hãy xin tọa độ trình duyệt, fallback về city khi bị từ chối, không tạo distance giả.