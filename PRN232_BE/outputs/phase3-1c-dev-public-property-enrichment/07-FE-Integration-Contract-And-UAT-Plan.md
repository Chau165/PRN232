# FE integration contract và UAT plan

## Trạng thái FE hiện tại

Không sửa FE theo phạm vi. PublicPropertyDetail trong publicBrowsingApi.ts chưa có description, commonAmenities hoặc livingCosts. PropertyDetailPage còn placeholder cho Giới thiệu, Tiện ích chung và Chi phí sinh hoạt; hiện chỉ đọc roomAmenities. RoomDetailModal format service.price thành VND nhưng không hiển thị unit. Không có listener trên TCP 5102 nên không có API process cũ để đối chiếu runtime; đây là source-level contract review.

## JSON mẫu từ fixture synthetic

Mẫu minh họa response enriched ở thời điểm test 2026-10-31 23:59 Asia/Ho_Chi_Minh; tên và dữ liệu đều tổng hợp:

{
  "propertyId": 1,
  "propertyName": "Fixture Alpha",
  "addressText": "Synthetic address",
  "ward": "Fixture ward",
  "district": "Fixture district",
  "city": "Fixture city",
  "primaryImageUrl": null,
  "availableRoomCount": 2,
  "startingPrice": 1500000,
  "description": "Verified Alpha description",
  "commonAmenities": [{ "amenityId": 1, "name": "Common Alpha" }],
  "roomAmenities": [{ "amenityId": 3, "name": "Room amenity", "rentableRoomCount": 2 }],
  "livingCosts": [{
    "serviceId": 11,
    "serviceName": "Internet",
    "price": 120000,
    "unitCode": "room_month",
    "billingBasis": "fixed_room",
    "effectiveFrom": "2026-10-01",
    "effectiveTo": "2026-11-01"
  }]
}

## Tích hợp đề xuất cho phase FE

1. Bổ sung type PublicPropertyDetail với các field trên; không coi field enrichment là có trên backend production trước khi release gate.
2. Giới thiệu: null -> empty state; commonAmenities/roomAmenities/livingCosts [] -> thông báo tương ứng, không dùng mock.
3. Hiện LivingCosts như biểu phí tham khảo của khu trọ; trình bày UnitCode/BillingBasis và kỳ hiệu lực. Ví dụ kWh, m³, phòng/tháng, người/tháng, xe/tháng. Không cộng thành tổng, không gắn required/optional, không để nhầm với rent.
4. Giữ StartingPrice là tiền thuê từ RoomTypes.Price; giá raw API là number, format VND ở UI. Không gom livingCosts với RoomDetailModal services[].price cho tới khi có consumer transition được duyệt.
5. Kiểm thử loading, 404, Property không rentable, description null, từng mảng rỗng, rate hiệu lực chuyển tháng, một phần/toàn bộ room amenity, không lộ owner/customer.

## UAT gate

Cần bật flag chỉ ở môi trường đã migrate và sau approval; nạp dữ liệu thật đã được phê duyệt; cập nhật FE type/render; kiểm thử API-FE thực tế. Test hiện tại không chứng minh trang đang hiển thị dữ liệu mới.