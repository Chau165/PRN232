# PRN232_FE — Frontend Hệ thống Quản lý Phòng Trọ

React 19 + Vite 8 + TypeScript (strict) + Tailwind CSS v4 + react-router v8.

## Chạy local

```bash
pnpm install
pnpm dev
```

Mặc định chạy ở `http://localhost:5173`.

## Trạng thái hiện tại

Toàn bộ dữ liệu đang là mock (`src/data/*.ts`), chưa nối API backend thật. Khi backend
(`PRN232_BE`) sẵn sàng, cần thêm HTTP client (Axios hoặc fetch wrapper) để gọi API thay
cho mock, và thay `authMock.ts` / `managementAuth.ts` bằng gọi API JWT thật — xem thêm
trong README gốc của repo và tài liệu tech stack của nhóm.

## Biến môi trường

Chưa cần `.env` ở bước hiện tại (chưa gọi API). Khi nối backend thật, thêm biến base URL
API, ví dụ:

```
VITE_API_BASE_URL=http://localhost:8080
```

## Cấu trúc thư mục chính

```
src/
├─ pages/        từng trang theo route (customer-facing + management/admin)
├─ components/   component dùng chung theo domain (auth, customer, management, room, ...)
├─ data/         mock data (sẽ thay bằng gọi API)
├─ types/        type định nghĩa theo domain
└─ utils/        logic phụ trợ (scoping theo property, mock auth, ...)
```
