# Phase 3 — FE to Public API Mapping

Nguồn đối chiếu: Phase 2B listing contract, Phase 2C property detail/room list contract, Phase 2D room detail contract, Phase 2E end-to-end contract review và source thực tế trong PRN232_BE.

| FE flow | API | Dữ liệu dùng trong UI | Hạn chế được giữ nguyên |
|---|---|---|---|
| Trang chủ / thẻ Property | GET /api/public/properties | propertyId, propertyName, addressText, ward/district/city, primaryImageUrl, amenitiesSummary, availableRoomCount, startingPrice; response phân trang items/totalCount/page/pageSize | amenitiesSummary là tiện ích từ Room đại diện, không được trình bày như tiện ích toàn Property. Không có distance/toạ độ/map pin. Ảnh nullable nên dùng placeholder trung tính. |
| Chi tiết Property | GET /api/public/properties/{propertyId} | propertyId, propertyName, addressText, ward/district/city, primaryImageUrl, availableRoomCount, startingPrice | API không có mô tả, nội quy, property-level amenities, chi phí, map/distance hoặc gallery. Unknown và Property không có Room rentable đều là 404 generic; zero-room semantics còn chờ owner. |
| Danh sách Room | GET /api/public/properties/{propertyId}/rooms | roomId, roomCode, roomTypeId, roomTypeName, baseRent, areaSqm, primaryImageUrl, amenitiesSummary; query lọc/phân trang | Không có floor, capacity, ngày vào ở, mô tả, equipment hay occupancy. RoomCode giữ theo contract nhưng bị ẩn trong FE đến khi owner duyệt. |
| Chi tiết Room | GET /api/public/rooms/{roomId} | roomId, areaSqm, roomType, baseRent, property summary, images, amenities, services (serviceId/name/price), equipments (equipmentId/name) | images và equipments hiện rỗng. Không có floor/capacity/available date/description. RoomCode bị ẩn. Service price hiển thị riêng, không cộng vào rent và không gắn chu kỳ thanh toán. |

## Quy tắc dữ liệu

- Dùng ID số trên route để tải Property, room list và Room detail; không parse slug/code để suy ra ID.
- Chuỗi rỗng, null, array rỗng và field thiếu không được thay bằng dữ liệu demo hoặc giá trị nghiệp vụ suy đoán.
- Giá được định dạng VND từ giá trị API; không tự thêm “/tháng”, do contract không xác nhận chu kỳ.
- Ảnh không có URL thì giao diện hiển thị trạng thái không có ảnh; không lấy ảnh stock làm dữ liệu Property.
- Room detail được tải mới theo roomId khi mở modal; không dùng dữ liệu card làm detail cache.