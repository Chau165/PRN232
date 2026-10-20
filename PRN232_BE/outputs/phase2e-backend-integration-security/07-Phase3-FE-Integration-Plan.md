# Phase 3 — Kế hoạch tích hợp FE (analysis only)

## Hiện trạng FE

Không có `fetch`, Axios/API client hoặc `/api/` callsite trong `PRN232_FE/src`; README nói cần thêm HTTP client. Home dùng `src/data/properties.ts`; Property Detail dùng `properties[0]`, `propertyDetails.ts` và ảnh mock; Available Rooms dùng `src/data/rooms.ts`; management properties/rooms dùng `getManagementScope`. Router hiện dùng slug cố định `/property/sunrise-residence` và `/property/sunrise-residence/rooms`, room detail là modal. Không có sửa FE trong Phase 2E.

| FE view hiện tại | Public API Phase 3 | Field khớp | FE field chưa có / cần quyết định |
|---|---|---|---|
| Home: `HomePage`, `SearchSection`, property card | `GET /api/public/properties` | `id→propertyId`, `name→propertyName`, `address→addressText`, `rooms→availableRoomCount`, `price→startingPrice`, amenities→`amenitiesSummary` | `distance`, tọa độ `x/y`, bản đồ chính xác, ảnh thật. `amenitiesSummary` mô tả Room đại diện, không phải tiện ích toàn Property. |
| Property detail: `PropertyDetailPage` | `GET /api/public/properties/{propertyId}` | ID, tên, địa chỉ/khu vực, count, giá bắt đầu | Mô tả, nội quy, property-level amenities, chi phí, tọa độ/distance, gallery. `primaryImageUrl` là null. Zero-room hiện là 404. |
| Room list: `AvailableRoomsPage`, `AvailableRooms` | `GET /api/public/properties/{propertyId}/rooms` | `roomId`, RoomCode, RoomType ID/name, `baseRent`, `areaSqm`, `primaryImageUrl`, `amenitiesSummary` | floor, capacity, ngày có thể dọn vào, mô tả, equipment. API có `roomTypeName` filter nhưng FE hiện chưa nối API; không suy đoán filter/UI đã có. |
| Room detail: `RoomDetailModal` | `GET /api/public/rooms/{roomId}` | Room ID/code, type, giá, diện tích, Property summary, amenities, services | `images: []`, `equipments: []`; thiếu floor/capacity/available date/description. `RoomCode` hiển thị chờ owner sign-off; dùng RoomId để gọi/navigate. |

`RoomTypes.Price`/`Services.Price` không có billing unit được lưu. FE hiện hiển thị tiền `/ tháng`; cần bỏ/đổi nhãn đó cho tới khi owner xác nhận semantics, không cộng service vào rent.

## Đề xuất task slices

1. **Client/contract:** thêm typed API client, base URL config, cancellation, map `400 ValidationProblemDetails`, generic `404` và lỗi mạng. Thêm type từ DTO thật; giữ numeric `PropertyId`/`RoomId` trong route state, slug chỉ để hiển thị nếu cần.
2. **Home:** thay mock listing bằng page/filter API; loading skeleton, empty state, retry/error; ẩn map/distance khi API không có coordinate/distance. Format price không tự thêm kỳ thanh toán.
3. **Property detail + rooms:** gọi detail rồi room page, truyền `propertyId`; filters/pagination theo query API; xử lý 404 khi property hết eligibility và 200 rỗng khi filter không có kết quả.
4. **Room detail:** mở route/modal bằng `roomId`, fetch detail riêng để xử lý trường hợp phòng vừa bị giữ; generic not-found state không phân biệt unknown/blocked; placeholder FE cho ảnh/thiết bị, không thay bằng URL minh họa.
5. **Security/write boundary:** nút `Đặt cọc giữ phòng` hiện chuyển sang mock Deposit bằng `RoomCode`. Disable hoặc chuyển sang login/placeholder cho đến khi Booking API chống concurrent reservation được triển khai. GET availability không giữ phòng.
6. **Management integration:** tách task; chỉ dùng endpoint có JWT và owner/Admin policy đã chứng minh. Không dùng legacy anonymous DTO như management contract.

## Test/UAT đề xuất

- Unit/contract tests cho mapping DTO, giá decimal, null/empty arrays và không gắn billing unit giả.
- FE API tests cho 400 validation, empty page, 404 do stale availability, timeout/cancel; loading skeleton và retry.
- Browser E2E: Home → Property Detail → Rooms → Room Detail; lọc/pagination; 404 khi Room vừa bị giữ; ảnh/thiết bị placeholder; nút booking không gửi write request.
- `PRN232_FE/package.json` hiện chưa có test script hoặc Playwright. Chọn test runner/E2E setup trong Phase 3; không cài thêm trong Phase 2E.
