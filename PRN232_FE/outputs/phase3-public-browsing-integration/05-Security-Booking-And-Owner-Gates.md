# Security, Booking và Owner Gates

## FE security boundary

- FE chỉ gọi bốn GET /api/public allow-list từ Phase 2B–2D.
- Không dùng legacy /api/properties hoặc /api/rooms routes.
- Không gửi credential/cookie từ public client.
- Không thêm Auth, role handling, management UI hay write endpoint.
- RoomCode bị ẩn ở danh sách/detail đến khi có owner approval.
- API empty/null fields được giữ nguyên về mặt ngữ nghĩa; placeholder chỉ biểu thị thiếu nội dung, không tạo dữ liệu Property/Room giả.

## Booking boundary

Availability là snapshot, không phải giữ chỗ. Phase 3 không triển khai Booking POST, deposit, thanh toán hoặc lịch xem. Các thao tác tương ứng chưa khả dụng và không gửi request ghi.

## Production blockers theo Phase 2E

1. BLOCKED: anonymous POST /api/roles thiếu [Authorize], cần security fix và positive/negative auth tests.
2. BLOCKED: còn 17 anonymous GET legacy/reference ngoài bốn public allow-list trong tổng số 21 anonymous GET. Legacy property/room routes có dữ liệu owner/status/inventory; consumer inventory và owner scope chưa được đóng.
3. PENDING OWNER: công khai RoomCode.
4. PENDING OWNER: Property không có Room rentable hiện trả 404 generic; owner chưa duyệt semantics này.
5. NOT VERIFIED / BLOCKED: image source, host allow-list, anonymous ACL và quyền dùng URL public; hiện primaryImageUrl null và images rỗng.
6. PENDING OWNER: public equipment visibility; API hiện trả equipments rỗng.
7. PENDING APPROVAL: các GET reference/diagnostic còn anonymous cần quyết định route nào được công khai.
8. NOT VERIFIED: CORS/routing/API base URL trên môi trường production; local API không chạy trong phiên kiểm tra này.

## Đánh giá

FE integration hoàn tất cho development theo contract hiện tại. Production vẫn BLOCKED bởi các gate trên; không được xem test FE với fetch stub hoặc build thành bằng chứng security/UAT live.