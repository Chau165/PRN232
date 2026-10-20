# API Client, Routing và Configuration

## Public endpoints được gọi

| Method | Path |
|---|---|
| GET | /api/public/properties |
| GET | /api/public/properties/{propertyId} |
| GET | /api/public/properties/{propertyId}/rooms |
| GET | /api/public/rooms/{roomId} |

Client chỉ tạo GET. Không gửi bearer token/cookie và không gửi Booking, deposit hay schedule write.

## Base URL và local proxy

- VITE_API_BASE_URL mặc định là /api; có thể đặt origin API hoặc base /api.
- Client chuẩn hoá suffix /api để tránh lặp tiền tố trong path; từ chối base URL chứa credential, query hoặc fragment.
- .env.example đặt VITE_API_BASE_URL=/api và PRN232_DEV_API_PROXY_TARGET=http://localhost:5102.
- Vite proxy /api dùng target từ process env hoặc file env PRN232_DEV_API_PROXY_TARGET, mặc định http://localhost:5102; proxy này chỉ dành cho dev server.
- API launch profile trong BE khai báo HTTP localhost:5102 và HTTPS localhost:7223.

## Query và lỗi

- amenityIds được gửi thành nhiều query key, loại trùng và giới hạn phù hợp với contract.
- Giá decimal được giữ dưới dạng chuỗi để không làm tròn khi serialize.
- Page mặc định là 1, pageSize mặc định là 10 và tối đa 50.
- 400 được chuyển thành validation error; 404 thành not-found; lỗi mạng/server có trạng thái riêng. Nội dung diagnostic của server không được hiển thị cho người dùng.
- Mỗi request nhận AbortSignal từ view để huỷ request khi unmount/thay truy vấn.

## Triển khai production

Cần cấu hình VITE_API_BASE_URL theo origin/base path thật và xác nhận deployment routing/CORS trên môi trường mục tiêu. Vite proxy không thay thế cấu hình production.