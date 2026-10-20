# Phase 4A — Báo cáo triển khai

## Kết quả

Đã tích hợp Auth, hồ sơ và quản trị User/Manager với các API Backend hiện có. Không sửa schema, không migration/seed, không ghi dữ liệu live, không commit/push/deploy. Public Property/Room browsing và `src/constants/routes.ts` được giữ nguyên.

## Backend

- `POST /api/roles` nay yêu cầu role `Admin`; các GET role vẫn công khai để FE đọc danh sách role.
- Không viết lại Auth/User API. Source hiện tại đã giới hạn đăng ký công khai thành role `Customer`, và các API tạo user, đổi role, đổi trạng thái đã yêu cầu Admin.
- Thêm kiểm thử hồi quy xác nhận đăng ký công khai luôn gán Customer và TestServer xác nhận tạo role trả 401 cho anonymous, 403 cho Customer, 201 cho Admin.

## Frontend

- Bổ sung typed API client dùng `VITE_API_BASE_URL`, hỗ trợ ProblemDetails và chỉ gắn `Authorization: Bearer` cho request được bảo vệ.
- AuthProvider lưu access token và hạn dùng trong `sessionStorage`, xác minh lại `GET /api/users/me` sau reload, tự xóa session khi hết hạn/401/404 hoặc tài khoản không còn hợp lệ; 403 được giữ là lỗi quyền truy cập. Không lưu mật khẩu hoặc user profile vào storage.
- Login/Register gọi `POST /api/auth/login` và `/api/auth/register`; role lấy từ response Backend. CUSTOMER chuyển về `/`; MANAGER và ADMIN dùng route thực tế chung `/management/dashboard`.
- ProtectedRoute giới hạn route khách thuê cho CUSTOMER, route management cho MANAGER/ADMIN, và các màn Areas/Reports/Managers/Users cho ADMIN. Public home/property/room routes vẫn public.
- Profile tải từ `/api/users/me`, cập nhật tên/số điện thoại qua PUT, đổi mật khẩu qua `/api/users/me/password`; email/role không thể sửa tại FE. Navbar và management header dùng user hiện tại; phần tên profile dùng trong khi gửi khiếu nại hóa đơn cũng không còn lấy từ fixture Ngọc Anh.
- ManagementUsers gọi API list/detail/create/role/status. ManagementManagers tra role `Manager` từ `GET /api/roles`, lọc user bằng roleId trả về, tạo account bằng `POST /api/users`, đọc properties theo OwnerId.
- Xóa tài khoản demo, OTP giả, mock session/query role bypass, hồ sơ Ngọc Anh và danh sách user/manager mẫu. Bỏ modal “Phân công” giả; trang Manager thông báo Backend chưa có API ghi phân công.

## Route role đã áp dụng

| Quyền | Route |
|---|---|
| Public | `/`, `/property/:propertyId`, `/property/:propertyId/rooms` |
| CUSTOMER | `/my-room`, `/my-contracts`, `/my-bills`, `/my-bookings`, `/notifications`, `/deposit`, `/payment-success` |
| CUSTOMER, MANAGER, ADMIN | `/profile` |
| MANAGER, ADMIN | Dashboard và các route quản lý properties/rooms/tenants/bookings/viewings/contracts/invoices/equipment/maintenance |
| ADMIN | Areas, Reports, Managers, Users |

## Giới hạn ngoài phạm vi có xác nhận

- Backend không có endpoint đổi `Property.OwnerId`/gán Manager. Giao diện không giả vờ lưu phân công; properties đang hiển thị chỉ là records có `OwnerId` trùng Manager.
- Các màn nghiệp vụ quản lý khác ngoài Users/Managers vẫn dùng fixture hiện có; phase này không chuyển toàn bộ management domain sang API.
- Không có tài khoản kiểm thử được cung cấp nên chưa chạy đăng nhập live hoặc kiểm thử trình duyệt FE→API→DB bằng user thật. Kiểm thử tự động dùng HTTP stub ở FE và TestServer/fixture ở BE.

## Diff

`phase4a-only.patch` chứa source và test của riêng Phase 4A, không kèm các thay đổi Phase 2/3 hoặc thay đổi sẵn có khác trong working tree.
