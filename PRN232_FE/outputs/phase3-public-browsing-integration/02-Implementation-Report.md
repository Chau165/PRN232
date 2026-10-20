# Phase 3 — Implementation Report

## Kết quả theo giai đoạn

### 3A — API client, routing và cấu hình

- Thêm typed public browsing client cho bốn GET allow-list, response DTO, query serialization và xử lý validation/404/network/server error.
- Kiểm tra query giá decimal, page/pageSize, sort, amenityIds lặp và ID route trước khi gửi request.
- Hỗ trợ AbortSignal; component bỏ qua response cũ khi người dùng tìm kiếm tiếp.
- Thêm cấu hình VITE_API_BASE_URL, mẫu .env.example và Vite dev proxy có thể cấu hình qua PRN232_DEV_API_PROXY_TARGET.
- Không thêm auth token, cookie hoặc phương thức ghi.

### 3B — Home và tìm kiếm Property

- Home tải listing từ API; URL giữ keyword, city, district, ward, khoảng giá, loại Room, amenityIds, sort và page.
- Nối bộ lọc hiện hữu với query server; đổi filter reset page, đổi page giữ filter.
- Thêm loading, empty success, validation, network/server error, retry và pagination.
- Thẻ chỉ dùng field trong DTO. Bỏ khoảng cách, pin bản đồ, ảnh demo và tuyên bố giá theo tháng. Khu vực map được giữ nhưng báo rõ dữ liệu bản đồ chưa có.

### 3C — Property detail và danh sách Room

- Đổi route sang ID Property và tải detail/room page theo ID.
- Nối filter giá, RoomType, amenityIds, sort và paging cho danh sách Room.
- Phân biệt trang lọc không có kết quả với Property không tồn tại/không còn Room rentable.
- Chỉ hiển thị field có trong contract; thiếu ảnh dùng trạng thái không có ảnh. RoomCode bị ẩn chờ owner approval.

### 3D — Room detail và hành động chưa hỗ trợ

- Modal tải GET /api/public/rooms/{roomId} mới mỗi lần chọn Room.
- Render thông tin Property, RoomType, giá, diện tích, tiện ích, services và equipment nếu API trả dữ liệu.
- Hình/equipment rỗng được trình bày đúng trạng thái; service price không bị cộng vào rent.
- Thêm đóng bằng Escape/backdrop, focus trap và khôi phục focus.
- Deposit và lịch xem không kích hoạt request hay chuyển sang luồng booking; nút được disable và ghi rõ chưa hỗ trợ. Không có Booking write.

## Mock data và phạm vi

Mock Property/Property detail cho public flow và mock map/schedule component không còn được dùng, các file public-only không còn cần thiết đã được xoá. Dữ liệu Room dùng ở luồng deposit/customer ngoài public browsing được giữ nguyên.

Không chỉnh backend, database, Auth, API quản trị; không tạo commit, push hoặc deploy. BE đang có các thay đổi Phase 2 từ trước khi bắt đầu Phase 3; chúng khớp baseline đã lưu và không thuộc Phase 3 diff.