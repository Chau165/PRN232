# Phase 4A — Báo cáo kiểm thử

## Đã chạy

| Kiểm tra | Kết quả |
|---|---|
| Backend `PRN232.Application.Tests` | 203 passed, 7 skipped, 0 failed (210 tổng) |
| FE Vitest | 44 passed (5 test files) |
| FE management contract tests | 6 passed |
| TypeScript | `tsc --noEmit` thành công |
| Vite production build | Thành công |
| `git diff --check` cho source tracked của Phase 4A | Không có lỗi whitespace |

Backend tests được build/run ở `outputs/phase4a-auth-users-profile-role-routing/be-test` vì API local đang chạy giữ khóa file trong `PRN232.Api/bin`; không dừng hoặc sửa tiến trình đó. Bảy test PostgreSQL hiện có bị Skip do fixture/điều kiện PostgreSQL của chúng không được bật trong lần chạy này.

## Luồng được kiểm chứng tự động

- Backend: đăng ký công khai luôn trả role Customer; tạo role bị chặn với anonymous/Customer và cho phép Admin.
- FE: exact login/register request DTO; không có OTP/role selector/demo credentials; mapping và chuyển hướng Customer/Manager/Admin; reload với Bearer `/users/me`; route role guard; session hết hạn khi đang hoạt động/401/403; cập nhật profile, đổi mật khẩu, logout và đồng bộ tên Navbar; Admin list/detail/role/status; Manager roleId được tra từ API và tạo account qua `/users`.
- Public browsing tests trong suite hiện có vẫn pass; public API client không được thay đổi.

## Chưa xác minh live

- Không thực hiện đăng nhập thật hoặc gọi API có thể ghi dữ liệu vì không có test identity/approval để dùng.
- Không xác minh UAT trực quan trên trình duyệt hoặc luồng qua DB live. Test FE sử dụng HTTP stub; TestServer BE không dùng Supabase.
- Vite cảnh báo chunk JS minified khoảng 505 KB, cao hơn ngưỡng cảnh báo 500 KB; build vẫn thành công.
