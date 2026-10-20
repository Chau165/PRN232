# Phase 4A — Rủi ro và việc còn lại trước production

## Cần xử lý trước khi xem là production-ready

1. **JWT chưa bị thu hồi tức thì khi khóa user/đổi role/đổi mật khẩu.** `UpdateRoleAsync` và `UpdateStatusAsync` chỉ cập nhật DB; JWT giữ claim role đã cấp lúc login và Backend chỉ kiểm tra hạn token. Token đang còn hạn có thể tiếp tục gọi API theo claim cũ. Cần quyết định và triển khai server-side revocation hoặc kiểm tra trạng thái/role hiện hành trên request, rồi bổ sung kiểm thử bảo mật.
2. **Gán Manager cho khu trọ chưa được hỗ trợ bởi Backend.** Không có API ghi `OwnerId`/assignment; nếu nghiệp vụ cần phân công, cần bổ sung contract/backend và kiểm thử authorization trước khi bật thao tác ghi ở FE.
3. **Session token nằm trong `sessionStorage`.** Token được giới hạn theo tab và FE tự hết hạn theo JWT, nhưng vẫn đọc được bởi JavaScript cùng origin. Backend hiện không có refresh/logout endpoint hoặc HttpOnly-cookie flow; cần đánh giá CSP/XSS và cơ chế session phù hợp trước production.

## Còn ngoài Phase 4A

- Các trang management nghiệp vụ khác ngoài Users/Managers vẫn có dữ liệu fixture; đăng nhập thật không làm chúng thành API-backed.
- Chưa chạy UAT với tài khoản thật vì không có test credentials. Cần kiểm thử lần lượt Customer, Manager và Admin trên môi trường test, gồm disable/role change và session đang hoạt động.
- FE build thành công nhưng bundle chính vượt cảnh báo 500 KB; có thể tách chunk trong một phase tối ưu riêng.

Không có migration, live seed, commit, push hoặc deploy trong Phase 4A.
