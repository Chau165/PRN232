# PRN232_FE — Frontend hệ thống quản lý phòng trọ

React 19, TypeScript strict, Vite 8, Tailwind CSS v4 và React Router.

## Chạy local

```sh
pnpm install
pnpm dev
```

Vite dùng `http://localhost:8443` theo cấu hình hiện tại; có thể đổi bằng biến `PORT`.

## Public browsing API

Trang tìm khu trọ, chi tiết khu trọ, danh sách phòng và chi tiết phòng dùng bốn API GET public ở backend. Mặc định, trình duyệt gọi đường dẫn cùng origin `/api`; Vite dev proxy chuyển tiếp tới `http://localhost:5102`, là HTTP binding trong `PRN232_BE/PRN232.Api/Properties/launchSettings.json`. Để dùng launch profile hoặc máy khác, sao chép `.env.example` thành `.env.local` rồi đặt `PRN232_DEV_API_PROXY_TARGET` theo URL backend thực tế.

`VITE_API_BASE_URL` nhận backend origin (ví dụ `https://localhost:7223`) hoặc prefix `/api`. Không đặt mật khẩu, Supabase service key, JWT signing key hay token vào biến `VITE_`; các giá trị này được đóng gói công khai. Dev proxy chỉ áp dụng cho Vite dev server; không thay đổi CORS của backend.

Các request public không gửi Authorization/cookie và không cache availability. Booking, đặt cọc, thanh toán và lịch xem chưa có API ghi an toàn nên không được kích hoạt từ luồng public.

## Dữ liệu và cấu trúc

Public browsing lấy dữ liệu từ API. Các màn hình customer, management và tiện ích demo ngoài phạm vi này vẫn dùng dữ liệu/mock flow riêng.

```text
src/
├─ api/          typed public API client
├─ pages/        trang public, customer và management
├─ components/   component theo domain
├─ data/         fixture/mock data cho flow chưa tích hợp
├─ types/        contract và model FE
└─ utils/        helper giao diện
```
