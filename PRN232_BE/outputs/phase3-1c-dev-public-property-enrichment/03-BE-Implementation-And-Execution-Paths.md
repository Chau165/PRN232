# Backend implementation và execution paths

## Luồng gọi

PublicPropertiesController giữ nguyên endpoint và gọi IPublicPropertyListingService. Program.cs đăng ký PublicPropertyListingService bằng DI factory, đọc PublicBrowsing:PropertyDetailEnrichmentEnabled; bool không hợp lệ hoặc thiếu được xem là false.

PublicPropertyListingService.GetDetailAsync lấy TimeProvider.GetUtcNow một lần, chuẩn hóa UTC rồi:
- flag false: gọi GetDetailAsync cũ;
- flag true: chuyển cùng instant sang Asia/Ho_Chi_Minh, lấy DateOnly, gọi GetEnrichedDetailAsync.

IPublicPropertyListingRepository khai báo method enriched riêng. PublicPropertyListingRepository dùng một core query, giữ transaction Repeatable Read khi relational và projection AsNoTracking. Query/rentability hiện tại không bị thay đổi.

## Provider queries khi enriched

Với Property còn phòng rentable, repository chạy 5 truy vấn có giới hạn:
1. Property detail, AvailableRoomCount và StartingPrice từ tập rentable hiện có.
2. RoomAmenities tổng hợp từ các Room rentable của Property.
3. Description của đúng PropertyId.
4. PropertyAmenities của đúng PropertyId.
5. PropertyServiceRates theo đúng PropertyId và khoảng hiệu lực.

Các bảng mới chỉ được truy vấn sau khi nhánh enriched được gọi. Rates được project phía server, ổn định theo Service.Name rồi ServiceId. Không có vòng lặp query từng phòng/dịch vụ và không dùng dữ liệu mock. Không có rate vẫn trả [].

Nếu detail query không tìm thấy Property public, method trả null/404 trước khi query các bảng enrichment. CancellationToken được truyền qua các lệnh EF.

## Tính chất không thay đổi

Listing, property room list, public room detail, rentability Phase 2A, giá thuê RoomTypes.Price, booking/contract status, controller routes và API auth không bị sửa. Không chỉnh migration, model snapshot, DbContext hoặc entity trong phase này.