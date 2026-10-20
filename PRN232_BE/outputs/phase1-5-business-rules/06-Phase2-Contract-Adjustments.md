# 06 — Điều chỉnh hợp đồng API Phase 2

Ngày lập/đối chiếu: 2026-10-19 (Asia/Saigon). Snapshot live Supabase: 2026-10-18 17:00:33 UTC (transaction READ ONLY, ROLLBACK). Bốn route dưới đây vẫn là **PROPOSED**; không route nào được cài trong Phase 1.5.

## Kết luận chung

Giữ Strategy B: public browsing routes và DTO allow-list riêng. Các blockers về publication, Booking, Contract, Reserved và privacy phải được giải quyết trước khi trả kết quả ẩn danh. Không đổi semantics hoặc response của management routes hiện tại trong phase này.

## Review bốn endpoint

| Endpoint dự kiến | Trạng thái | Điều chỉnh bắt buộc |
|---|---|---|
| GET /api/public/properties | Hợp lý nhưng **BLOCKED** | Một item/Property; publication predicate; rentability query thống nhất; count và min price từ cùng rentable Room set; page/pageSize bounded; keyword/location/sort; không có OwnerId/OwnerName. Mặc định loại zero availability là PROPOSED. |
| GET /api/public/properties/{id} | Hợp lý nhưng **BLOCKED** | 404 nếu hidden/unpublished; response count/price cùng query predicate với list; gallery chỉ từ Room images và phải có sourceRoomId; không bịa Property description/rules/image; zero-room behavior cần policy. |
| GET /api/public/properties/{id}/rooms | Hợp lý nhưng **BLOCKED** | Chỉ trả Room trong cùng RentableRooms predicate; page/pageSize và stable RoomId tie-break; property hidden trả 404; property public hết phòng có thể 200 items=[] nếu policy cho phép. |
| GET /api/public/rooms/{id} | Hợp lý nhưng **BLOCKED** | Chỉ trả Room nếu Room rentable và Property public; 404 cho không tồn tại/hidden/non-rentable để tránh dò trạng thái; DTO chỉ derived rentableNow, không trả raw Status/Booking/Contract. |

Truy cập anonymous nên được đánh dấu rõ cho route mới sau khi các điều kiện an toàn được duyệt. Hiện source có GET anonymous cũ; không dùng chúng làm DTO public mới.

## Response fields và pricing

- availableRoomCount: số DISTINCT Room đủ điều kiện thuê trong Property; **APPROVED**.
- startingPrice: MIN giá của RoomTypes.Price qua đúng Room.RoomTypeId, từ cùng tập; null khi count bằng 0; **APPROVED**.
- Giá nằm trên RoomTypes, không phải Room. Giữ Option 1; không thêm Rooms.MonthlyPrice/bảng giá Property.
- Có thể thêm availabilityAsOfUtc nếu UI cần biết freshness; chỉ ghi derived result, không cho biết Booking/Contract nào gây block.
- RoomTypeId có thể xuất hiện trên Room detail/list như ID của row giá thật. Không dùng nó làm đại diện cho mọi Room có cùng TypeName.

## Filters và cùng-Room semantics

- Public search theo tên logic nên có roomTypeName; query phải tìm mọi RoomTypeId có tên khớp theo normalization đã chọn. roomTypeId chỉ là filter chính xác một row, hữu ích cho quản trị; hai tham số nên loại trừ nhau.
- Current FE chỉ có SelectControl label “Loại phòng” tại PRN232_FE/src/components/home/SearchSection.tsx:40; không thấy request/API hoặc tham số RoomTypeId/TypeName trong src. Vì vậy hiện chưa có contract UI để kế thừa.
- Room price, roomTypeName/ID và amenityIds phải cùng áp lên một Rentable Room. Với nhiều amenities, yêu cầu cùng một Room có đủ các amenity; không tổng hợp từ nhiều phòng của Property.
- Giá/count trên thẻ theo đề xuất là trên toàn bộ rentable Rooms của Property, không đổi theo filter; filter chỉ quyết định Property có match. Nếu Product Owner muốn số phòng khớp filter, thêm field tên riêng như matchingRoomCount.
- Bỏ occupantCount, floor, availableFrom, radiusKm/distance trong MVP: schema không có dữ liệu tương ứng hoặc precision tọa độ không đủ. Không tính tổng service charge/tháng vì Services.Price không có unit/mandatory flag.

## Images, amenities, services, equipment

- RoomAmenities lấy theo đúng Room; API property summary chỉ được ghi rõ đây là amenity của phòng đại diện, không đại diện toàn Property.
- Images hiện gắn Room. Chỉ dùng ảnh từ Room thuộc tập được public; nếu không có ảnh trả null/empty và FE dùng placeholder.
- Service phí lấy theo RoomTypeId -> RoomTypeServices -> Services; giữ price riêng, không gán chu kỳ hoặc cộng vào monthly total.
- Public equipment projection nếu cần chỉ allow-list field Name. Không trả Supplier, InstallDate, WarrantyExpiry, internal Status.
- Không trả Contract/Booking/Customer/payment/maintenance records hoặc ID liên kết của chúng.

## Pagination, privacy và tương thích

- Listing: page >= 1, pageSize mặc định nhỏ và giới hạn tối đa (Phase 1 gợi ý 1–50); sort phải có tie-break PropertyId.
- Room list: sort giá rồi RoomId để paging ổn định.
- Filters/count/price/page chạy trong DB; dùng EXISTS/aggregate server-side, tránh N+1.
- DTO public tách khỏi PropertyDto/RoomDto; source hiện tại PropertyDto có OwnerId/OwnerName và RoomDto có raw Status. Không trả hai DTO này cho khách.
- Legacy GET /api/properties và GET room endpoints đang anonymous. GET properties hiện phát OwnerId/OwnerName; GET rooms có thể trả mọi status. Route mới an toàn không tự sửa hoặc vô hiệu hóa exposure của route cũ. Owner/security maintainer cần quyết định riêng có được giữ exposure hay cần harden, ngoài scope read-only task này.
- Phase 2 cần test contract 404 cho hidden/non-rentable, no PII, duplicate TypeName mapping, aggregate count/price, same-room filters, stable pagination, và CRUD management tương thích.

## Acceptance criteria sau khi blockers được duyệt

1. Mỗi Home card là một Property.
2. Mọi số count/price dùng chung một predicate rentable đã được owner duyệt.
3. Room Available có blocking Booking/Contract không bị trả.
4. Không có API response chứa Customer/Owner/Booking/Contract PII.
5. Duplicate TypeName lọc đúng mọi biến thể ID theo rule normalization được duyệt; Room vẫn trả giá/service theo ID thực.
6. Property chưa publish, Room Reserved/Occupied/Maintenance hoặc status lạ không được quảng bá.
7. Filter price/type/amenity không ghép điều kiện từ các Room khác nhau.
8. Pagination ổn định, giới hạn response; management endpoints giữ nguyên semantics đã kiểm thử.

