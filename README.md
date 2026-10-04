# PRN232 — Hệ thống Quản lý Phòng Trọ

Đồ án môn PRN232, FPT University HCM. Hệ thống quản lý phòng trọ cho thuê (boarding
house rental management): tìm phòng, đặt lịch xem phòng, đặt cọc, ký hợp đồng, hoá đơn
điện nước, bảo trì thiết bị — cho 3 vai trò Khách thuê / Quản lý / Admin.

## Cấu trúc repo

```
.
├─ PRN232_FE/   Frontend — React 19 + Vite + TypeScript + Tailwind CSS
└─ PRN232_BE/   Backend  — ASP.NET Core Web API (N-layer) + EF Core + PostgreSQL (Supabase)
```

Mỗi thư mục có README riêng với hướng dẫn setup/chạy chi tiết — xem
[`PRN232_FE/README.md`](./PRN232_FE/README.md) và [`PRN232_BE/README.md`](./PRN232_BE/README.md).

## Tech stack tóm tắt

- **Frontend**: React 19, Vite, TypeScript, Tailwind CSS v4, react-router v8
- **Backend**: ASP.NET Core 8 Web API, Entity Framework Core (Code-First + Migrations), Mapperly, FluentValidation
- **Database**: Supabase (PostgreSQL) — chỉ dùng làm data store, Auth/RLS không dùng vì FE luôn gọi qua API backend
- **Auth**: JWT + ASP.NET Core Identity, Policy-based Authorization theo role (ADMIN/MANAGER/CUSTOMER)
- **Deploy**: Railway (cả backend lẫn frontend), Docker cho backend
- **Khác**: Swagger/OpenAPI, xUnit, Rate Limiting (ASP.NET Core built-in)

Tài liệu tech stack đầy đủ (lý do chọn từng công nghệ, mức độ ưu tiên, phần AI tuỳ chọn)
nằm trong tài liệu nhóm chia sẻ riêng, không lặp lại ở đây để tránh lệch thông tin khi
tài liệu đó được cập nhật.

## Bắt đầu nhanh

1. Backend: xem [`PRN232_BE/README.md`](./PRN232_BE/README.md) — cài .NET 8 SDK, cấu
   hình connection string Supabase qua `dotnet user-secrets`, chạy migration, `dotnet run`.
2. Frontend: xem [`PRN232_FE/README.md`](./PRN232_FE/README.md) (hoặc `package.json`
   nếu chưa có README) — `pnpm install`, `pnpm dev`.

## Phân công & quy trình

Làm việc trên nhánh riêng theo module, mở Pull Request để merge vào `main`, không push
thẳng. Chi tiết phân chia module xem phần "Phân module cho từng thành viên" trong
`PRN232_BE/README.md`.
